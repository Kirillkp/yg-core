using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YgCore.Contracts;
using YgCore.Core;

namespace YgCore.Store
{
    /// <summary>
    /// Магазин игры: покупка за монеты, покупка за реальные деньги через платформу, экипировка и восстановление
    /// из сохранения. Только логика, без экранов.
    ///
    /// Главные правила (перенесены из «Обби Потоп»):
    /// 1. Покупка за реальные деньги выдаётся в два шага. Сначала товар выдаётся игроку и прогресс записывается,
    ///    и только потом покупка подтверждается платформе (для пакетов валюты). Если игра закроется между шагами,
    ///    при следующем запуске <see cref="RestoreAsync"/> вернёт невыданную покупку.
    /// 2. Один и тот же токен покупки выдаётся только один раз: обработанные токены хранятся в прогрессе.
    /// 3. Деньги тратятся только через <see cref="IEconomyService"/>, а не напрямую.
    /// 4. Идентификаторы, которых уже нет в каталоге, не теряются: они остаются в сохранении (игрок мог ими владеть),
    ///    но магазин их игнорирует.
    /// </summary>
    public sealed class SessionStoreService
    {
        private readonly IEconomyService economyService;
        private readonly IPurchasesService purchasesService;
        private readonly IReadOnlyList<StoreItemDefinition> catalog;
        private readonly StoreOptions options;
        private readonly Dictionary<string, StoreItemDefinition> itemsById = new Dictionary<string, StoreItemDefinition>();
        private readonly Dictionary<string, StoreItemDefinition> itemsByProductId = new Dictionary<string, StoreItemDefinition>();
        private readonly Dictionary<string, PurchaseProduct> platformProductsById = new Dictionary<string, PurchaseProduct>();
        private readonly HashSet<string> ownedItems = new HashSet<string>();

        // Идентификаторы из сохранения, которых больше нет в каталоге: хранятся в сохранении, магазином игнорируются.
        private readonly HashSet<string> retainedUnknownItems = new HashSet<string>();
        private readonly HashSet<string> processedPurchaseTokens = new HashSet<string>();
        private readonly Dictionary<int, string> equippedItems = new Dictionary<int, string>();
        private Func<Task<bool>> criticalSaveHandler;
        private IStoreEquipmentRules equipmentRules;

        /// <param name="economyService">Экономика, из которой списываются монеты и в которую зачисляются.</param>
        /// <param name="purchasesService">Покупки платформы (можно null: тогда доступны только покупки за монеты).</param>
        /// <param name="catalog">Каталог товаров игры.</param>
        /// <param name="options">Предметы по умолчанию и необязательные категории.</param>
        public SessionStoreService(
            IEconomyService economyService,
            IPurchasesService purchasesService,
            IReadOnlyList<StoreItemDefinition> catalog,
            StoreOptions options = null)
        {
            this.economyService = economyService;
            this.purchasesService = purchasesService;
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.options = options ?? new StoreOptions();

            foreach (var item in this.catalog)
            {
                itemsById[item.Id] = item;
                if (!string.IsNullOrWhiteSpace(item.PlatformProductId))
                {
                    itemsByProductId[item.PlatformProductId] = item;
                }
            }

            GrantDefaults();
        }

        /// <summary>Изменилось что-то, что показывают экраны (владение, надетое, цены платформы).</summary>
        public event Action Changed;

        /// <summary>Результат действия для показа игроку.</summary>
        public event Action<StoreStatus> StatusChanged;

        public IReadOnlyList<StoreItemDefinition> Catalog => catalog;

        /// <summary>
        /// Задаёт действие «записать прогресс сейчас и вернуть, получилось ли». Магазин вызывает его после выдачи покупки
        /// и до подтверждения платформе. Обычно это <c>ProgressionService.SaveCriticalAsync</c>. Без обработчика
        /// покупки за реальные деньги не завершаются (запись считается неудачной).
        /// </summary>
        public void SetCriticalSaveHandler(Func<Task<bool>> handler)
        {
            criticalSaveHandler = handler;
        }

        /// <summary>Задаёт правила блокировки слотов экипировки (можно null).</summary>
        public void SetEquipmentRules(IStoreEquipmentRules rules)
        {
            equipmentRules = rules;
            Changed?.Invoke();
        }

        public bool IsOwned(string itemId)
        {
            return ownedItems.Contains(itemId);
        }

        public bool IsEquipped(string itemId)
        {
            if (!itemsById.TryGetValue(itemId, out var item))
            {
                return false;
            }

            return equippedItems.TryGetValue(item.Category, out var equippedId)
                && equippedId == itemId;
        }

        /// <summary>Надетый предмет категории или пустая строка.</summary>
        public string GetEquipped(int category)
        {
            return equippedItems.TryGetValue(category, out var itemId)
                ? itemId
                : string.Empty;
        }

        /// <summary>Слот предмета заблокирован другим надетым предметом (например, полным костюмом).</summary>
        public bool IsBlockedByEquipment(string itemId)
        {
            return itemsById.TryGetValue(itemId ?? string.Empty, out var item)
                && item.IsEquippable
                && IsSlotBlocked(item.Category);
        }

        public bool IsSlotBlocked(int category)
        {
            return equipmentRules != null
                && equipmentRules.IsSlotBlocked(equippedItems, category);
        }

        /// <summary>Надетые предметы, которые применяются к персонажу: предметы заблокированных слотов сохранены, но не включены.</summary>
        public IReadOnlyDictionary<int, string> GetActiveEquipment()
        {
            return equipmentRules != null
                ? equipmentRules.GetActiveEquipment(equippedItems)
                : new Dictionary<int, string>(equippedItems);
        }

        /// <summary>Цена для показа игроку: число монет или цена платформы (пусто, если цену ещё не получили).</summary>
        public string GetDisplayPrice(string itemId)
        {
            if (!itemsById.TryGetValue(itemId, out var item))
            {
                return string.Empty;
            }

            if (item.AcquisitionKind == StoreAcquisitionKind.Coins)
            {
                return item.CoinPrice.ToString();
            }

            if (!string.IsNullOrWhiteSpace(item.PlatformProductId)
                && platformProductsById.TryGetValue(
                    item.PlatformProductId,
                    out var product))
            {
                return product.DisplayPrice;
            }

            return string.Empty;
        }

        /// <summary>Адрес значка валюты платформы для товара (пусто, если не нужен или ещё не получен).</summary>
        public string GetCurrencyImageUrl(string itemId)
        {
            if (itemsById.TryGetValue(itemId, out var item)
                && item.AcquisitionKind != StoreAcquisitionKind.Coins
                && !string.IsNullOrWhiteSpace(item.PlatformProductId)
                && platformProductsById.TryGetValue(item.PlatformProductId, out var product))
            {
                return product.CurrencyImageUrl;
            }

            return string.Empty;
        }

        /// <summary>Товар можно купить прямо сейчас: за монеты или платформа вернула его в каталоге.</summary>
        public bool IsAvailableForPurchase(string itemId)
        {
            if (!itemsById.TryGetValue(itemId, out var item))
            {
                return false;
            }

            return item.CanBuyWithCoins || IsPlatformAvailable(itemId);
        }

        /// <summary>Платформа вернула товар в своём каталоге (после <see cref="RestoreAsync"/>).</summary>
        public bool IsPlatformAvailable(string itemId)
        {
            return itemsById.TryGetValue(itemId, out var item)
                && item.AcquisitionKind == StoreAcquisitionKind.Platform
                && !string.IsNullOrWhiteSpace(item.PlatformProductId)
                && platformProductsById.ContainsKey(item.PlatformProductId);
        }

        /// <summary>Купленные товары для записи в прогресс, включая убранные из каталога.</summary>
        public IReadOnlyList<string> GetOwnedItemIds()
        {
            var result = new List<string>(ownedItems);
            result.AddRange(retainedUnknownItems);
            return result;
        }

        /// <summary>Токены уже обработанных покупок для записи в прогресс.</summary>
        public IReadOnlyList<string> GetProcessedPurchaseTokens()
        {
            return new List<string>(processedPurchaseTokens);
        }

        /// <summary>Надетые предметы для записи в прогресс: категория → идентификатор.</summary>
        public IReadOnlyDictionary<int, string> GetEquippedItems()
        {
            return new Dictionary<int, string>(equippedItems);
        }

        /// <summary>
        /// Восстанавливает состояние магазина из прогресса. Предметы по умолчанию выдаются всегда.
        /// Неизвестные идентификаторы сохраняются, но не используются. Пакеты валюты предметами не владеют.
        /// Надетое, которое не купили, не подходит по категории или неизвестно, возвращается к предмету по умолчанию (или пусто).
        /// </summary>
        public void RestoreProgress(
            IEnumerable<string> restoredOwnedItems,
            IEnumerable<string> restoredPurchaseTokens,
            IReadOnlyDictionary<int, string> restoredEquipment)
        {
            ownedItems.Clear();
            retainedUnknownItems.Clear();
            processedPurchaseTokens.Clear();
            equippedItems.Clear();

            GrantDefaults();

            if (restoredOwnedItems != null)
            {
                foreach (var itemId in restoredOwnedItems)
                {
                    if (itemsById.TryGetValue(itemId ?? string.Empty, out var item))
                    {
                        if (item.Kind != StoreItemKind.CurrencyPack)
                        {
                            ownedItems.Add(item.Id);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(itemId))
                    {
                        retainedUnknownItems.Add(itemId);
                    }
                }
            }

            if (restoredPurchaseTokens != null)
            {
                foreach (var token in restoredPurchaseTokens)
                {
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        processedPurchaseTokens.Add(token);
                    }
                }
            }

            if (restoredEquipment != null)
            {
                foreach (var pair in restoredEquipment)
                {
                    if (itemsById.TryGetValue(pair.Value ?? string.Empty, out var item)
                        && item.Category == pair.Key
                        && item.IsEquippable
                        && ownedItems.Contains(item.Id))
                    {
                        equippedItems[pair.Key] = item.Id;
                    }
                }
            }

            foreach (var pair in options.DefaultItems)
            {
                EnsureDefaultEquipped(pair.Key, pair.Value);
            }

            Changed?.Invoke();
        }

        /// <summary>Покупка за монеты. Возвращает true, если товар теперь у игрока (куплен сейчас или уже был).</summary>
        public bool TryBuyWithCoins(string itemId)
        {
            if (!itemsById.TryGetValue(itemId, out var item)
                || !item.CanBuyWithCoins)
            {
                PublishStatus(StoreStatus.NotForCoins);
                return false;
            }

            if (ownedItems.Contains(item.Id))
            {
                Equip(item);
                PublishStatus(StoreStatus.AlreadyOwned);
                return true;
            }

            if (economyService == null
                || !economyService.TrySpend("store_soft:" + item.Id, item.CoinPrice, "store_item"))
            {
                PublishStatus(StoreStatus.InsufficientCoins);
                return false;
            }

            ownedItems.Add(item.Id);
            Equip(item);
            PublishStatus(StoreStatus.Success);
            Changed?.Invoke();
            RequestCriticalSave();
            return true;
        }

        /// <summary>
        /// Надеть купленный предмет. Для необязательных категорий повторный вызов с уже надетым предметом снимает его.
        /// Возвращает false, если предмета нет, он не куплен или не экипируемый.
        /// </summary>
        public bool TryEquip(string itemId)
        {
            if (!itemsById.TryGetValue(itemId, out var item)
                || !ownedItems.Contains(itemId)
                || !item.IsEquippable)
            {
                return false;
            }

            if (options.OptionalCategories.Contains(item.Category)
                && equippedItems.TryGetValue(item.Category, out var equippedItemId)
                && equippedItemId == item.Id)
            {
                equippedItems.Remove(item.Category);
                PublishStatus(StoreStatus.AccessoryRemoved);
                Changed?.Invoke();
                return true;
            }

            Equip(item);
            PublishStatus(StoreStatus.OutfitUpdated);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Покупка за реальные деньги через платформу. Итог сообщается через <see cref="StatusChanged"/>.</summary>
        public async Task PurchaseAsync(string itemId)
        {
            if (!itemsById.TryGetValue(itemId, out var item)
                || item.AcquisitionKind != StoreAcquisitionKind.Platform
                || purchasesService == null
                || !platformProductsById.ContainsKey(item.PlatformProductId))
            {
                PublishStatus(StoreStatus.Unavailable);
                return;
            }

            PublishStatus(StoreStatus.InProgress);
            var result = await purchasesService.PurchaseAsync(item.PlatformProductId);
            if (!result.IsSuccess || result.Value == null)
            {
                PublishStatus(StoreStatus.Canceled);
                return;
            }

            await ProcessReceiptAsync(result.Value);
        }

        /// <summary>
        /// Загружает каталог платформы (цены) и обрабатывает невыданные покупки. Вызывать при открытии магазина
        /// и при старте игры, чтобы покупка, прерванная закрытием игры, всё равно была выдана.
        /// </summary>
        public async Task RestoreAsync()
        {
            if (purchasesService == null)
            {
                return;
            }

            var catalogResult = await purchasesService.GetCatalogAsync();
            if (catalogResult.IsSuccess && catalogResult.Value != null)
            {
                platformProductsById.Clear();
                foreach (var product in catalogResult.Value)
                {
                    if (product != null && !string.IsNullOrWhiteSpace(product.Id))
                    {
                        platformProductsById[product.Id] = product;
                    }
                }

                Changed?.Invoke();
            }
            else
            {
                PublishStatus(StoreStatus.Unavailable);
            }

            var result = await purchasesService.RestorePurchasesAsync();
            if (!result.IsSuccess || result.Value == null)
            {
                return;
            }

            foreach (var receipt in result.Value)
            {
                // Убранные из каталога товары платформа может ещё возвращать. Они ничего не выдают
                // и не должны подменять статус магазина ошибкой.
                if (receipt != null
                    && !itemsByProductId.ContainsKey(receipt.ProductId ?? string.Empty))
                {
                    continue;
                }

                await ProcessReceiptAsync(receipt);
            }
        }

        private async Task ProcessReceiptAsync(PurchaseReceipt receipt)
        {
            if (receipt == null
                || string.IsNullOrWhiteSpace(receipt.Token))
            {
                PublishStatus(StoreStatus.Invalid);
                return;
            }

            if (!itemsByProductId.TryGetValue(receipt.ProductId, out var item))
            {
                PublishStatus(StoreStatus.UnknownProduct);
                return;
            }

            if (processedPurchaseTokens.Contains(receipt.Token))
            {
                // Покупку уже выдавали, но запись прогресса могла не удаться. Повторяем запись,
                // и только после неё подтверждаем покупку платформе.
                if (criticalSaveHandler == null || !await criticalSaveHandler())
                {
                    PublishStatus(StoreStatus.SaveRetry);
                    return;
                }

                if (item.Kind == StoreItemKind.CurrencyPack && !receipt.IsConsumed)
                {
                    var consumeResult = await purchasesService.ConsumeAsync(receipt.Token);
                    PublishStatus(consumeResult.IsSuccess
                        ? StoreStatus.AlreadyProcessed
                        : StoreStatus.ConsumeRetry);
                    return;
                }

                PublishStatus(StoreStatus.AlreadyProcessed);
                return;
            }

            var granted = false;
            if (item.Kind == StoreItemKind.CurrencyPack)
            {
                // Идентификатор транзакции из токена: экономика не зачислит монеты дважды в одной сессии.
                granted = economyService != null
                    && economyService.TryCredit(
                        "purchase:" + receipt.Token,
                        item.GrantedCoins,
                        "platform_purchase");
            }
            else
            {
                granted = ownedItems.Add(item.Id) || ownedItems.Contains(item.Id);
                if (item.IsEquippable)
                {
                    Equip(item);
                }
            }

            if (!granted)
            {
                PublishStatus(StoreStatus.NotGranted);
                return;
            }

            // Токен помечаем обработанным только после выдачи, чтобы невыданную покупку можно было повторить.
            processedPurchaseTokens.Add(receipt.Token);
            Changed?.Invoke();
            if (criticalSaveHandler == null || !await criticalSaveHandler())
            {
                PublishStatus(StoreStatus.GrantedSaveRetry);
                return;
            }

            PublishStatus(StoreStatus.Success);

            // Постоянные товары на платформе не гасятся, гасятся только пакеты валюты.
            if (item.Kind == StoreItemKind.CurrencyPack && !receipt.IsConsumed)
            {
                var consumeResult = await purchasesService.ConsumeAsync(receipt.Token);
                if (!consumeResult.IsSuccess)
                {
                    PublishStatus(StoreStatus.ConsumeRetry);
                }
            }
        }

        private void GrantDefaults()
        {
            foreach (var pair in options.DefaultItems)
            {
                ownedItems.Add(pair.Value);
                equippedItems[pair.Key] = pair.Value;
            }
        }

        private void EnsureDefaultEquipped(int category, string defaultItemId)
        {
            if (!equippedItems.ContainsKey(category))
            {
                equippedItems[category] = defaultItemId;
            }
        }

        private void Equip(StoreItemDefinition item)
        {
            if (item.IsEquippable)
            {
                equippedItems[item.Category] = item.Id;
            }
        }

        private void PublishStatus(StoreStatus status)
        {
            StatusChanged?.Invoke(status);
        }

        private async void RequestCriticalSave()
        {
            if (criticalSaveHandler != null)
            {
                await criticalSaveHandler();
            }
        }
    }
}
