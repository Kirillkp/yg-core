# Доработка: сообщение о закрытии окна входа

**Файл плагина:** `Assets/PluginYourGames/Modules/Authorization/Scripts/Editor/CopyCode/Auth_js.js`
**Версия PluginYG2:** v2.0092 (Authorization)
**Нужна:** рекомендуется. Без неё всё работает, но отмена входа определяется по таймауту.

## Зачем

Когда игрок открывает окно входа и закрывает его, не войдя, PluginYG2 не сообщает об этом игре. Сервис авторизации не может отличить «игрок отказался» от «ответа ещё нет» и ждёт 30 секунд, после чего возвращает `Error`. С доработкой JS-функция `OpenAuthDialog` в любом исходе (вошли, отменили, ошибка) отправляет сообщение `YgCoreAuthDialogClosed`, и сервис сразу возвращает `Success` или `Denied`.

Принимает сообщение метод `YgCoreAuthDialogClosed` класса `YGSendMessage` (файл `YgSdkBridge.cs` пакета).

## Что изменить

В функции `OpenAuthDialog` добавьте два фрагмента (остальной код функции не меняется).

В начале, в ветке «SDK нет»:

```javascript
async function OpenAuthDialog() {
    if (!ysdk) {
        LogStyledMessage('OpenAuthDialog: ysdk is null');
        YG2Instance('YgCoreAuthDialogClosed');   // добавить
        return;
    }
```

В конце функции добавьте блок `finally` после последнего `catch`:

```javascript
    } catch (e) {
        player = null;
        await InitPlayer();
        LogStyledMessage('CRASH OpenAuthDialog / getPlayer:', e?.message ?? e);
    } finally {
        // Сообщаем игре, что окно закрыто: при входе, отмене и ошибке.
        YG2Instance('YgCoreAuthDialogClosed');    // добавить
    }
}
```

## Если игра уже использует прежнее имя

В «Обби Потоп» эта доработка сделана с именем `ObbyAuthDialogClosed`. При переходе «Обби» на пакет замените имя на `YgCoreAuthDialogClosed` в этом файле (два места).

## Как проверить

На стенде, секция «Авторизация»: открыть окно входа и закрыть его без входа. Ожидаемый результат: сразу `Denied`, а не `Error` через 30 секунд.
