# Модуль: Локализация

Сборки: `YgCore.Localization` и необязательная `YgCore.Localization.UI` · Пространства имён: `YgCore.Localization`, `YgCore.Localization.UI` · С версии: `v0.1.0` (пока в разделе Unreleased) · Статус: перенесён из «Обби Потоп» (каталог строк и подстановка в надписи; определение языка платформы живёт в Яндекс-модуле)

`YgCore.Localization` не зависит от других сборок. `YgCore.Localization.UI` подключается сама только если в проекте есть uGUI.

## Зачем нужен

Игра хранит свои тексты на нескольких языках, а пакет (моки, заглушки, реализация на PluginYG2) должен уметь выдавать эти строки, не зная их содержимого. Для этого между ними стоит интерфейс каталога строк. Тексты остаются в игре, механизм в пакете.

## Когда использовать

- Нужны строки на двух и более языках с запасным языком.
- Нужно привести код языка от платформы (`ru-RU`, `EN_us`) к поддерживаемому.

## Когда не использовать

- Тексты конкретной игры в пакет не кладите: каталог создаёт игра.
- Подставлять строки в надписи на экране автоматически (`LocalizedTextView`) умеет необязательная сборка `YgCore.Localization.UI`, но только для uGUI `Text`. Для TextMeshPro и UI Toolkit подставляйте строки сами: `localization.Get(ключ)`.

## Подключение

Игра собирает таблицы и создаёт каталог один раз, затем передаёт его сервисам локализации.

```csharp
using System.Collections.Generic;
using YgCore.Localization;

// Таблицы: язык -> (ключ -> текст). Тексты принадлежат игре.
var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>
{
    ["en"] = new Dictionary<string, string> { ["main.play"] = "PLAY", ["unit.meters"] = "{0} m" },
    ["ru"] = new Dictionary<string, string> { ["main.play"] = "ИГРАТЬ", ["unit.meters"] = "{0} м" },
};

// Язык по умолчанию обязателен и должен иметь таблицу.
ILocalizationCatalog catalog = new DictionaryLocalizationCatalog("en", tables);

catalog.NormalizeLanguage("ru-RU");        // "ru"
catalog.Get("ru", "unit.meters", 120);     // "120 м"
```

## Подстановка строк в надписи (uGUI)

Если в проекте есть uGUI, сборка `YgCore.Localization.UI` даёт компонент `LocalizedTextView`: он ставит в uGUI `Text` строку по ключу и сам обновляет её при смене языка.

1. Повесьте `LocalizedTextView` на корень экрана.
2. В инспекторе добавьте пары «надпись (`Text`) и ключ строки».
3. В коде экрана один раз вызовите:

```csharp
using YgCore.Localization.UI;

// Подключает все компоненты LocalizedTextView внутри экрана, включая неактивные.
LocalizedTextView.InitializeChildren(this, localizationService);
```

Пустые ключи и пустые надписи пропускаются. Повторный вызов с другим сервисом переподключает компонент и снимает подписку со старого. При выгрузке подписка снимается.

## Публичный API

| Что | Назначение | Замечания |
|---|---|---|
| `ILocalizationCatalog.DefaultLanguage` | Язык по умолчанию | |
| `ILocalizationCatalog.NormalizeLanguage(language)` | Приводит код языка к поддерживаемому | Берёт часть до `-` или `_`, переводит в нижний регистр. Неизвестный и пустой дают язык по умолчанию |
| `ILocalizationCatalog.Get(language, key, args)` | Строка по ключу | Нет ключа на языке: берётся язык по умолчанию. Нет и там: возвращается сам ключ. Пустой ключ: пустая строка. Аргументы подставляются как в `string.Format` (культура инвариантная) |
| `DictionaryLocalizationCatalog(defaultLanguage, tables)` | Каталог на словарях | Бросает `ArgumentException`, если для языка по умолчанию нет таблицы |
| `LocalizedTextView.Initialize(service)`, `InitializeChildren(root, service)` | Строки в uGUI `Text` (сборка `YgCore.Localization.UI`) | Обновляется при смене языка |

## События и ошибки

| Ситуация | Результат |
|---|---|
| Ключа нет нигде | Возвращается ключ. На экране виден пропуск, игра не падает |
| Число аргументов не совпадает с форматом строки | `FormatException` из `string.Format`: проверяйте строки |
| `DictionaryLocalizationCatalog` создан без таблицы для языка по умолчанию | `ArgumentException` при создании |

## Чего делать нельзя

- Класть тексты игры в пакет.
- Пользоваться строкой `ru`/`en` напрямую в логике: язык берите из `ILocalizationService.CurrentLanguage`.

## Как проверить

Без Unity: `dotnet run --project Tools/CoreChecks` (нормализация, запасной язык, аргументы, ошибки; для `LocalizedTextView` подстановка, обновление при смене языка, переподключение, снятие подписок). На стенде (секция «Язык»). **Не проверено в Unity:** сам компонент на настоящем uGUI и включение необязательной сборки по `versionDefines`.

## Отличия от «Обби Потоп»

`LocalizedTextView` перенесён без изменений поведения (пространство имён `YgCore.Localization.UI`, комментарии на русском). В «Обби» был статический класс `LocalizationCatalog` с зашитыми словарями ru/en. Теперь механизм вынесен в `DictionaryLocalizationCatalog`, а тексты остаются в игре. При двух языках (ru и en) и языке по умолчанию en результат тот же: другой язык заменяется на en, на русском берутся русские строки, при отсутствии ключа используется английская строка. Как перевести «Обби» на новый каталог, см. `Docs/MIGRATION_FROM_OBBY.md`.

## История

См. [CHANGELOG](../../CHANGELOG.md).
