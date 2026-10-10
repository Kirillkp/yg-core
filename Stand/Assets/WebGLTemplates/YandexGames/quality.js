// Качество веб-страницы: определение телефона и ограничение разрешения. Читает пакет yg-core (YgCoreWebQuality.jslib).
(function () {
    "use strict";
    // iPadOS может выдавать себя за настольный Mac, поэтому проверяем ещё и касания.
    const isMobile = /Android|iPhone|iPad|iPod/i.test(navigator.userAgent)
        || (/Macintosh/i.test(navigator.userAgent) && navigator.maxTouchPoints > 1);
    const desktopPixelRatioLimit = 1.5;
    const mobilePixelRatioLimit = 1.75;
    const mobileCanvasPixelBudget = 2000000;

    window.YgWebQuality = Object.freeze({
        isMobile: isMobile,
        getDevicePixelRatio: function () {
            const nativeRatio = Number(window.devicePixelRatio);
            const ratio = Number.isFinite(nativeRatio) && nativeRatio > 0 ? nativeRatio : 1;
            if (!isMobile) {
                return Math.min(ratio, desktopPixelRatioLimit);
            }
            // Чёткий экран телефона без буфера кадра в родном разрешении планшета.
            const cssPixels = Number(window.innerWidth) * Number(window.innerHeight);
            const budgetRatio = Number.isFinite(cssPixels) && cssPixels > 0
                ? Math.sqrt(mobileCanvasPixelBudget / cssPixels) : mobilePixelRatioLimit;
            return Math.min(ratio, mobilePixelRatioLimit, budgetRatio);
        }
    });
})();
