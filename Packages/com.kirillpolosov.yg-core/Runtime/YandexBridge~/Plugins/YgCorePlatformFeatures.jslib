// JS-сторона возможностей платформы без модулей PluginYG2: оценка игры, ярлык, серверное время.
// Использует глобальный объект ysdk, который создаёт PluginYG2, и функцию YG2Instance для ответа в Unity.
// Методы SDK (по документации Яндекс Игр):
//   ysdk.feedback.canReview()      -> Promise<{ value: boolean, reason?: string }>
//   ysdk.feedback.requestReview()  -> Promise<{ feedbackSent: boolean }>
//   ysdk.shortcut.canShowPrompt()  -> Promise<{ canShow: boolean }>
//   ysdk.shortcut.showPrompt()     -> Promise<{ outcome: 'accepted' | 'rejected' }>
//   ysdk.serverTime()              -> миллисекунды Unix (значение или Promise); в официальной документации
//                                    не найден, поэтому наличие функции проверяется, а её отсутствие не ошибка.
// Все ответы приходят одним сообщением YgCoreFeatureReply с JSON: { id, ok, supported, flag, number, reason, error }.
mergeInto(LibraryManager.library, {
    // Есть ли у SDK нужная функция (1 или 0). Проверка без вызова, чтобы IsPlatformAvailable был честным.
    YgCoreFeatureSupported: function(featurePointer) {
        var feature = UTF8ToString(featurePointer);
        if (typeof ysdk === 'undefined' || ysdk === null) {
            return 0;
        }

        var supported = false;
        if (feature === 'canReview') {
            supported = !!(ysdk.feedback && typeof ysdk.feedback.canReview === 'function');
        } else if (feature === 'requestReview') {
            supported = !!(ysdk.feedback && typeof ysdk.feedback.requestReview === 'function');
        } else if (feature === 'canShowPrompt') {
            supported = !!(ysdk.shortcut && typeof ysdk.shortcut.canShowPrompt === 'function');
        } else if (feature === 'showPrompt') {
            supported = !!(ysdk.shortcut && typeof ysdk.shortcut.showPrompt === 'function');
        } else if (feature === 'serverTime') {
            supported = typeof ysdk.serverTime === 'function';
        }

        return supported ? 1 : 0;
    },

    // Вызов возможности. Ответ всегда один: YgCoreFeatureReply с тем же id, что передан в запросе.
    YgCoreFeatureCall: function(idPointer, featurePointer) {
        var id = UTF8ToString(idPointer);
        var feature = UTF8ToString(featurePointer);
        var answer = function(fields) {
            fields.id = id;
            YG2Instance('YgCoreFeatureReply', JSON.stringify(fields));
        };
        var fail = function(error) {
            answer({
                ok: false,
                supported: true,
                error: error && error.message ? error.message : 'Вызов SDK завершился ошибкой.'
            });
        };
        var returnFocus = function() {
            // После окна платформы возвращаем фокус игре, как это делает PluginYG2.
            if (typeof FocusGame === 'function') FocusGame();
        };

        if (typeof ysdk === 'undefined' || ysdk === null) {
            answer({ ok: false, supported: false, error: 'SDK не готов.' });
            return;
        }

        try {
            if (feature === 'canReview' && ysdk.feedback && typeof ysdk.feedback.canReview === 'function') {
                ysdk.feedback.canReview().then(function(result) {
                    answer({ ok: true, supported: true, flag: !!(result && result.value), reason: (result && result.reason) || '' });
                }).catch(fail);
            } else if (feature === 'requestReview' && ysdk.feedback && typeof ysdk.feedback.requestReview === 'function') {
                ysdk.feedback.requestReview().then(function(result) {
                    answer({ ok: true, supported: true, flag: !!(result && result.feedbackSent) });
                    returnFocus();
                }).catch(function(error) {
                    fail(error);
                    returnFocus();
                });
            } else if (feature === 'canShowPrompt' && ysdk.shortcut && typeof ysdk.shortcut.canShowPrompt === 'function') {
                ysdk.shortcut.canShowPrompt().then(function(result) {
                    answer({ ok: true, supported: true, flag: !!(result && result.canShow) });
                }).catch(fail);
            } else if (feature === 'showPrompt' && ysdk.shortcut && typeof ysdk.shortcut.showPrompt === 'function') {
                ysdk.shortcut.showPrompt().then(function(result) {
                    answer({ ok: true, supported: true, flag: !!(result && result.outcome === 'accepted') });
                    returnFocus();
                }).catch(function(error) {
                    fail(error);
                    returnFocus();
                });
            } else if (feature === 'serverTime' && typeof ysdk.serverTime === 'function') {
                // Значение может прийти сразу или через Promise: Promise.resolve принимает оба варианта.
                Promise.resolve(ysdk.serverTime()).then(function(value) {
                    var milliseconds = Number(value);
                    if (!isFinite(milliseconds) || milliseconds <= 0) {
                        answer({ ok: false, supported: true, error: 'SDK вернул некорректное серверное время.' });
                        return;
                    }

                    answer({ ok: true, supported: true, number: milliseconds });
                }).catch(fail);
            } else {
                answer({ ok: false, supported: false, error: 'SDK не поддерживает: ' + feature });
            }
        } catch (error) {
            fail(error);
        }
    }
});
