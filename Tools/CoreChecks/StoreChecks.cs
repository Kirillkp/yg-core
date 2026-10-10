// Проверки модуля Store без Unity: покупка за монеты, экипировка, восстановление из сохранения и,
// главное, покупки за реальные деньги: выдача в два шага, защита от двойной выдачи, повтор после сбоя записи.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YgCore.Contracts;
using YgCore.Core;
using YgCore.Mocks;
using YgCore.Store;

static class StoreChecks
{
    // Обёртка над моком покупок: записывает порядок вызовов, чтобы проверить «сначала запись, потом подтверждение».
    private sealed class RecordingPurchases : IPurchasesService
    {
        public readonly EditorMockPurchasesService Inner;
        public readonly List<string> Log;

        public RecordingPurchases(EditorMockPurchasesService inner, List<string> log)
        {
            Inner = inner;
            Log = log;
        }

        public bool IsPlatformAvailable => Inner.IsPlatformAvailable;
        public Task<PlatformServiceResult<List<PurchaseProduct>>> GetCatalogAsync() => Inner.GetCatalogAsync();
        public Task<PlatformServiceResult<PurchaseReceipt>> PurchaseAsync(string productId) => Inner.PurchaseAsync(productId);
        public Task<PlatformServiceResult<List<PurchaseReceipt>>> RestorePurchasesAsync() => Inner.RestorePurchasesAsync();

        public Task<PlatformServiceResult> ConsumeAsync(string purchaseToken)
        {
            Log.Add("consume");
            return Inner.ConsumeAsync(purchaseToken);
        }
    }

    private sealed class Rig
    {
        public readonly SessionEconomyService Economy = new SessionEconomyService();
        public readonly EditorMockPurchasesService Mock;
        public readonly List<string> Log = new List<string>();
        public readonly List<StoreStatus> Statuses = new List<StoreStatus>();
        public readonly SessionStoreService Store;
        public bool SaveSucceeds = true;
        public int ChangedCount;

        public Rig(IReadOnlyList<StoreItemDefinition> catalog = null, bool withPurchases = true)
        {
            Mock = new EditorMockPurchasesService(new[]
            {
                new PurchaseProduct("skin_king", "King", "299", "YAN", false),
                new PurchaseProduct("coins_small", "Small pack", "49", "YAN", true, "https://img/yan.png"),
                new PurchaseProduct("vip", "VIP", "199", "YAN", false)
            });
            Store = new SessionStoreService(
                Economy,
                withPurchases ? new RecordingPurchases(Mock, Log) : null,
                catalog ?? SampleStore.CreateCatalog(),
                SampleStore.CreateOptions());
            Store.StatusChanged += s => Statuses.Add(s);
            Store.Changed += () => ChangedCount++;
            Store.SetCriticalSaveHandler(() =>
            {
                Log.Add("save");
                return Task.FromResult(SaveSucceeds);
            });
        }

        public StoreStatus Last => Statuses.Count > 0 ? Statuses[Statuses.Count - 1] : (StoreStatus)(-1);

        public void Coins(int amount) => Economy.TryCredit("seed" + Guid.NewGuid(), amount, "test");
    }

    public static void Run(Action<string, bool> check)
    {
        var skin = (int)SampleCategory.Skin;
        var hat = (int)SampleCategory.Hat;

        // --- Старт и товары по умолчанию
        var r = new Rig();
        check("магазин: базовый скин выдан и надет сразу", r.Store.IsOwned("skin_default") && r.Store.IsEquipped("skin_default") && r.Store.GetEquipped(skin) == "skin_default");
        check("магазин: у категории без предмета пустая строка", r.Store.GetEquipped(hat) == "");

        // --- Покупка за монеты
        r = new Rig();
        check("монеты: не хватает -> InsufficientCoins, товар не куплен", !r.Store.TryBuyWithCoins("skin_pirate") && r.Last == StoreStatus.InsufficientCoins && !r.Store.IsOwned("skin_pirate"));
        r.Coins(250);
        var logBefore = r.Log.Count;
        check("монеты: покупка списывает цену, выдаёт и надевает", r.Store.TryBuyWithCoins("skin_pirate") && r.Economy.Balance == 150
            && r.Store.IsOwned("skin_pirate") && r.Store.IsEquipped("skin_pirate") && r.Last == StoreStatus.Success);
        check("монеты: после покупки прогресс записывается", r.Log.Skip(logBefore).Contains("save"));
        check("монеты: повторная покупка -> AlreadyOwned без списания", r.Store.TryBuyWithCoins("skin_pirate") && r.Economy.Balance == 150 && r.Last == StoreStatus.AlreadyOwned);
        check("монеты: товар за реальные деньги без цены в монетах -> NotForCoins", !r.Store.TryBuyWithCoins("vip") && r.Last == StoreStatus.NotForCoins);
        check("монеты: пакет валюты за монеты не купить", !r.Store.TryBuyWithCoins("pack_small") && r.Last == StoreStatus.NotForCoins);
        check("монеты: неизвестный товар -> NotForCoins", !r.Store.TryBuyWithCoins("нет-такого") && r.Last == StoreStatus.NotForCoins);
        var king = r.Store.Catalog.First(i => i.Id == "skin_king");
        check("монеты: экипируемый товар платформы с ценой в монетах продаётся и за монеты", king.CanBuyWithCoins && king.HasCoinAlternative);
        r.Coins(1000);
        check("монеты: легендарный скин за монеты", r.Store.TryBuyWithCoins("skin_king") && r.Store.IsEquipped("skin_king"));

        // --- Экипировка
        r = new Rig();
        r.Coins(500);
        check("экипировка: непокупленное надеть нельзя", !r.Store.TryEquip("hat_red"));
        r.Store.TryBuyWithCoins("hat_red");
        check("экипировка: купленная шапка надета", r.Store.GetEquipped(hat) == "hat_red");
        check("экипировка: повторное надевание шапки снимает её (необязательная категория)", r.Store.TryEquip("hat_red") && r.Store.GetEquipped(hat) == "" && r.Last == StoreStatus.AccessoryRemoved);
        check("экипировка: снова надеть", r.Store.TryEquip("hat_red") && r.Store.GetEquipped(hat) == "hat_red" && r.Last == StoreStatus.OutfitUpdated);
        r.Store.TryBuyWithCoins("skin_pirate");
        check("экипировка: скин (обязательная категория) повторным надеванием не снимается", r.Store.TryEquip("skin_pirate") && r.Store.GetEquipped(skin) == "skin_pirate");
        check("экипировка: можно вернуть базовый скин", r.Store.TryEquip("skin_default") && r.Store.GetEquipped(skin) == "skin_default");

        // --- Блокировка слотов
        r = new Rig();
        r.Coins(2000);
        r.Store.TryBuyWithCoins("hat_red");
        r.Store.TryBuyWithCoins("skin_king");
        var changedBefore = r.ChangedCount;
        r.Store.SetEquipmentRules(new StoreSlotBlockingRules(new Dictionary<string, int[]> { ["skin_king"] = new[] { hat } }));
        var active = r.Store.GetActiveEquipment();
        check("блокировка: костюм блокирует слот шапки", r.Store.IsSlotBlocked(hat) && !r.Store.IsSlotBlocked(skin) && r.Store.IsBlockedByEquipment("hat_red"));
        check("блокировка: заблокированный слот сохранён, но не применяется", r.Store.GetEquipped(hat) == "hat_red" && !active.ContainsKey(hat) && active[skin] == "skin_king");
        check("блокировка: смена правил сообщает об изменении", r.ChangedCount == changedBefore + 1);
        check("блокировка: без правил все надетые предметы активны", new Rig().Store.GetActiveEquipment().ContainsKey(skin));

        // --- Восстановление из сохранения
        r = new Rig();
        r.Store.RestoreProgress(
            new[] { "skin_pirate", "ушёл_из_каталога", "pack_small", null, " " },
            new[] { "t1", " ", null },
            new Dictionary<int, string> { [skin] = "skin_pirate", [hat] = "hat_red", [99] = "skin_pirate" });
        var owned = r.Store.GetOwnedItemIds();
        check("восстановление: купленное и неизвестное сохраняются, пакет валюты не считается купленным",
            r.Store.IsOwned("skin_pirate") && r.Store.IsOwned("skin_default") && owned.Contains("ушёл_из_каталога") && !r.Store.IsOwned("pack_small") && !owned.Contains("pack_small"));
        check("восстановление: токены без пустых", r.Store.GetProcessedPurchaseTokens().SequenceEqual(new[] { "t1" }));
        check("восстановление: непокупленное и не по категории не надевается", r.Store.GetEquipped(skin) == "skin_pirate" && r.Store.GetEquipped(hat) == "" && !r.Store.GetEquippedItems().ContainsKey(99));
        r.Store.RestoreProgress(null, null, null);
        check("восстановление: пустой прогресс возвращает базовый скин", r.Store.GetEquipped(skin) == "skin_default" && !r.Store.IsOwned("skin_pirate"));
        r.Store.RestoreProgress(new[] { "skin_pirate" }, null, new Dictionary<int, string> { [skin] = "hat_red" });
        check("восстановление: предмет чужой категории в слоте заменяется базовым", r.Store.GetEquipped(skin) == "skin_default");

        // --- Запись и чтение состояния магазина через данные прогресса
        r = new Rig();
        r.Coins(1000);
        r.Store.TryBuyWithCoins("skin_pirate");
        r.Store.TryBuyWithCoins("hat_blue");
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        r.Store.PurchaseAsync("pack_small").GetAwaiter().GetResult();
        var saved = SampleStoreProgress.Capture(r.Store);
        var json = UnityEngine.JsonUtility.ToJson(saved);
        var loaded = UnityEngine.JsonUtility.FromJson<SampleStoreData>(json);
        SampleStoreProgress.Normalize(loaded);
        var other = new Rig();
        SampleStoreProgress.Apply(other.Store, loaded);
        check("прогресс магазина: запись и чтение через JSON сохраняют купленное, токены и надетое",
            other.Store.IsOwned("skin_pirate") && other.Store.IsOwned("hat_blue") && other.Store.GetEquipped(skin) == "skin_pirate" && other.Store.GetEquipped(hat) == "hat_blue"
            && other.Store.GetProcessedPurchaseTokens().Count == 1 && json.Contains("\"category\"") && json.Contains("\"itemId\""));
        var messy = new List<EquippedItemProgressData>
        {
            null, new EquippedItemProgressData(5, "b"), new EquippedItemProgressData(5, "c"), new EquippedItemProgressData(1, " "), new EquippedItemProgressData(2, "a")
        };
        var cleaned = EquippedItemProgressData.Normalize(messy);
        check("прогресс магазина: Normalize убирает null, пустые и повторы категорий, сортирует", cleaned.Count == 2 && cleaned[0].category == 2 && cleaned[1].category == 5 && cleaned[1].itemId == "b");

        // --- Покупки за реальные деньги
        r = new Rig();
        r.Store.PurchaseAsync("skin_king").GetAwaiter().GetResult();
        check("платформа: до загрузки каталога платформы покупка недоступна", r.Last == StoreStatus.Unavailable && !r.Store.IsOwned("skin_king"));
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        check("платформа: после загрузки каталога товар доступен, цена и значок валюты известны",
            r.Store.IsPlatformAvailable("skin_king") && r.Store.GetDisplayPrice("skin_king") == "299 YAN" && r.Store.GetCurrencyImageUrl("pack_small") == "https://img/yan.png"
            && r.Store.GetDisplayPrice("skin_pirate") == "100" && r.Store.IsAvailableForPurchase("skin_pirate") && !r.Store.IsAvailableForPurchase("нет"));

        r.Log.Clear();
        r.Store.PurchaseAsync("pack_small").GetAwaiter().GetResult();
        var token1 = r.Store.GetProcessedPurchaseTokens().FirstOrDefault();
        check("пакет валюты: монеты зачислены, токен запомнен, статус Success", r.Economy.Balance == 1000 && token1 != null && r.Last == StoreStatus.Success);
        check("пакет валюты: СНАЧАЛА запись прогресса, ПОТОМ подтверждение платформе", r.Log.SequenceEqual(new[] { "save", "consume" }) && r.Mock.WasConsumed(token1));
        check("пакет валюты: пакет не становится предметом", !r.Store.IsOwned("pack_small"));

        r.Log.Clear();
        r.Store.PurchaseAsync("skin_king").GetAwaiter().GetResult();
        var token2 = r.Store.GetProcessedPurchaseTokens().First(t => t != token1);
        check("постоянный товар: выдан и надет", r.Store.IsOwned("skin_king") && r.Store.IsEquipped("skin_king"));
        check("постоянный товар: платформе не подтверждается (не гасится)", r.Log.SequenceEqual(new[] { "save" }) && !r.Mock.WasConsumed(token2));

        r.Store.PurchaseAsync("vip").GetAwaiter().GetResult();
        check("разблокировка: куплена, не экипируется, не гасится", r.Store.IsOwned("vip") && !r.Store.IsEquipped("vip") && !r.Store.TryEquip("vip"));

        // Повторный токен не выдаёт дважды
        r = new Rig();
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        r.Mock.ReturnDuplicateToken = true;
        r.Store.PurchaseAsync("pack_small").GetAwaiter().GetResult();
        r.Store.PurchaseAsync("pack_small").GetAwaiter().GetResult();
        check("защита: тот же токен второй раз не выдаётся", r.Economy.Balance == 1000 && r.Last == StoreStatus.AlreadyProcessed);

        // Сбой записи прогресса после выдачи
        r = new Rig();
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        r.SaveSucceeds = false;
        r.Log.Clear();
        r.Store.PurchaseAsync("pack_small").GetAwaiter().GetResult();
        var failedToken = r.Store.GetProcessedPurchaseTokens().FirstOrDefault();
        check("сбой записи: выдано, но статус GrantedSaveRetry и покупка НЕ подтверждена платформе",
            r.Economy.Balance == 1000 && r.Last == StoreStatus.GrantedSaveRetry && failedToken != null && !r.Mock.WasConsumed(failedToken) && !r.Log.Contains("consume"));
        // После перезапуска платформа возвращает эту же невыданную покупку
        r.SaveSucceeds = true;
        r.Mock.AddPendingPurchase("coins_small", failedToken);
        r.Log.Clear();
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        check("сбой записи: при повторе запись удаётся, покупка подтверждается, монеты не удваиваются",
            r.Economy.Balance == 1000 && r.Mock.WasConsumed(failedToken) && r.Last == StoreStatus.AlreadyProcessed && r.Log.SequenceEqual(new[] { "save", "consume" }));

        // Без обработчика записи покупка не завершается
        r = new Rig();
        r.Store.SetCriticalSaveHandler(null);
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        r.Store.PurchaseAsync("pack_small").GetAwaiter().GetResult();
        check("без обработчика записи: GrantedSaveRetry и подтверждения нет", r.Last == StoreStatus.GrantedSaveRetry && r.Log.Count == 0);

        // Отмена
        r = new Rig();
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        r.Mock.Scenario = MockPlatformScenario.Denied;
        r.Store.PurchaseAsync("skin_king").GetAwaiter().GetResult();
        check("отмена: платформа отказала -> Canceled, ничего не выдано", r.Last == StoreStatus.Canceled && !r.Store.IsOwned("skin_king") && r.Store.GetProcessedPurchaseTokens().Count == 0);

        // Невыданные покупки при старте
        r = new Rig();
        r.Mock.AddPendingPurchase("coins_small", "tok-pending");
        r.Mock.AddPendingPurchase("убранный_товар", "tok-old");
        r.Statuses.Clear();
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        check("невыданные: покупка пакета выдана и подтверждена", r.Economy.Balance == 1000 && r.Mock.WasConsumed("tok-pending") && r.Store.GetProcessedPurchaseTokens().Contains("tok-pending"));
        check("невыданные: товар, убранный из каталога, молча пропущен", !r.Statuses.Contains(StoreStatus.UnknownProduct) && !r.Store.GetProcessedPurchaseTokens().Contains("tok-old"));

        r = new Rig();
        r.Mock.AddPendingPurchase("coins_small", "");
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        check("невыданные: квитанция без токена -> Invalid, монеты не зачислены", r.Last == StoreStatus.Invalid && r.Economy.Balance == 0);

        // Выдать нечего
        var zeroCatalog = new List<StoreItemDefinition>(SampleStore.CreateCatalog())
        {
            StoreItemDefinition.CurrencyPack("pack_zero", "store.item.pack_zero", (int)SampleCategory.Pack, "coins_zero", 0)
        };
        r = new Rig(zeroCatalog);
        r.Mock.Catalog.Add(new PurchaseProduct("coins_zero", "Zero", "1", "YAN", true));
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        r.Store.PurchaseAsync("pack_zero").GetAwaiter().GetResult();
        check("выдать нечего: NotGranted и токен не помечен обработанным", r.Last == StoreStatus.NotGranted && r.Store.GetProcessedPurchaseTokens().Count == 0);

        // Платформа недоступна
        r = new Rig();
        r.Mock.IsPlatformAvailable = false;
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        check("платформа недоступна: статус Unavailable, товары за реальные деньги недоступны", r.Statuses.Contains(StoreStatus.Unavailable) && !r.Store.IsPlatformAvailable("skin_king"));
        r = new Rig(withPurchases: false);
        r.Store.RestoreAsync().GetAwaiter().GetResult();
        r.Store.PurchaseAsync("skin_king").GetAwaiter().GetResult();
        check("без сервиса покупок: восстановление ничего не делает, покупка Unavailable", r.Last == StoreStatus.Unavailable);

        // --- Ключи статусов совпадают с ключами «Обби Потоп»
        var expectedKeys = new[]
        {
            "store.status.success", "store.status.coins_only", "store.status.already_owned", "store.status.insufficient_coins",
            "store.status.accessory_removed", "store.status.outfit_updated", "store.status.unavailable", "store.status.in_progress",
            "store.status.canceled", "store.status.invalid", "store.status.unknown_product", "store.status.save_retry",
            "store.status.already_processed", "store.status.consume_retry", "store.status.not_granted", "store.status.granted_save_retry"
        };
        var keys = Enum.GetValues(typeof(StoreStatus)).Cast<StoreStatus>().Select(StoreStatusKeys.ToKey).ToList();
        check("ключи статусов: 16 разных, совпадают с «Обби Потоп»", keys.Count == 16 && keys.Distinct().Count() == 16 && expectedKeys.All(keys.Contains));
    }
}
