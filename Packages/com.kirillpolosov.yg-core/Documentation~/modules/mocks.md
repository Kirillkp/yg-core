# Модуль: Моки редактора

Сборка: `YgCore.Mocks` · Пространство имён: `YgCore.Mocks` · С версии: `v0.1.0` · Статус: перенесён из «Обби Потоп»

Зависит от `YgCore.Contracts` и `YgCore.Localization`.

## Зачем нужен

Настоящая реклама, покупки, облако и вход работают только на домене Яндекса. Моки подставляют фальшивую платформу, чтобы играть и отлаживать игру в редакторе Unity без консоли Яндекса. Каждый мок умеет возвращать любой исход операции.

## Когда использовать

- Разработка и отладка в редакторе.
- Проверка редких исходов: отказ рекламы, сбой хранилища, невыданная покупка.

## Когда не использовать

- В выпускаемой сборке WebGL: там работает Яндекс-реализация. Выбор делают фабрики, вручную моки в такую сборку не подключайте.

## Как управлять исходом

Все моки наследуют `EditorMockPlatformService`:

| Свойство | Назначение |
|---|---|
| `Scenario` | `Success`, `Denied`, `Error`, `PlatformUnavailable`. Определяет результат всех операций мока |
| `IsPlatformAvailable` | Если `false`, всё возвращает `PlatformUnavailable` и сервис сообщает, что платформы нет |

```csharp
using YgCore.Mocks;

var ads = new EditorMockAdsService();

// Проверяем, как игра ведёт себя, когда игрок не досмотрел рекламу.
ads.Scenario = MockPlatformScenario.Denied;
```

## Моки

| Мок | Контракт | Особенности |
|---|---|---|
| `EditorMockAdsService` | `IAdsService` | При успехе шлёт `AdStarted` и `AdFinished`, при отказе и ошибке событий нет |
| `EditorMockAnalyticsService` | `IAnalyticsService` | Запоминает имена событий в `TrackedEvents` |
| `EditorMockAudioFocusService` | `IAudioFocusService` | Фокус меняется вручную: `SetAudioFocus(bool)` |
| `EditorMockAuthorizationService` | `IAuthorizationService` | Можно стартовать вошедшим; `SetGuest()`, `SetAuthorized()`; данные игрока в свойствах |
| `EditorMockGameReadinessService` | `IGameReadinessService` | `Reset()` сбрасывает отметку |
| `EditorMockGameplayLifecycleService` | `IGameplayLifecycleService` | Пауза от платформы имитируется `SetPlatformPaused(bool)` |
| `EditorMockLeaderboardService` | `ILeaderboardService` | Один общий список записей на все таблицы, без сортировки; `SeedEntry(...)` |
| `EditorMockLocalizationService` | `ILocalizationService` | Нужен каталог игры; язык меняется вручную |
| `EditorMockPurchasesService` | `IPurchasesService` | Каталог задаёт игра (конструктор с товарами или список `Catalog`); `AddPendingPurchase(...)`, `WasConsumed(token)`, `ReturnDuplicateToken` |
| `EditorMockSaveService` | `ISaveService` | Хранилище в памяти; `SimulateTimeout` даёт `Error` и не меняет данные; нет ключа: успех со значением `null` |

## Чего делать нельзя

- Включать моки в сборку для Яндекса вручную в обход фабрик.
- Считать поведение моков доказательством, что всё работает на платформе: реальный исход проверяет стенд.

## Как проверить

Без Unity: `dotnet run --project Tools/CoreChecks` (сценарии, события, хранилище, покупки, вход, язык).

## Отличия от «Обби Потоп»

- Пространство имён `YgCore.Mocks`, сообщения результатов на русском.
- `EditorMockLocalizationService` принимает каталог строк (раньше использовал статический каталог игры).
- `EditorMockPurchasesService` по умолчанию содержит общий тестовый каталог (`coins_small`, `coins_medium`, `coins_large`, `premium_item`) вместо товаров «Обби». Игра с собственными товарами передаёт каталог в конструктор.

## История

См. [CHANGELOG](../../CHANGELOG.md).
