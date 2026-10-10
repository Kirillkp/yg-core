# Модуль: Фабрики и заглушки

Сборка: `YgCore.Factories` · Пространство имён: `YgCore.Factories` · С версии: `v0.1.0` · Статус: перенесён из «Обби Потоп»

Зависит от `YgCore.Contracts`, `YgCore.Localization`, `YgCore.Mocks`, `YgCore.Yandex`.

## Зачем нужен

Выбирает, какая реализация контракта стоит за игрой: настоящая на PluginYG2, мок в редакторе или безопасная заглушка «платформы нет». Заглушки позволяют игре работать там, где платформы нет вообще (например, запуск вне Яндекса), и не падать.

## Что уже есть: заглушки

Каждая заглушка сообщает `IsPlatformAvailable = false`, а любая операция возвращает `PlatformUnavailable`. События платформы не приходят (подписаться можно, но они не сработают). Единственное исключение: `UnavailableLocalizationService` шлёт `LanguageChanged`, когда язык переключают вручную.

| Заглушка | Поведение сверх «недоступно» |
|---|---|
| `UnavailableAdsService` | |
| `UnavailableAuthorizationService` | `CurrentProfile` всегда гость |
| `UnavailableGameReadinessService` | `IsReadySent` всегда `false` |
| `UnavailableGameplayLifecycleService` | `IsGameplayActive` всегда `false` |
| `UnavailableLeaderboardService` | |
| `UnavailableLocalizationService` | Язык определить нельзя, но строки из каталога игры работают и язык переключается вручную. Начальный язык: язык по умолчанию каталога |
| `UnavailablePurchasesService` | |
| `UnavailableSaveService` | Загрузка возвращает `PlatformUnavailable`, а не пустое сохранение |

У аналитики и аудиофокуса заглушек нет (их не было в «Обби»).

## Фабрики

У каждого сервиса есть статическая фабрика `Create(...)`, которая выбирает реализацию по условиям компиляции:

1. В редакторе: переданный мок или мок по умолчанию.
2. В WebGL с нужным модулем PluginYG2 (например, `Payments_yg`): реализация на PluginYG2.
3. Иначе: заглушка «недоступно».

| Фабрика | Контракт | Условие для PluginYG2 (WebGL) | Параметры |
|---|---|---|---|
| `AdsServiceFactory` | `IAdsService` | `RewardedAdv_yg` или `InterstitialAdv_yg` | `editorService` |
| `AuthorizationServiceFactory` | `IAuthorizationService` | `Authorization_yg` | `editorService` |
| `GameReadinessServiceFactory` | `IGameReadinessService` | `YandexGamesPlatform_yg` | `editorService` |
| `GameplayLifecycleServiceFactory` | `IGameplayLifecycleService` | `YandexGamesPlatform_yg` | `editorService` |
| `LeaderboardServiceFactory` | `ILeaderboardService` | `Leaderboards_yg` | `editorService` |
| `PurchasesServiceFactory` | `IPurchasesService` | `Payments_yg` | `editorService` |
| `SaveServiceFactory` | `ISaveService` | `Storage_yg` | `editorService`, `storageFieldName` (см. [yandex.md](yandex.md)) |
| `LocalizationServiceFactory` | `ILocalizationService` | `Localization_yg` | `catalog` (обязателен), `editorService` |

В редакторе параметр `editorService` позволяет передать мок с нужным сценарием:

```csharp
var ads = new EditorMockAdsService { Scenario = MockPlatformScenario.Denied };
IAdsService service = AdsServiceFactory.Create(ads);
```

У аналитики и аудиофокуса фабрик нет (их не было в «Обби»).

## Когда использовать заглушки напрямую

Почти никогда: выбор делают фабрики. Прямое создание нужно в тестах и на стенде, когда нужен именно «пустой» вариант.

## Чего делать нельзя

- Считать результат заглушки сбоем: `PlatformUnavailable` означает «платформы нет», игра должна продолжить работу без неё.

## Как проверить

Без Unity: `dotnet run --project Tools/CoreChecks` (заглушки) и `dotnet run --project Tools/YandexChecks` (фабрики вне редактора и WebGL возвращают заглушки). Ветки редактора и WebGL проверяются только в Unity: стенд, секция каждого сервиса.

## Отличия от «Обби Потоп»

- Пространство имён `YgCore.Factories`, сообщения на русском.
- `UnavailableLocalizationService` и `LocalizationServiceFactory` принимают каталог строк игры (раньше использовали статический каталог).
- `SaveServiceFactory` принимает имя поля сохранения (см. [yandex.md](yandex.md)).
- Фабрики теперь ссылаются на сборки `YgCore.Mocks` и `YgCore.Yandex`.

## История

См. [CHANGELOG](../../CHANGELOG.md).
