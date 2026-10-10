# Модуль: Магазин

Сборка: `YgCore.Store` · Пространство имён: `YgCore.Store` · С версии: `v0.1.0` · Статус: перенесён из «Обби Потоп», проверен вне Unity; покупка, возврат невыданной и гашение через Яндекс проверены на стенде (сам `SessionStoreService` на живой платформе стенд не гонял)

Зависит от `YgCore.Core` и `YgCore.Contracts`. Только логика, **экранов в модуле нет** (карточки, окна, превью живут в игре и в шаблонах).

## Зачем нужен

Любая игра с покупками решает одни и те же задачи: купить за монеты, купить за реальные деньги через платформу, надеть купленное, не выдать одну и ту же покупку дважды, не потерять покупку, если игра закрылась на полпути. Модуль решает их один раз. Каталог товаров и цены остаются в игре.

## Что делает пакет и что делает игра

| Пакет (общее) | Игра (своё) |
|---|---|
| Покупка за монеты через экономику | Каталог товаров, цены, редкости |
| Покупка за реальные деньги через платформу | Список категорий (скины, шапки и т.д.) |
| Выдача в два шага: сначала запись прогресса, потом подтверждение платформе | Что считается обязательной и необязательной категорией |
| Защита от двойной выдачи по токену покупки | Предметы по умолчанию (базовый скин) |
| Обработка невыданных покупок при старте | Правила совместимости слотов (какой костюм что блокирует) |
| Экипировка: один предмет на категорию, снятие аксессуаров | Экраны, значки, показ статусов игроку |
| Восстановление из сохранения, неизвестные идентификаторы не теряются | Запись магазина в данные прогресса (образец ниже) |

## Типы товаров

| Тип (`StoreItemKind`) | Что это | Что происходит после покупки за реальные деньги |
|---|---|---|
| `Equippable` | Скин, шапка и т.п. Покупается один раз, надевается в свою категорию | Выдаётся, надевается, запись прогресса. На платформе **не гасится** |
| `CurrencyPack` | Пакет монет, покупается много раз | Монеты зачисляются, запись прогресса, и только потом покупка **гасится** на платформе |
| `Unlock` | Разовая разблокировка без экипировки (например, отключение рекламы) | Запоминается как «куплено», запись прогресса. На платформе не гасится |

Способ получения (`StoreAcquisitionKind`): `Coins` (за монеты) или `Platform` (за реальные деньги). Экипируемый товар за реальные деньги может иметь и цену в монетах: тогда он продаётся обоими способами.

## Как это работает

### Покупка за монеты
`TryBuyWithCoins(id)`: если товар уже куплен, он просто надевается (`AlreadyOwned`). Иначе деньги списываются через экономику (`InsufficientCoins`, если не хватает), товар выдаётся и надевается, прогресс записывается немедленно.

### Покупка за реальные деньги (два шага)
1. `RestoreAsync()` загружает каталог платформы (цены) и обрабатывает невыданные покупки. Без него `PurchaseAsync` вернёт `Unavailable`.
2. `PurchaseAsync(id)` проводит оплату и получает квитанцию с токеном.
3. Магазин **выдаёт** товар и помечает токен обработанным.
4. Магазин **записывает прогресс** через обработчик, который задала игра (`SetCriticalSaveHandler`).
5. Только если запись удалась, для пакета валюты вызывается подтверждение платформе (`ConsumeAsync`). Постоянные товары не гасятся.

Если игра закрылась после оплаты и не успела выполнить шаги 3-5, при следующем запуске `RestoreAsync()` вернёт эту покупку и выполнит оставшиеся шаги. Если токен уже обработан, он повторно ничего не выдаёт, а только повторяет запись и подтверждение.

> **Без обработчика записи покупки за реальные деньги не завершаются** (`GrantedSaveRetry`): магазин не подтвердит покупку, пока не убедится, что прогресс записан. Подключите `ProgressionService.SaveCriticalAsync` из модуля Save.

### Результаты (`StoreStatus`)

Магазин сообщает результат событием `StatusChanged`. Текст игрок видит из строк игры по ключу `StoreStatusKeys.ToKey(status)`.

| Статус | Ключ строки | Когда |
|---|---|---|
| `Success` | `store.status.success` | Покупка выполнена и записана |
| `NotForCoins` | `store.status.coins_only` | Товар за монеты не продаётся или неизвестен |
| `AlreadyOwned` | `store.status.already_owned` | Товар уже куплен, надет |
| `InsufficientCoins` | `store.status.insufficient_coins` | Не хватает монет |
| `AccessoryRemoved` | `store.status.accessory_removed` | Предмет снят |
| `OutfitUpdated` | `store.status.outfit_updated` | Предмет надет |
| `Unavailable` | `store.status.unavailable` | Нет покупок платформы или товара в её каталоге |
| `InProgress` | `store.status.in_progress` | Покупка идёт |
| `Canceled` | `store.status.canceled` | Отмена или отказ |
| `Invalid` | `store.status.invalid` | Квитанция без токена |
| `UnknownProduct` | `store.status.unknown_product` | Платформа вернула товар, которого нет в каталоге |
| `SaveRetry` | `store.status.save_retry` | Покупка обработана ранее, запись не удалась, будет повтор |
| `AlreadyProcessed` | `store.status.already_processed` | Покупка уже обработана |
| `ConsumeRetry` | `store.status.consume_retry` | Выдано, но платформа не подтвердила, будет повтор |
| `NotGranted` | `store.status.not_granted` | Выдать не удалось (например, нулевая сумма пакета) |
| `GrantedSaveRetry` | `store.status.granted_save_retry` | Выдано, но запись не удалась, подтверждения платформе нет |

## Подключение

Игре нужны каталог, настройки и три строки подключения. Ниже рабочий образец (этот код компилируется и проверяется в `Tools/CoreChecks`).

```csharp
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
```

Подключение на старте (после создания экономики, фабрики покупок и сервиса прогресса):

```csharp
var store = new SessionStoreService(
    economyService,                       // IEconomyService из Core
    purchasesService,                     // IPurchasesService из PurchasesServiceFactory (можно null)
    SampleStore.CreateCatalog(),
    SampleStore.CreateOptions());

// Магазин вызывает это после выдачи покупки и до подтверждения платформе.
store.SetCriticalSaveHandler(progression.SaveCriticalAsync);

// При необходимости правила совместимости слотов (полный костюм блокирует шапку).
store.SetEquipmentRules(new StoreSlotBlockingRules(
    new Dictionary<string, int[]> { ["skin_king"] = new[] { (int)SampleCategory.Hat } }));

// Подписка экранов: Changed для обновления, StatusChanged для сообщений игроку.
store.StatusChanged += status => ShowMessage(localization.Get(StoreStatusKeys.ToKey(status)));

// При старте игры и при открытии магазина: цены платформы и невыданные покупки.
_ = store.RestoreAsync();
```

Магазин в прогресс записывается и читается образцом `SampleStoreProgress` (см. выше): поля `ownedItemIds`, `processedPurchaseTokens`, `equippedItems`. Они подключаются в схему и связь модуля Save ([save.md](save.md)): `Capture` в `IProgressionBinding.Capture`, `Normalize` в `IProgressionSchema.Normalize`, `Apply` в `IProgressionBinding.Apply`. При любом изменении состояния магазина (событие `Changed`) связь вызывает `RequestBatchSave()`.

## Публичный API

### `SessionStoreService`

| Что | Назначение |
|---|---|
| `TryBuyWithCoins(id)` | Покупка за монеты. `true`, если товар теперь у игрока |
| `TryEquip(id)` | Надеть купленное. Для необязательной категории повтор снимает предмет. `false`, если не куплено или не экипируется |
| `PurchaseAsync(id)` | Покупка за реальные деньги. Итог в `StatusChanged` |
| `RestoreAsync()` | Каталог платформы и невыданные покупки. Вызывать при старте и открытии магазина |
| `IsOwned`, `IsEquipped`, `GetEquipped(category)` | Состояние для экранов |
| `IsBlockedByEquipment`, `IsSlotBlocked`, `GetActiveEquipment` | Блокировка слотов правилами |
| `GetDisplayPrice`, `GetCurrencyImageUrl` | Цена и значок валюты платформы |
| `IsAvailableForPurchase`, `IsPlatformAvailable` | Можно ли купить сейчас |
| `GetOwnedItemIds`, `GetProcessedPurchaseTokens`, `GetEquippedItems`, `RestoreProgress(...)` | Запись и чтение прогресса |
| `SetCriticalSaveHandler`, `SetEquipmentRules` | Подключение записи и правил слотов |
| События `Changed`, `StatusChanged` | Для экранов и звуков |

### Описание товара

`StoreItemDefinition.Equippable(...)`, `.CurrencyPack(...)`, `.Unlock(...)`: фабричные методы с понятными параметрами. Поля: `Id`, `TitleKey`, `Kind`, `Category` (число), `AcquisitionKind`, `CoinPrice`, `PlatformProductId`, `GrantedCoins`, `Rarity`; вычисляемые `IsEquippable`, `CanBuyWithCoins`, `HasCoinAlternative`.

### Помощники

| Что | Назначение |
|---|---|
| `StoreOptions` | Предметы по умолчанию и необязательные категории |
| `StoreSlotBlockingRules` | Готовые правила блокировки слотов по данным |
| `EquippedItemProgressData` | Запись «в категории надет предмет» для данных прогресса (`FromDictionary`, `Normalize`, `ToDictionary`) |
| `StoreRarity` | Редкость для рамок и подписей на экранах |

## Правила для игры

- Идентификаторы товаров (`Id`) и числа категорий **после выпуска менять нельзя**: они хранятся в прогрессе игрока.
- Идентификатор товара платформы (`PlatformProductId`) должен совпадать с консолью Яндекса.
- Убранные из каталога товары из прогресса не удаляйте: магазин сохранит их идентификаторы и проигнорирует. Игрок мог ими владеть.
- Строки для всех 16 статусов должны быть в таблицах локализации игры.

## Чего делать нельзя

- Гасить покупку платформе (`ConsumeAsync`) до выдачи и записи прогресса: магазин делает это сам в правильном порядке.
- Менять баланс монет мимо экономики.
- Вызывать `PurchaseAsync` до `RestoreAsync`.
- Хранить в категориях числа, которые могут поменяться (используйте `enum` с фиксированными значениями).

## Как проверить

Без Unity: `dotnet run --project Tools/CoreChecks`. Для Store там 48 проверок: покупки за монеты (не хватает, успех, повтор, не продаётся за монеты), экипировка и снятие, блокировка слотов, восстановление из сохранения, пакет валюты (порядок «запись, затем подтверждение»), постоянный товар (не гасится), защита от двойной выдачи по токену, сбой записи с повтором, невыданные покупки при старте, убранный из каталога товар, квитанция без токена, нечего выдавать, платформа недоступна. Критичные инварианты проверены «мутациями»: если сломать порядок записи и подтверждения, повторную выдачу или гашение постоянных товаров, проверки падают.

На стенде (секция «Покупки»): список товаров, покупка, повтор, невыданная покупка при перезапуске, закрытие вкладки между оплатой и выдачей. **Не проверено:** работа с настоящим хранилищем и платформой Яндекса.

## Отличия от «Обби Потоп»

- Категории стали числами (`int`) вместо enum `StoreItemCategory`. Числа те же и так же сохраняются в прогрессе, поэтому сохранения «Обби» читаются без миграции. Enum остаётся в игре и приводится к `int`.
- Вместо проверки «категория Currency» введён явный тип товара `StoreItemKind` (`Equippable`, `CurrencyPack`, `Unlock`). Поведение для скинов и пакетов монет прежнее.
- Каталог (`StoreCatalog`), предметы по умолчанию и необязательные категории стали параметрами конструктора, а не зашиты в сервис.
- Результаты `StatusChanged` стали типизированными (`StoreStatus`) вместо строк. Ключи строк для игрока те же (`StoreStatusKeys.ToKey`).
- Правила слотов: интерфейс `IStoreEquipmentRules` и готовая `StoreSlotBlockingRules`. `CharacterEquipmentRules` «Обби» может реализовать интерфейс.
- Сообщения для логов и комментарии на русском.
- Поведение покупок, порядок «запись, затем подтверждение», защита от двойной выдачи и восстановление из сохранения перенесены без изменений логики.

Как перевести «Обби» на модуль, см. `Docs/MIGRATION_FROM_OBBY.md` (раздел «Переключение Обби»).

## История

См. [CHANGELOG](../../CHANGELOG.md).
