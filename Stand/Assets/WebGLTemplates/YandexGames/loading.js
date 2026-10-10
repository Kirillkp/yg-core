// Только интерфейс загрузки. Инициализация SDK и сигнал готовности остаются за PluginYG2.
// Пакет yg-core вызывает window.YgLoading.gameReady(), когда игра сообщает платформе о готовности.
(function () {
    'use strict';

    const root = document.getElementById('loading-cover');
    if (!root) return;
    const label = document.getElementById('yg-loading-label');
    const value = document.getElementById('yg-loading-value');
    const track = document.getElementById('unity-progress-bar-empty');
    const fill = document.getElementById('unity-progress-bar-full');
    const spinner = root.querySelector('.spinner');
    const retry = document.getElementById('yg-loading-retry');
    const strings = {
        ru: { waiting: 'Подготавливаем игру…', loading: 'Загрузка игры', starting: 'Запускаем игру…', ready: 'Готово!', error: 'Не удалось загрузить игру.', retry: 'Повторить' },
        en: { waiting: 'Preparing game…', loading: 'Loading game', starting: 'Starting game…', ready: 'Ready!', error: 'Unable to load the game.', retry: 'Retry' }
    };
    let language = 'en';
    let stage = 'waiting';
    let progress = 0;
    let runtimeCreated = false;
    let gameReady = false;
    let waitForGameReady = true;
    let completed = false;

    function render() {
        const copy = strings[language];
        const percentage = completed ? 100 : Math.min(99, Math.floor(progress * 100));
        root.dataset.stage = stage;
        document.documentElement.lang = language;
        label.textContent = stage === 'error' ? copy.error : copy[stage];
        value.textContent = stage === 'waiting' || stage === 'error' ? '' : percentage + '%';
        retry.textContent = copy.retry;
        retry.hidden = stage !== 'error';
        if (stage === 'waiting' || stage === 'error') track.removeAttribute('aria-valuenow');
        else track.setAttribute('aria-valuenow', String(percentage));
        fill.style.width = percentage + '%';
        spinner.style.display = stage === 'waiting' || stage === 'starting' ? '' : 'none';
    }

    function setLanguage(code) {
        const normalized = String(code || '').trim().toLowerCase().split(/[-_]/)[0];
        language = normalized === 'ru' ? 'ru' : 'en';
        render();
    }

    function finishIfReady() {
        if (completed || stage === 'error' || !runtimeCreated || (waitForGameReady && !gameReady)) return;
        completed = true;
        stage = 'ready';
        render();
        root.setAttribute('aria-hidden', 'true');
        root.hidden = true;
    }

    window.YgLoading = {
        setLanguage,
        progress(amount) {
            if (completed || stage === 'error') return;
            if (Number.isFinite(amount)) progress = Math.max(progress, Math.min(1, Math.max(0, amount)));
            stage = progress >= 1 ? 'starting' : 'loading';
            render();
        },
        runtimeReady(shouldWaitForGameReady) {
            runtimeCreated = true;
            waitForGameReady = waitForGameReady && shouldWaitForGameReady;
            if (!completed && stage !== 'error') { stage = 'starting'; render(); }
            finishIfReady();
        },
        gameReady() { gameReady = true; finishIfReady(); },
        allowFallback() { waitForGameReady = false; finishIfReady(); },
        fail() { if (!completed) { stage = 'error'; render(); } }
    };

    retry.addEventListener('click', () => window.location.reload());
    // Язык SDK заменит эту подсказку, как только YaGames.init завершится.
    const urlLanguage = new URLSearchParams(window.location.search).get('lang');
    setLanguage(urlLanguage || navigator.language || 'en');
}());
