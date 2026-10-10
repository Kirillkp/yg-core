# Модуль: Яндекс-реализация (PluginYG2)

Сборка: `YgCore.Yandex` + мосты из папки `Runtime/YandexBridge~`, которые установщик (`YgCore.Editor`) копирует в `Assets/YgCore/YandexBridge` игры, в `Assembly-CSharp` · Пространства имён: `YgCore.Yandex`, `YgCore.YandexBridge` · С версии: `v0.1.0` · Статус: перенесён из «Обби Потоп», проверен на стенде на Яндексе (v0.1.0): реклама, авторизация, лидерборд, покупки, сохранение, язык, готовность, пауза платформы

Зависит от `YgCore.Core`, `YgCore.Contracts`, `YgCore.Localization`. Работает с PluginYG2 v2.0092 (см. [compatibility.md](../compatibility.md)).

## Зачем нужен

Превращает контракты платформы (`IAdsService`, `ISaveService` и т.д.) в вызовы PluginYG2, а в двух местах (сохранение и покупки) добавляет то, чего PluginYG2 не даёт: явное подтверждение операций. Игровой код с этим модулем не работает напрямую: сервисы создают фабрики (модуль Factories).

## Как устроено

```
Игра ──→ интерфейсы (Contracts) ──→ сервисы PluginYG2*Service        (YgCore.Yandex, есть .asmdef)
                                          │ рефлексия (по имени типа)
                                          ▼
                                     мосты YgSdkBridge, YgPaymentsBridge, YgStorageBridge,
                                     расширение SavesYG                 (YandexBridge, в Assets игры, Assembly-CSharp)
                                          │ прямые вызовы YG2
                                          ▼
                                     PluginYG2 (Assembly-CSharp) ──→ JS ──→ SDK Яндекс Игр
                                     + YgCore*.jslib (подтверждения покупок и хранилища)
```

### Почему рефлексия и установка мостов в проект игры

PluginYG2 компилируется в `Assembly-CSharp`. Сборки с `.asmdef` (как `YgCore.Yandex`) не могут ссылаться на `Assembly-CSharp`. Поэтому:

- сервисы находят `YG.YG2` и мосты по имени типа среди загруженных сборок;
- мосты должны компилироваться в `Assembly-CSharp`, чтобы видеть PluginYG2 напрямую. Но скрипты пакета без `.asmdef` Unity в сборки игры **не берёт** (проверено на стенде: код из папки пакета без `.asmdef` в `Assembly-CSharp` не попадает). Поэтому исходники мостов лежат в пакете в папке `Runtime/YandexBridge~` (тильда: Unity её не импортирует), а редакторный установщик `YgCore.Editor.YandexBridgeInstaller` копирует их вместе с `.jslib` в `Assets/YgCore/YandexBridge` игры. Копия обновляется сама при смене версии пакета или содержимого мостов; пункт меню «YgCore → Переустановить мосты Яндекса» делает это вручную. **Закоммитьте `Assets/YgCore/YandexBridge` в репозиторий игры.**
- каждый файл моста обёрнут в `#if PLUGIN_YG_2` (символ, который ставит сам PluginYG2), чтобы пакет не ломал проект без плагина, и в условие нужного модуля плагина (`Payments_yg`, `Storage_yg`).

> **История.** Первая версия пакета рассчитывала, что папка без `.asmdef` попадёт в `Assembly-CSharp` (DEC-015). Первая сборка стенда это опровергла: все сервисы Яндекса были недоступны, а загрузочный экран бесконечно ждал сигнала готовности. Исправлено установщиком (DEC-022).

## Сервисы

| Сервис | Контракт | Условие в фабрике (WebGL) | Через что работает | Таймауты |
|---|---|---|---|---|
| `PluginYG2AdsService` | `IAdsService` | `RewardedAdv_yg` или `InterstitialAdv_yg` | `YgSdkBridge` | 3 минуты на показ |
| `PluginYG2AuthorizationService` | `IAuthorizationService` | `Authorization_yg` | `YG.YG2` напрямую и `YgSdkBridge.AuthDialogClosed` | 30 секунд на вход |
| `PluginYG2PurchasesService` | `IPurchasesService` | `Payments_yg` | `YgPaymentsBridge` и `YG.YG2.purchases` | 20 с операция, 3 мин покупка |
| `PluginYG2SaveService` | `ISaveService` | `Storage_yg` | `YgStorageBridge` и `YG.YG2.saves` | 15 секунд |
| `PluginYG2LeaderboardService` | `ILeaderboardService` | `Leaderboards_yg` | `YgSdkBridge` | 15 секунд |
| `PluginYG2LocalizationService` | `ILocalizationService` | `Localization_yg` | `YG.YG2` напрямую | нет |
| `PluginYG2GameReadinessService` | `IGameReadinessService` | `YandexGamesPlatform_yg` | `YgSdkBridge` | нет |
| `PluginYG2GameplayLifecycleService` | `IGameplayLifecycleService` | `YandexGamesPlatform_yg` | `YgSdkBridge` | нет |

Все сервисы реализуют `IDisposable`: при выгрузке они отписываются от статических событий и завершают ожидающие операции статусом `PlatformUnavailable`.

Статусы результатов каждой операции описаны в [contracts.md](contracts.md).

## Что добавляют заплатки

### Сохранение (`YgStorageBridge` + `YgCoreStorage.jslib`)

Штатное сохранение PluginYG2 не ждёт ответа Яндекса и превращает сбой загрузки в пустое сохранение (подробности в `Docs/DECISIONS.md`, обоснование DEC-009). Заплатка:

- каждый запрос получает идентификатор, ответ ищется по нему: запоздавший ответ не подтвердит другую запись;
- запись идёт через очередь на стороне JS: старая запись не завершится после новой;
- `SaveAsync` возвращает `Success` только после подтверждения Яндекса, сбой загрузки возвращается как `Error`;
- ответ читается без обработки обратных слешей (штатный разбор портит вложенный JSON);
- формат конверта PluginYG2 не меняется, данные игры лежат в списке `ygCoreStorage` внутри `YG2.saves`.

### Покупки (`YgPaymentsBridge` + `YgCorePayments.jslib`)

Штатный PluginYG2 гасит покупку сразу после оплаты, до выдачи награды. Заплатка гасит её только по явному `ConsumeAsync(token)`. Схема из двух шагов описана в [contracts.md](contracts.md).

## Подключение

Сервисы создаются через фабрики (модуль Factories). Для сохранения можно указать имя поля:

```csharp
using YgCore.Factories;
using YgCore.Localization;

// Каталог строк игры.
ILocalizationCatalog catalog = /* см. modules/localization.md */;

var ads = AdsServiceFactory.Create();
var save = SaveServiceFactory.Create();
var purchases = PurchasesServiceFactory.Create();
var localization = LocalizationServiceFactory.Create(catalog);
```

Что нужно проекту (подробнее в [install.md](../install.md)):

1. PluginYG2 v2.0092 с нужными модулями.
2. Define-символы PluginYG2 для WebGL (`PLUGIN_YG_2`, `YandexGamesPlatform_yg`, `Storage_yg`, `Payments_yg` и т.д.).
3. Рекомендуемая доработка PluginYG2: [patches/pluginyg2-auth-dialog-closed.md](../patches/pluginyg2-auth-dialog-closed.md).
4. Страница игры (WebGL-шаблон) по желанию определяет `window.YgLoading.gameReady()`: мост вызывает её, когда игра готова (убрать загрузочный экран).

### Имя поля сохранения и существующие игры

Данные игры лежат в `YG2.saves` под именем поля `ygCoreStorage`. Это имя попадает в облачный JSON игрока, поэтому **менять его после выпуска игры нельзя**: прогресс перестанет находиться.

Игра, у которой уже есть сохранения игроков под другим именем (например, «Обби Потоп» с полем `obbyPotopStorage`), оставляет своё имя:

```csharp
// Игра сама объявляет поле (partial SavesYG в Assembly-CSharp) и передаёт его имя фабрике.
var save = SaveServiceFactory.Create(storageFieldName: "obbyPotopStorage");
```

В этом случае пакетное поле `ygCoreStorage` остаётся пустым в JSON и ни на что не влияет.

## Известные особенности

- **`PurchaseProduct.IsConsumable` из каталога PluginYG2 всегда `false`.** PluginYG2 не сообщает тип товара. Какой товар расходуемый, знает игра (каталог магазина).
- **Неудачная запись остаётся в памяти.** При `Error` от `SaveAsync` данные уже лежат в `YG2.saves` в памяти (но не в облаке). Следующая успешная запись отправит их.
- **`DeleteAsync` всегда выполняет запись**, даже если записи уже не было: предыдущая неудачная попытка могла убрать её из памяти, но не из облака.
- **Реклама:** `ShowInterstitialAsync` возвращает `Denied`, если не прошёл интервал между показами PluginYG2.
- **Обрезка кода (IL2CPP stripping).** Сервисы обращаются к PluginYG2 по имени через рефлексию. На высоком уровне обрезки необходимые члены `YG.YG2` могут быть удалены. В «Обби» уровень обрезки для WebGL отдельно не менялся (по умолчанию). Если включаете обрезку выше, проверьте на стенде и при необходимости добавьте `link.xml`.

## Чего делать нельзя

- Вызывать `YG.YG2` и мосты из игрового кода: только через контракты.
- Менять имя поля сохранения после выпуска игры.
- Убирать `#if PLUGIN_YG_2` из мостов: проект без плагина перестанет собираться.
- Класть `.asmdef` в `Assets/YgCore/YandexBridge`: мосты перестанут видеть PluginYG2.
- Править файлы в `Assets/YgCore/YandexBridge` руками: установщик перезапишет их при обновлении пакета.
- Вызывать `ConsumeAsync` до выдачи и сохранения награды.

## Как проверить

Без Unity (запуск командами из корня репозитория):

| Команда | Что проверяет |
|---|---|
| `dotnet run --project Tools/YandexChecks` | 47 проверок: реальные мосты и сервисы с заглушкой PluginYG2: реклама (успех, отказ, ошибка, события), лидерборд, вход (успех, отмена, повторный вызов), готовность и геймплей (очередь до SDK), язык, сбои сохранения, фабрики |
| `dotnet build Tools/YandexChecks -p:WebGLCompile=true` | Компиляция ветки WebGL (внешние функции `DllImport`) |
| `python3 Tools/check-jslib.py` | Имена функций и сообщений в C# и `.jslib` совпадают |

**Не проверено:** работа с настоящим PluginYG2, JavaScript-сторона покупок и хранилища, успешные покупки и сохранение, поведение в `Assembly-CSharp`. Это проверяет стенд на Яндексе (секции «Покупки», «Сохранение», «Авторизация», «Реклама» в `Docs/STAND_CHECKLIST.md`).

## Отличия от «Обби Потоп»

- Пространства имён `YgCore.Yandex` и `YgCore.YandexBridge` вместо `ObbyPotop.PlatformServices.*`.
- Переименованы внутренние имена: мосты `YgSdkBridge`, `YgPaymentsBridge`, `YgStorageBridge`; функции `.jslib` `YgCore*`; методы `YGSendMessage` `YgCore*`; JS-объект загрузочного экрана `window.YgLoading` (было `window.ObbyLoading`); сообщение о закрытии окна входа `YgCoreAuthDialogClosed` (было `ObbyAuthDialogClosed`).
- Поле сохранения по умолчанию `ygCoreStorage` (было `obbyPotopStorage`), имя можно передать в `SaveServiceFactory`.
- Мосты обёрнуты в `#if PLUGIN_YG_2`; `SavesYG`-расширение в `#if Storage_yg`.
- Мост хранилища игнорирует некорректный JSON в ответе (раньше бросал исключение).
- Сервис локализации принимает каталог строк игры (раньше использовал статический каталог).
- Общие приёмы рефлексии собраны в `PluginYG2BridgeReflection` (раньше копировались в каждом сервисе).
- Сообщения результатов и комментарии на русском.

Как перевести «Обби» на эти имена, см. `Docs/MIGRATION_FROM_OBBY.md` (раздел «Переключение Обби»).

## История

См. [CHANGELOG](../../CHANGELOG.md).
