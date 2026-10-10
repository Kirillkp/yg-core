# Индекс документации yg-core

Читайте только то, что нужно для задачи. Колонка «Статус» показывает, перенесён ли модуль из «Обби Потоп».

## Если нужно…

| Задача | Читать |
|---|---|
| Понять, что это за пакет и как он устроен | [architecture.md](architecture.md) |
| Подключить пакет в игру или обновить версию | [install.md](install.md) |
| Узнать, какие версии Unity, PluginYG2 и схемы сохранения поддерживаются | [compatibility.md](compatibility.md) |
| Узнать, что изменилось в версии | [../CHANGELOG.md](../CHANGELOG.md), затем [versions/](versions/) |
| Написать новый модульный документ | [modules/_TEMPLATE.md](modules/_TEMPLATE.md) |
| Доработать PluginYG2 (правки его файлов) | [patches/](patches/README.md) |
| Оформить новую версию | [versions/_TEMPLATE.md](versions/_TEMPLATE.md) и `Docs/RELEASE_PROCESS.md` в корне репозитория |
| Выполнить типовую операцию (рекламу, товар, поле сохранения) | [recipes/](recipes/) |

## Модули

| Модуль | Документ | Статус |
|---|---|---|
| Core (состояния, пауза, экономика, настройки) | [modules/core.md](modules/core.md) | перенесён, не выпущен |
| Контракты платформы | [modules/contracts.md](modules/contracts.md) | перенесён, не выпущен |
| Сохранения и прогресс | [modules/save.md](modules/save.md) | перенесён, не выпущен |
| Магазин (логика) | [modules/store.md](modules/store.md) | перенесён, не выпущен |
| Аудио | [modules/audio.md](modules/audio.md) | перенесён, не выпущен |
| Локализация (каталог строк, подстановка в надписи) | [modules/localization.md](modules/localization.md) | перенесён, не выпущен |
| Web-качество | [modules/web-quality.md](modules/web-quality.md) | перенесён, не выпущен |
| Фабрики и заглушки | [modules/factories.md](modules/factories.md) | перенесён, не выпущен |
| Моки редактора | [modules/mocks.md](modules/mocks.md) | перенесён, не выпущен |
| Яндекс-реализация и заплатки PluginYG2 | [modules/yandex.md](modules/yandex.md) | перенесён, не проверен в Unity |

Файл модуля появляется вместе с переносом кода. Строка статуса обновляется в том же изменении.

## Рецепты

Папка [recipes/](recipes/) пока пуста. Рецепты добавляются вместе с модулями, к которым относятся (например, «показать rewarded-рекламу», «добавить товар», «добавить поле в сохранение»).

## Как устроены документы

- Модульный документ всегда в одном формате: [modules/_TEMPLATE.md](modules/_TEMPLATE.md).
- Запись версии всегда в одном формате: [versions/_TEMPLATE.md](versions/_TEMPLATE.md).
- Документы короткие и однозначные: агенту читать их целиком дешевле, чем угадывать.
