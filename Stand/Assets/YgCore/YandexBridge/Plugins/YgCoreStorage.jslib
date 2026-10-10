// JS-сторона хранилища с подтверждением. Использует объект player, который создаёт PluginYG2,
// и функцию YG2Instance для ответа в Unity (метод YgCoreStorageCompleted).
// Ответ всегда содержит id запроса: на стороне Unity по нему находится ожидающая операция.
mergeInto(LibraryManager.library, {
    $YgCoreStorage: {
        // Цепочка записей: каждая следующая запись начинается только после завершения предыдущей.
        saveQueue: null,

        // Отправляет ответ в Unity: payload при успехе или error при сбое.
        complete: function(id, payload, error) {
            YG2Instance('YgCoreStorageCompleted', JSON.stringify({
                id: id,
                payload: payload || '',
                error: error ? (error.message || String(error)) : ''
            }));
        }
    },

    YgCoreStorageSave__deps: ['$YgCoreStorage'],
    YgCoreStorageSave: function(idPointer, payloadPointer) {
        var id = UTF8ToString(idPointer);
        var payload = UTF8ToString(payloadPointer);
        var target = typeof player !== 'undefined' ? player : null;

        // Записи идут в строгом порядке, даже если предыдущая по таймауту уже «потеряна» на стороне Unity:
        // старая запись не может завершиться позже новой.
        YgCoreStorage.saveQueue = (YgCoreStorage.saveQueue || Promise.resolve()).then(function() {
            if (!target) throw new Error('Игрок для хранилища недоступен.');
            return target.setData({ saves: [payload] }, true);
        }).then(function() {
            YgCoreStorage.complete(id, '', null);
        }, function(error) {
            YgCoreStorage.complete(id, '', error);
        });
    },

    YgCoreStorageLoad__deps: ['$YgCoreStorage'],
    YgCoreStorageLoad: function(idPointer) {
        var id = UTF8ToString(idPointer);
        try {
            if (typeof player === 'undefined' || !player)
                throw new Error('Игрок для хранилища недоступен.');
            Promise.resolve(player.getData(['saves'])).then(function(data) {
                var saves = data && data.saves;

                // Данных нет вообще: допустимо (новый игрок), возвращаем пустой ответ.
                if (saves == null) {
                    YgCoreStorage.complete(id, '', null);
                    return;
                }

                // Конверт PluginYG2: массив ровно из одной строки с JSON.
                if (!Array.isArray(saves) || saves.length !== 1 || typeof saves[0] !== 'string')
                    throw new Error('Неверный формат конверта хранилища.');

                // Проверяем, что внутри объект JSON, но возвращаем исходную строку без изменений:
                // обработка обратных слешей в старом PluginYG2 портит вложенный JSON.
                var parsed = JSON.parse(saves[0]);
                if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed))
                    throw new Error('Данные хранилища повреждены.');
                YgCoreStorage.complete(id, saves[0], null);
            }).catch(function(error) {
                YgCoreStorage.complete(id, '', error);
            });
        } catch (error) {
            YgCoreStorage.complete(id, '', error);
        }
    }
});
