namespace YgCore.Store
{
    /// <summary>
    /// Описание товара каталога. Каталог (список таких описаний) составляет игра.
    ///
    /// Категория (<see cref="Category"/>) это число, которое игра задаёт сама, обычно через свой enum:
    /// «персонаж», «голова» и так далее. Для экипируемых товаров категория же и слот: в каждый слот надевается
    /// один предмет. Это число сохраняется в прогрессе игрока, поэтому после выпуска игры значения категорий менять нельзя.
    /// </summary>
    public sealed class StoreItemDefinition
    {
        public StoreItemDefinition(
            string id,
            string titleKey,
            StoreItemKind kind,
            int category,
            StoreAcquisitionKind acquisitionKind,
            int coinPrice = 0,
            string platformProductId = null,
            int grantedCoins = 0,
            StoreRarity rarity = StoreRarity.Common)
        {
            Id = id;
            TitleKey = titleKey;
            Kind = kind;
            Category = category;
            AcquisitionKind = acquisitionKind;
            CoinPrice = coinPrice;
            PlatformProductId = platformProductId;
            GrantedCoins = grantedCoins;
            Rarity = rarity;
        }

        /// <summary>Экипируемый товар (скин, шапка и т.п.).</summary>
        public static StoreItemDefinition Equippable(
            string id,
            string titleKey,
            int category,
            StoreAcquisitionKind acquisitionKind,
            int coinPrice = 0,
            string platformProductId = null,
            StoreRarity rarity = StoreRarity.Common)
        {
            return new StoreItemDefinition(
                id, titleKey, StoreItemKind.Equippable, category, acquisitionKind,
                coinPrice, platformProductId, 0, rarity);
        }

        /// <summary>Пакет валюты за реальные деньги: после покупки зачисляет монеты.</summary>
        public static StoreItemDefinition CurrencyPack(
            string id,
            string titleKey,
            int category,
            string platformProductId,
            int grantedCoins,
            StoreRarity rarity = StoreRarity.Common)
        {
            return new StoreItemDefinition(
                id, titleKey, StoreItemKind.CurrencyPack, category, StoreAcquisitionKind.Platform,
                0, platformProductId, grantedCoins, rarity);
        }

        /// <summary>Разовая разблокировка за реальные деньги без экипировки.</summary>
        public static StoreItemDefinition Unlock(
            string id,
            string titleKey,
            int category,
            string platformProductId,
            StoreRarity rarity = StoreRarity.Common)
        {
            return new StoreItemDefinition(
                id, titleKey, StoreItemKind.Unlock, category, StoreAcquisitionKind.Platform,
                0, platformProductId, 0, rarity);
        }

        /// <summary>Уникальный идентификатор товара. После выпуска игры менять нельзя (он хранится в прогрессе).</summary>
        public string Id { get; }

        /// <summary>Ключ строки с названием товара.</summary>
        public string TitleKey { get; }

        public StoreItemKind Kind { get; }

        /// <summary>Категория: для экипируемых товаров ещё и слот экипировки.</summary>
        public int Category { get; }

        public StoreAcquisitionKind AcquisitionKind { get; }

        /// <summary>Цена в монетах (0, если за монеты не продаётся).</summary>
        public int CoinPrice { get; }

        /// <summary>Идентификатор товара в консоли платформы (для товаров за реальные деньги).</summary>
        public string PlatformProductId { get; }

        /// <summary>Сколько монет зачисляется за пакет валюты.</summary>
        public int GrantedCoins { get; }

        public StoreRarity Rarity { get; }

        /// <summary>Можно надеть в слот своей категории.</summary>
        public bool IsEquippable => Kind == StoreItemKind.Equippable;

        /// <summary>
        /// Можно купить за монеты: товары за монеты, а также экипируемые товары за реальные деньги,
        /// у которых задана и цена в монетах (например, легендарные скины).
        /// </summary>
        public bool CanBuyWithCoins => AcquisitionKind == StoreAcquisitionKind.Coins
            || (AcquisitionKind == StoreAcquisitionKind.Platform && CoinPrice > 0 && IsEquippable);

        /// <summary>Продаётся и за реальные деньги, и за монеты.</summary>
        public bool HasCoinAlternative => AcquisitionKind == StoreAcquisitionKind.Platform && CanBuyWithCoins;
    }
}
