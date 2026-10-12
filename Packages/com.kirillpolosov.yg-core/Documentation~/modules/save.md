# Модуль: Сохранения и прогресс

Сборка: `YgCore.Save` · Пространство имён: `YgCore.Save` · С версии: `v0.1.0` · Статус: перенесён из «Обби Потоп», проверен вне Unity; запись и чтение через Яндекс проверены на стенде (`ProgressionService` на живой платформе стенд не гонял)

Зависит от `YgCore.Core` и `YgCore.Contracts`.

## Зачем нужен

Прогресс игрока нельзя потерять. Модуль берёт на себя всё, что для этого нужно в любой игре, и оставляет игре только то, что у каждой своё: какие данные хранить и как их объединять. Он отвечает на вопросы: что делать, если облако не ответило; что делать со старым сохранением; как не затереть прогресс, который не удалось прочитать; как слить прогресс гостя с облачным после входа.

## Что делает пакет и что делает игра

| Пакет (общее) | Игра (своё) |
|---|---|
| Поля конверта `format`, `schemaVersion`, `revision` | Остальные поля данных |
| Разбор заголовка, проверка формата и версии | Схема: приведение, слияние, миграции со старых версий |
| Блокировка записи после сбоя загрузки | Какие сервисы игры читать и записывать (`Capture`/`Apply`) |
| Пакетная и немедленная запись, повтор после сбоя | Какие изменения «важные» (записать сразу), какие нет |
| Лимит размера записи | |
| Слияние гостя с облаком при входе | |
| Сброс, перенос старой схемы с немедленной перезаписью | |

## Как это работает

1. **Загрузка.** `InitializeAsync` читает запись по ключу из хранилища. Если данных нет, игрок новый (`Empty`). Если есть, они разбираются и применяются к сервисам игры.
2. **Защита прогресса.** Любой сбой загрузки (ошибка хранилища, повреждённые данные, чужой формат, версия новее, нет платформы) **блокирует автоматическую запись**: нельзя затереть пустым прогресс, который просто не удалось прочитать. Блокировку снимает только успешная загрузка или явный `ResetProgressAsync`.
3. **Миграция.** Данные старой схемы переводятся на текущую и сразу записываются заново.
4. **Запись.** Игра сообщает об изменениях двумя способами: `RequestBatchSave()` (записать через 1,5 с, изменения объединяются) и `RequestCriticalSave()` (записать сейчас: награда, покупка). Номер записи (`revision`) растёт при каждой записи. Слишком большая запись (больше 190 КБ) не отправляется. После неудачи запись повторяется через 10 с.
5. **Вход игрока.** Когда гость входит в аккаунт, прогресс гостя и облачный объединяются правилами схемы игры. Если облачный прогресс не читается, он не перезаписывается. Снимок гостя делается **после** получения облака, поэтому награда или покупка, полученные за время слияния, в результат входят (до `v0.2.0` снимок делался до загрузки облака, и такие изменения терялись). Запросы записи за время слияния не теряются: итоговую запись делает слияние, а при сбое облака запись повторяется отдельно. Слияние по-прежнему подчиняется правилам вашей `Merge`: если число берётся максимумом, награда гостя не суммируется с облачным значением.

### Состояния загрузки (`ProgressionLoadState`)

| Состояние | Что произошло | Запись |
|---|---|---|
| `NotLoaded` | Загрузка ещё не выполнялась | |
| `Empty` | Сохранения нет (новый игрок) | разрешена |
| `Loaded` | Прочитано в текущей схеме | разрешена |
| `Migrated` | Прочитано в старой схеме, приведено и сразу записано заново | разрешена |
| `Corrupted` | Повреждено или не наш формат | **заблокирована** |
| `UnsupportedVersion` | Создано более новой версией игры | **заблокирована** |
| `PlatformUnavailable` | Хранилища нет | **заблокирована** |
| `Error` | Хранилище вернуло ошибку | **заблокирована** |

## Подключение

Игре нужны три небольших класса и один вызов. Ниже полный рабочий образец (этот код компилируется и проверяется в `Tools/CoreChecks`).

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using YgCore.Core;
using YgCore.Save;

// 1. Данные игры: наследник ProgressionData с публичными полями.
[Serializable]
public sealed class SampleProgress : ProgressionData
{
    public int balance;
    public int bestDistance;
    public List<string> ownedItemIds = new List<string>();
    public SettingsProgressData settings = new SettingsProgressData();
}

// 2. Схема: формат, версия и правила приведения, слияния и миграции.
public sealed class SampleSchema : IProgressionSchema<SampleProgress>
{
    public string FormatId => "sample-progress";

    public int CurrentSchemaVersion => 1;

    public SampleProgress Normalize(SampleProgress data)
    {
        data.balance = Math.Max(0, data.balance);
        data.bestDistance = Math.Max(0, data.bestDistance);
        data.ownedItemIds = ProgressionLists.Distinct(data.ownedItemIds);
        data.settings = SettingsProgressData.Normalized(data.settings);
        return data;
    }

    public SampleProgress Merge(SampleProgress guest, SampleProgress cloud)
    {
        // Числа берём большие, списки объединяем, настройки из более свежей записи.
        var preferenceSource = cloud.revision >= guest.revision ? cloud : guest;
        return new SampleProgress
        {
            balance = Math.Max(guest.balance, cloud.balance),
            bestDistance = Math.Max(guest.bestDistance, cloud.bestDistance),
            ownedItemIds = ProgressionLists.Union(guest.ownedItemIds, cloud.ownedItemIds),
            settings = SettingsProgressData.Normalized(preferenceSource.settings)
        };
    }

    public SampleProgress Migrate(int schemaVersion, string payload)
    {
        if (schemaVersion == 0)
        {
            var legacy = JsonUtility.FromJson<LegacyV0>(payload);
            return new SampleProgress
            {
                revision = legacy.revision,
                balance = legacy.coins,
                bestDistance = legacy.recordMeters,
                ownedItemIds = legacy.items
            };
        }

        throw new NotSupportedException("Неизвестная старая версия схемы: " + schemaVersion);
    }

    // Старый формат (версия 0), другие имена полей. Поля заполняет JsonUtility, поэтому предупреждение компилятора не нужно.
#pragma warning disable 0649
    [Serializable]
    private sealed class LegacyV0
    {
        public long revision;
        public int coins;
        public int recordMeters;
        public List<string> items = new List<string>();
    }
#pragma warning restore 0649
}

// 3. Связь с сервисами игры: что собрать для записи, что применить после загрузки, за чем следить.
public sealed class SampleBinding : IProgressionBinding<SampleProgress>
{
    private readonly SessionEconomyService economy;
    private readonly SessionGameSettingsService settings;
    private ProgressionService<SampleProgress> service;

    public SampleBinding(SessionEconomyService economy, SessionGameSettingsService settings)
    {
        this.economy = economy;
        this.settings = settings;
    }

    public int BestDistance { get; set; }

    public List<string> OwnedItems { get; private set; } = new List<string>();

    public SampleProgress Capture()
    {
        return new SampleProgress
        {
            balance = economy.Balance,
            bestDistance = BestDistance,
            ownedItemIds = new List<string>(OwnedItems),
            settings = SettingsProgressData.CaptureFrom(settings)
        };
    }

    public void Apply(SampleProgress data)
    {
        economy.RestoreBalance(data.balance);
        BestDistance = data.bestDistance;
        OwnedItems = new List<string>(data.ownedItemIds ?? new List<string>());
        (data.settings ?? new SettingsProgressData()).ApplyTo(settings);
    }

    public void Attach(ProgressionService<SampleProgress> progression)
    {
        service = progression;
        economy.BalanceChanged += HandleBalanceChanged;
        settings.SettingsChanged += HandleSettingsChanged;
    }

    public void Detach()
    {
        economy.BalanceChanged -= HandleBalanceChanged;
        settings.SettingsChanged -= HandleSettingsChanged;
        service = null;
    }

    public void AddItem(string itemId)
    {
        OwnedItems.Add(itemId);
        service?.RequestBatchSave();
    }

    private void HandleBalanceChanged(object sender, EconomyBalanceChangedEventArgs args)
    {
        // Награду за забег терять нельзя: пишем сразу. Остальные изменения баланса пишутся пакетом.
        if (args.Reason == "run_reward")
        {
            service?.RequestCriticalSave();
        }
        else
        {
            service?.RequestBatchSave();
        }
    }

    private void HandleSettingsChanged(object sender, GameSettingsChangedEventArgs args)
    {
        service?.RequestBatchSave();
    }
}
```

Затем один раз на старте, после создания сервисов (экономика, настройки, авторизация и хранилище из фабрик):

```csharp
var progression = new ProgressionService<SampleProgress>(
    saveService,                // ISaveService из SaveServiceFactory
    authorizationService,       // IAuthorizationService, нужен для слияния после входа (можно null)
    new SampleSchema(),
    binding,
    new ProgressionOptions { StorageKey = "player_progress" });

await progression.InitializeAsync();   // загрузка, применение и подключение отслеживания
```

### Составное действие одной записью

Если одно игровое действие меняет несколько вещей (рекорд и награду), оберните его так, чтобы записалось один раз:

```csharp
using (progression.SuppressChangeTracking())
{
    binding.BestDistance = distance;
    economy.TryCredit(transactionId, reward, "run_reward");
}

progression.RequestCriticalSave();   // одна общая запись после действия
```

### Подтверждение покупки только после записи

Покупку платформе подтверждают (`ConsumeAsync`) только после того, как награда записана:

```csharp
if (await progression.SaveCriticalAsync())
{
    await purchasesService.ConsumeAsync(receipt.Token);
}
```

## Публичный API

### `ProgressionService<TData>`

| Что | Назначение | Замечания |
|---|---|---|
| `InitializeAsync()` | Загрузить, применить, подключить отслеживание | Повторный вызов ничего не делает |
| `RequestBatchSave()` | Записать скоро | Изменения объединяются, срок не сдвигается |
| `RequestCriticalSave()` | Записать сразу, не ждать | Ошибок не бросает, при сбое будет повтор |
| `SaveCriticalAsync()` | Записать сразу и дождаться | `true` только при подтверждённой записи |
| `SaveNowAsync()` | Записать сразу, вернуть подробный результат | |
| `ReloadAsync()` | Загрузить заново и применить | |
| `ResetProgressAsync()` | Удалить прогресс и записать пустой | Единственный способ снять блокировку без загрузки |
| `SuppressChangeTracking()` | Отключить запросы на сохранение на время составного действия | Освободить через `using`, можно вкладывать |
| `LoadState`, `IsWriteBlocked`, `StatusMessage`, `LastPayloadBytes`, `Revision` | Состояние для логов и отладки | `StatusMessage` игроку не показывать |
| `Dispose()` | Отписаться, отменить ожидающую запись | После этого записей нет |

### Интерфейсы игры

| Интерфейс | Что реализует игра |
|---|---|
| `IProgressionSchema<TData>` | `FormatId`, `CurrentSchemaVersion`, `Normalize`, `Merge`, `Migrate` |
| `IProgressionBinding<TData>` | `Capture`, `Apply`, `Attach`, `Detach` |

### Помощники

| Что | Назначение |
|---|---|
| `ProgressionLists.Distinct`, `Union` | Списки идентификаторов без повторов и пустых значений, отсортированные |
| `SettingsProgressData` | Готовый блок для настроек звука из Core: `CaptureFrom`, `Normalized`, `ApplyTo`. Громкость `NaN` заменяется значением по умолчанию (с `v0.2.0`: раньше `Mathf.Clamp01` пропускал `NaN` в сохранение и в звук) |
| `ProgressionOptions` | Ключ записи, задержки (1,5 с и 10 с), предел размера (190 КБ) |
| `ProgressionCodec<TData>` | Разбор и запись строки данных (нужен отдельно редко) |

## Правила для схемы игры

- `FormatId` и ключ записи (`StorageKey`) после выпуска игры **менять нельзя**: прогресс перестанет находиться.
- Любое изменение формата данных: поднять `CurrentSchemaVersion` и добавить ветку в `Migrate`. Без миграции выпускать нельзя. Новую версию записывайте в `compatibility.md` игры.
- `Normalize` должен быть безопасным для любых данных: отрицательные числа, `null`-списки, значения вне диапазона.
- `Merge` не должен терять накопленное: числа берите большие, списки объединяйте.
- Идентификаторы, убранные из каталога игры, из данных не выбрасывайте: игрок мог ими владеть, а магазин просто их проигнорирует.

## События и ошибки

| Ситуация | Результат |
|---|---|
| Сбой загрузки | `LoadState` `Error`, запись заблокирована, прогресс в хранилище не тронут |
| Нечитаемые данные | `Corrupted`, запись заблокирована, данные не тронуты |
| Исключение в `Migrate` | Данные считаются повреждёнными (`Corrupted`) |
| Запись больше предела | `SaveNowAsync` возвращает `Error`, в хранилище ничего не уходит |
| Неудачная запись | Повтор через `RetryDelay` |
| Вызов после `Dispose` | `PlatformUnavailable` / запись не выполняется |

## Чего делать нельзя

- Записывать прогресс в обход сервиса (напрямую в `ISaveService` с этим же ключом).
- Менять `FormatId` или `StorageKey` после выпуска.
- Менять поля конверта (`format`, `schemaVersion`, `revision`) вручную.
- Показывать игроку `StatusMessage`.
- Использовать в данных типы, которые не понимает `JsonUtility` (словари, интерфейсы).

## Как проверить

Без Unity: `dotnet run --project Tools/CoreChecks`. Для Save там 47 проверок на образце выше (в том числе слияние при входе с наградой и покупкой за время слияния и при сбое облака, `NaN` и бесконечность в громкости): разбор заголовка, старая схема, повреждённые данные, версия новее, нет платформы, сбой загрузки без затирания, пакетная запись объединяется, повтор после сбоя, лимит размера, составное действие, слияние при входе, нечитаемое облако не затирается, сброс, выгрузка.

На стенде (секция «Сохранение»): запись и чтение, сбой загрузки, быстрые записи подряд, загрузка сохранения старой схемы. **Не проверено:** `JsonUtility` настоящего Unity (в проверках его заменяет упрощённая версия) и поведение с настоящим хранилищем Яндекса.

## Отличия от «Обби Потоп»

- В «Обби» всё было одним классом `GameProgressionService` с игровыми полями. Теперь общая часть (`ProgressionService<TData>`, кодек, состояния) в пакете, а поля, правила слияния, миграции и связь с сервисами остаются в игре.
- Формат JSON **не изменился**: поля конверта плоские, как раньше, поэтому сохранения «Обби» читаются без миграции. Нужно лишь сделать `GameProgressData` наследником `ProgressionData`.
- `ProgressionPayloadCodec` (статический) → `ProgressionCodec<TData>` (с игровой схемой). `ProgressionLoadState` сохранил имя и значения, но теперь лежит в `YgCore.Save`.
- Сообщения для логов переведены на русский.
- Добавлены `SuppressChangeTracking()` (вместо внутреннего флага записи результата забега), `SaveCriticalAsync()` (вместо обработчика магазина) и `ProgressionOptions` (значения по умолчанию прежние).
- `Normalize` теперь применяется и к пустым данным при сбросе.

Как перевести «Обби» на модуль, см. `Docs/MIGRATION_FROM_OBBY.md` (раздел «Переключение Обби»).

## История

См. [CHANGELOG](../../CHANGELOG.md).
