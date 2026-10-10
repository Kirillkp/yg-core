// Образец подключения модуля Store: маленький каталог игры со скинами, шапками, пакетом монет и разблокировкой.
// Этот же код приведён в Documentation~/modules/store.md. Он компилируется и проверяется в StoreChecks.cs,
// поэтому образец в документации всегда рабочий.
using System;
using System.Collections.Generic;
using YgCore.Save;
using YgCore.Store;

// Категории задаёт игра. Эти числа сохраняются в прогрессе игрока, после выпуска игры их менять нельзя.
public enum SampleCategory
{
    Skin = 0,
    Hat = 1,
    Pack = 2
}

public static class SampleStore
{
    public const string DefaultSkinId = "skin_default";

    // Каталог товаров игры.
    public static IReadOnlyList<StoreItemDefinition> CreateCatalog()
    {
        return new List<StoreItemDefinition>
        {
            // Базовый скин: бесплатный, выдаётся всем (см. DefaultItems ниже).
            StoreItemDefinition.Equippable(DefaultSkinId, "store.item.skin_default", (int)SampleCategory.Skin, StoreAcquisitionKind.Coins),

            // Скины и шапки за монеты.
            StoreItemDefinition.Equippable("skin_pirate", "store.item.skin_pirate", (int)SampleCategory.Skin, StoreAcquisitionKind.Coins, 100, rarity: StoreRarity.Rare),
            StoreItemDefinition.Equippable("hat_red", "store.item.hat_red", (int)SampleCategory.Hat, StoreAcquisitionKind.Coins, 50),
            StoreItemDefinition.Equippable("hat_blue", "store.item.hat_blue", (int)SampleCategory.Hat, StoreAcquisitionKind.Coins, 50),

            // Легендарный скин: продаётся за реальные деньги, а также за монеты.
            StoreItemDefinition.Equippable("skin_king", "store.item.skin_king", (int)SampleCategory.Skin, StoreAcquisitionKind.Platform, 500, "skin_king", StoreRarity.Legendary),

            // Пакет монет за реальные деньги (много раз): после покупки зачисляет 1000 монет.
            StoreItemDefinition.CurrencyPack("pack_small", "store.item.pack_small", (int)SampleCategory.Pack, "coins_small", 1000),

            // Разовая разблокировка за реальные деньги без экипировки.
            StoreItemDefinition.Unlock("vip", "store.item.vip", (int)SampleCategory.Pack, "vip"),
        };
    }

    // Настройки: базовый скин у всех сразу, шапку можно снять.
    public static StoreOptions CreateOptions()
    {
        return new StoreOptions
        {
            DefaultItems = new Dictionary<int, string> { [(int)SampleCategory.Skin] = DefaultSkinId },
            OptionalCategories = new HashSet<int> { (int)SampleCategory.Hat }
        };
    }
}

// Часть данных прогресса игры, которая относится к магазину. Игра включает эти поля в свой класс данных
// (наследник ProgressionData из модуля Save), см. modules/save.md.
[Serializable]
public sealed class SampleStoreData
{
    public List<string> ownedItemIds = new List<string>();
    public List<string> processedPurchaseTokens = new List<string>();
    public List<EquippedItemProgressData> equippedItems = new List<EquippedItemProgressData>();
}

public static class SampleStoreProgress
{
    // Для записи: собрать состояние магазина.
    public static SampleStoreData Capture(SessionStoreService store)
    {
        return new SampleStoreData
        {
            ownedItemIds = new List<string>(store.GetOwnedItemIds()),
            processedPurchaseTokens = new List<string>(store.GetProcessedPurchaseTokens()),
            equippedItems = EquippedItemProgressData.FromDictionary(store.GetEquippedItems())
        };
    }

    // Для Normalize схемы: убрать пустое и повторы. Идентификаторы, которых нет в каталоге, не трогаем:
    // игрок мог ими владеть, магазин их просто проигнорирует.
    public static void Normalize(SampleStoreData data)
    {
        data.ownedItemIds = ProgressionLists.Distinct(data.ownedItemIds);
        data.processedPurchaseTokens = ProgressionLists.Distinct(data.processedPurchaseTokens);
        data.equippedItems = EquippedItemProgressData.Normalize(data.equippedItems);
    }

    // Для загрузки: передать сохранённое в магазин.
    public static void Apply(SessionStoreService store, SampleStoreData data)
    {
        store.RestoreProgress(
            data.ownedItemIds,
            data.processedPurchaseTokens,
            EquippedItemProgressData.ToDictionary(data.equippedItems));
    }
}
