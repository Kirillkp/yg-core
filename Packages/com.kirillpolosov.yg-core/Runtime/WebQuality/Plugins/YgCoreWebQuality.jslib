// Определяет, мобильное ли устройство (1 или 0), для выбора уровня качества графики.
// Если страница игры (WebGL-шаблон) определила объект window.YgWebQuality со свойством isMobile,
// берётся оно: страница знает лучше (например, учитывает размер экрана). Иначе работает запасной вариант
// по строке браузера. Компьютеры с сенсорным экраном телефонами не считаются.
mergeInto(LibraryManager.library, {
    YgCoreWebIsMobile: function () {
        if (window.YgWebQuality) {
            return window.YgWebQuality.isMobile ? 1 : 0;
        }

        // iPad с современным Safari притворяется Macintosh, но у него есть несколько точек касания.
        var mobile = /Android|iPhone|iPad|iPod/i.test(navigator.userAgent)
            || (/Macintosh/i.test(navigator.userAgent) && navigator.maxTouchPoints > 1);
        return mobile ? 1 : 0;
    }
});
