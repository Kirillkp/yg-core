# План переноса кода из «Обби Потоп»

Источник: `/Users/kirillpolosov/obby`, `Assets/Scripts` и `Assets/PlatformServices`. Таблица составлена по именам файлов и числу строк, **содержимое большинства файлов ещё не читалось**. Границы уточняются при переносе каждого модуля.

Критерий готовности всего переноса: «Обби Потоп» переключается на пакет и работает как раньше.

Порядок: по модулям. Каждый модуль переносится вместе с модульным документом и секцией стенда.

## Таблица переноса

| Модуль пакета | Источник в «Обби» | Размер | Статус | Замечания |
|---|---|---|---|---|
| Core | `Scripts/Core/` (16 файлов: `AppState`, `PauseCoordinator`, `PauseReason`, `GameplayStateService`, `RealtimeDelay`, `SessionEconomyService`, `SessionGameSettingsService`, события) | около 625 строк | перенесён | Пространство имён `YgCore.Core`. Поведение сохранено, проверено `Tools/CoreChecks`. Отличия: `modules/core.md` |
| Контракты | `Scripts/PlatformServices/Contracts/` (22 файла) | около 445 строк | перенесён | Отдельная сборка `YgCore.Contracts` без зависимости от Core. Комментарии о значении статусов сверены с реализацией на PluginYG2 |
| Фабрики и заглушки | `Scripts/PlatformServices/*Factory.cs`, `Unavailable*` | около 400 строк | перенесён | Локализация принимает каталог, сохранение принимает имя поля |
| Моки | `Scripts/PlatformServices/EditorMocks/` (12 файлов) | около 490 строк | перенесён | Каталог локализации стал параметром. Общий тестовый каталог покупок вместо товаров «Обби» |
| Яндекс-реализация | `Scripts/PlatformServices/PluginYG2/` (9 файлов: реклама, авторизация, покупки 548, сохранение 363, лидерборд, язык, готовность, жизненный цикл, рефлексия) | около 2100 строк | перенесён | Через рефлексию, как в «Обби». Проверен вне Unity (`Tools/YandexChecks`), в Unity и на Яндексе не проверен |
| Заплатки PluginYG2 | `Assets/PlatformServices/PluginYG2/`: `ObbyPotopYandexSdkBridge.cs` (253), `…PaymentsBridge.cs` (151), `…StorageBridge.cs` (114), `Plugins/ObbyPotopStorage.jslib` (55), `Plugins/ObbyPotopPayments.jslib` (66), `SavesYGObbyPotop.cs` | около 640 строк | перенесён | Папка `YandexBridge` без `.asmdef`. Имена `Obby*` заменены на `YgCore*`. См. DEC-009, DEC-015, `modules/yandex.md` |
| Загрузочный экран | `ObbyLoading.jslib` (5 строк), WebGL-шаблон | малый | перенесён частично | `YgCoreLoading.jslib` вызывает `window.YgLoading.gameReady()`, если страница его определила. Сам загрузочный экран остаётся в WebGL-шаблоне игры |
| Сохранения и прогресс | `Scripts/Gameplay/Progression/` (`GameProgressionService` 611, `ProgressionPayloadCodec` 341, `GameProgressData`) | около 1000 строк | перенесён общий движок | `ProgressionService<TData>`, `ProgressionCodec<TData>`, состояния, защита от потери. Поля `GameProgressData`, правила слияния, миграция v0 и ID брейнротов, `Capture`/`Apply`, `RegisterRunResult`, туториал остаются в игре |
| Магазин (логика) | `SessionStoreService`, `StoreCatalog`, `StoreItemDefinition`, `StoreRarity`, `StoreAcquisitionKind` | около 700 строк логики | перенесён | Каталог, категории, цены, предметы по умолчанию остаются в игре; категории стали `int`. Экраны (карточки, окно магазина, превью, информер) не переносятся |
| Аудио | `AudioDirector`, `AudioSettingsApplier`, `SfxId` | около 600 строк | перенесён общий механизм | Базовый `AudioDirector` в пакете. Игровые подписки (бегун, монеты, брейнроты, жизни, вода, магазин, экономика) и enum `SfxId` остаются в игре |
| Локализация | `LocalizationCatalog` (483), `LocalizationService*`, `LocalizedTextView` | около 500 строк | перенесён | `DictionaryLocalizationCatalog` и `LocalizedTextView` (необязательная сборка `YgCore.Localization.UI`) в пакете. Тексты `LocalizationCatalog` остаются в игре |
| Web-качество | `WebQualityBootstrap`, `WebQualitySettings` | около 60 строк | перенесён | Правила выбора вынесены в `WebQualityPolicy` и проверены. JavaScript `ObbyWebIsMobile` → `YgCoreWebIsMobile` |

## Не переносится в пакет

- Весь геймплей: трасса, вода, препятствия, монеты, брейнроты, герой, камера, ввод бегуна.
- Все экраны UI и адаптивные макеты (`*Controller`, `*AdaptiveLayout`, карточки магазина). Уходят в шаблоны, отделённые от контента «Обби».
- Контент: арт, музыка, тексты, каталог товаров.
- `PluginYourGames/` (сам PluginYG2): внешняя зависимость, DEC-008.

## Порядок работ и статус

1. Core и Контракты: **сделано**.
2. Фабрики, заглушки, моки: **сделано**.
3. Яндекс-реализация вместе с заплатками: **сделано** (проверена вне Unity).
4. Сохранения и прогресс: **сделано**.
5. Магазин, аудио, локализация, Web-качество: **сделано**.
6. **Стенд: не сделано.** Нужен запущенный Unity Editor: создать проект `Stand/`, секции по `Docs/STAND_CHECKLIST.md`, WebGL-сборка, прогон на Яндексе. Это условие выпуска `v0.1.0`.
7. Первая реальная компиляция пакета в Unity и проверка допущений, помеченных «не проверено в Unity» (особенно DEC-015: мосты без `.asmdef` попадают в `Assembly-CSharp`; необязательные сборки `Audio.UI` и `Localization.UI`).
8. Переключение «Обби Потоп» на пакет (раздел ниже) и проверка.
9. Выпуск `v0.1.0`: запись версии, `Tools/check-release.sh`, тег.

После каждого шага: обновлены модульный документ, CHANGELOG, `00-index.md` (статус модуля).

## Переключение «Обби»

Когда все модули перенесены, «Обби Потоп» переключается на пакет. Что для этого нужно сделать в игре (по результатам переноса Core, Контрактов, моков, заглушек, локализации и Яндекс-модуля):

| Что | Действие |
|---|---|
| Пространства имён | `ObbyPotop.Core` → `YgCore.Core`; контракты `ObbyPotop.PlatformServices` → `YgCore.Contracts`; моки → `YgCore.Mocks`; заглушки и фабрики → `YgCore.Factories` |
| Каталог строк | Статический `LocalizationCatalog` игры оборачивается в `DictionaryLocalizationCatalog` (тексты остаются в игре) и передаётся в `LocalizationServiceFactory` и `EditorMockLocalizationService` |
| Мок покупок | `EditorMockPurchasesService` в игре создаётся с каталогом «Обби» (скины и наборы монет), так как общий каталог пакета другой |
| **Сохранения игроков** | Передать в `SaveServiceFactory.Create(storageFieldName: "obbyPotopStorage")` и оставить в игре `partial SavesYG` с полем `obbyPotopStorage` (сейчас это `SavesYGObbyPotop.cs`). Иначе облачные сохранения живых игроков не найдутся |
| **Веб-качество** | Файл `Resources/WebQualitySettings.asset` пересоздаётся из меню `YgCore / Web Quality Settings` (скрипт другой, старый файл потеряет ссылку): значения `Web Desktop`, `Web Mobile`, `-1` те же. В WebGL-шаблоне `window.ObbyWebQuality` → `window.YgWebQuality` в `index.html` и `quality.js` (если нужно, свойство `isMobile` остаётся). Из `Scripts/PlatformServices` удаляются `WebQualityBootstrap`, `WebQualitySettings`, а `Assets/PlatformServices/Web/ObbyWebQuality.jslib` |
| **Надписи** | `LocalizedTextView` берётся из пакета. Ссылки на старый скрипт в префабах экранов (меню, магазин, настройки, пауза, коллекция, HUD, брейнрот-вью) нужно перепривязать: у компонента другой скрипт, поэтому при простой замене файлов поля `entries` потеряются. Перепривязку лучше делать скриптом, который заменяет GUID старого скрипта на GUID нового во всех `.prefab` и `.unity` (см. «Переключение Обби») |
| **Аудио** | Из `AudioDirector` «Обби» делается наследник пакетного `AudioDirector`: в `OnInitialized` остаются подписки на бегуна, монеты, брейнроты, давление, воду и экономику; `OnAppStateEntered(Run)` сбрасывает счётчики жизней и монет; `KeepLoopOnResult` оставляет пузыри при финальном затоплении; `GetMusicPitch` ускоряет музыку при опасности. Звуки магазина: подписка на `store.StatusChanged` и сравнение `StoreStatus`. `SfxId` остаётся в игре, в префабе директора идентификаторы заново назначаются (скрипт другой, потеряются ссылки на поля: **префаб директора нужно пересобрать в Unity**). **Поведение звука при паузах меняется намеренно:** теперь реклама останавливает весь звук (включая «опасность»), а отсчёт музыку не держит, то есть «Обби» звучит иначе, чем раньше; это исправление. Опасность и пузыри заводятся записями `Loops` (громкости 0,55 с гашением на паузе игрока и 0,7). Правила имён кнопок (`Play`, `Close`, `Shop`…) переезжают в `UiPressSounds.Resolver`. `AudioSettingsApplier` берётся из пакета |
| **Магазин** | `StoreCatalog.CreateDefault()` переписывается на `StoreItemDefinition.Equippable(...)` / `CurrencyPack(...)` с `(int)StoreItemCategory.X` (категория `Currency` → `CurrencyPack`). `DefaultCharacterId` и `IsOptionalAccessory` уходят в `StoreOptions` (`DefaultItems = { [Character] = "character_default" }`, `OptionalCategories = { Head, Face, Torso, Pants }`). `StoreRarity` и `StoreAcquisitionKind` берутся из пакета (имена и значения те же). Enum `StoreItemCategory` остаётся в игре с теми же числами. `CharacterEquipmentRules` реализует `IStoreEquipmentRules` (или заменяется на `StoreSlotBlockingRules`). `SessionStoreService.StatusChanged` теперь даёт `StoreStatus`: экраны и `AudioDirector` переходят на `StoreStatusKeys.ToKey(status)` или сравнивают статусы. `GetEquippedItems()` и `RestoreProgress(...)` работают с `int` категориями. В `GameProgressionService` обработчик критического сохранения магазина получает `SaveCriticalAsync`. `EquippedItemProgressData` берётся из пакета (поля те же) |
| **Прогресс** | `GameProgressData` делается наследником `ProgressionData` (поля те же, формат JSON не меняется). Из `ProgressionPayloadCodec` в игровую схему `IProgressionSchema<GameProgressData>` уходят `Normalize` (включая миграцию ID брейнротов), `Merge`, `Migrate` (v0 → v1) и тестовые полезные нагрузки. `FormatId = "obby-potop-progress"`, ключ `player_progress`, предел 190 КБ, задержки 1,5 с и 10 с остаются прежними. Из `GameProgressionService` остаётся тонкая игровая обёртка: `BestDistance`, `RegisterDistance`, `RegisterRunResult` (внутри `SuppressChangeTracking()`), `CompleteTutorial`, `Capture`/`Apply` в классе-связке. Обработчик критического сохранения магазина получает `SaveCriticalAsync`. `GameSettingsProgressData` можно заменить на `SettingsProgressData` |
| Старые файлы | Удалить из игры перенесённые `Scripts/Core`, `Scripts/PlatformServices`, `Assets/PlatformServices/PluginYG2` (мосты, `.jslib`), чтобы не было двух копий |
| Доработка PluginYG2 | В `Auth_js.js` заменить `ObbyAuthDialogClosed` на `YgCoreAuthDialogClosed` (два места) |
| Загрузочный экран | В WebGL-шаблоне заменить `window.ObbyLoading` на `window.YgLoading` |
| Проверка | Пройти чек-лист стенда на сборке «Обби» и playtest в черновике консоли. Особое внимание: загрузка существующего сохранения, покупка с досрочным закрытием вкладки, отмена входа |

## Открытые вопросы

- **PluginYG2 компилируется в `Assembly-CSharp`**, а сборки с `.asmdef` не могут на него ссылаться напрямую. Поэтому в «Обби» Яндекс-реализация обращается к PluginYG2 через рефлексию (`PluginYG2BridgeReflection`), а мосты и `SavesYG` лежат в `Assembly-CSharp`. В пакете файлы без `.asmdef` в `Assembly-CSharp` **не попадают** (проверено на стенде), поэтому мосты копирует в проект игры установщик (DEC-022), а граница `YgCore.Yandex` ↔ PluginYG2 устроена как в «Обби»: рефлексия плюс папка `YandexBridge` без asmdef (DEC-015, предложено). **Остаётся проверить в Unity**, что такой код действительно попадает в `Assembly-CSharp`.

- Где проходит граница между общей рамкой сохранения и полями игры (шаг 4).
- Как подключать PluginYG2 (DEC-008) и как закрепить его версию.
- Куда относится `LocalizedTextView` (DEC-011).
- Проверить реклама, авторизация и лидерборд PluginYG2 на надёжность подтверждений так же, как сделано для сохранения и покупок.
