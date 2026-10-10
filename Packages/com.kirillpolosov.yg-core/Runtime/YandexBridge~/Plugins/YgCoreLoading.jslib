// Сообщает загрузочному экрану страницы, что игра готова. Страница (WebGL-шаблон игры) может
// определить объект window.YgLoading с функцией gameReady(). Если объекта нет, ничего не происходит.
mergeInto(LibraryManager.library, {
    YgCoreLoadingGameReady_js: function () {
        if (window.YgLoading && typeof window.YgLoading.gameReady === 'function') {
            window.YgLoading.gameReady();
        }
    }
});
