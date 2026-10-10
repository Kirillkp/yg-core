// Проверки модулей WebQuality и LocalizedTextView без Unity: правила выбора качества, настройки по умолчанию
// и настоящий код LocalizedTextView на заглушках Unity.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using YgCore.Localization;
using YgCore.Localization.UI;
using YgCore.Mocks;
using YgCore.WebQuality;

static class WebAndTextChecks
{
    // Сервис локализации со счётчиком подписчиков на смену языка: так видно утечку подписки, которую не видно по тексту.
    private sealed class CountingLocalization : YgCore.Contracts.ILocalizationService
    {
        private event EventHandler<YgCore.Contracts.LanguageChangedEventArgs> languageChanged;

        public int Subscribers { get; private set; }

        public event EventHandler<YgCore.Contracts.LanguageChangedEventArgs> LanguageChanged
        {
            add { languageChanged += value; Subscribers++; }
            remove { languageChanged -= value; Subscribers--; }
        }

        public bool IsPlatformAvailable => false;
        public string CurrentLanguage => "en";
        public System.Threading.Tasks.Task<YgCore.Contracts.PlatformServiceResult<string>> DetectLanguageAsync() =>
            System.Threading.Tasks.Task.FromResult(YgCore.Contracts.PlatformServiceResult<string>.Success("en"));
        public string Get(string key, params object[] arguments) => key;
        public void SetLanguage(string language) { }
    }

    private static void SetField(object target, Type type, string name, object value)
    {
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }

    public static void Run(Action<string, bool> check)
    {
        // --- Веб-качество: частота кадров
        check("веб-качество: положительная частота кадров сохраняется", WebQualityPolicy.NormalizeFrameRate(30) == 30 && WebQualityPolicy.NormalizeFrameRate(60) == 60);
        check("веб-качество: ноль и минус превращаются в -1 (решает браузер)", WebQualityPolicy.NormalizeFrameRate(0) == -1 && WebQualityPolicy.NormalizeFrameRate(-1) == -1 && WebQualityPolicy.NormalizeFrameRate(-30) == -1);

        // --- Веб-качество: выбор уровня
        var names = new[] { "Low", "Web Desktop", "Web Mobile" };
        check("веб-качество: компьютер получает свой уровень", WebQualityPolicy.FindQualityIndex(names, false, "Web Desktop", "Web Mobile") == 1);
        check("веб-качество: телефон получает свой уровень", WebQualityPolicy.FindQualityIndex(names, true, "Web Desktop", "Web Mobile") == 2);
        check("веб-качество: нет такого уровня в сборке -> -1", WebQualityPolicy.FindQualityIndex(names, false, "Ultra", "Web Mobile") == -1);
        check("веб-качество: пустое имя или нет списка -> -1", WebQualityPolicy.FindQualityIndex(names, true, "Web Desktop", "") == -1 && WebQualityPolicy.FindQualityIndex(null, false, "Web Desktop", "x") == -1);
        check("веб-качество: имена уровней чувствительны к регистру (как в Unity)", WebQualityPolicy.FindQualityIndex(names, false, "web desktop", "x") == -1);

        // --- Веб-качество: настройки по умолчанию
        var defaults = new WebQualitySettings();
        check("веб-качество: значения по умолчанию (Web Desktop, Web Mobile, кадры решает браузер)",
            defaults.DesktopQualityName == "Web Desktop" && defaults.MobileQualityName == "Web Mobile" && defaults.TargetFrameRate == -1);
        SetField(defaults, typeof(WebQualitySettings), "targetFrameRate", 30);
        check("веб-качество: заданная частота кадров доходит до Unity", defaults.TargetFrameRate == 30);
        SetField(defaults, typeof(WebQualitySettings), "targetFrameRate", 0);
        check("веб-качество: нулевая частота кадров нормализуется", defaults.TargetFrameRate == -1);
        check("веб-качество: имя файла настроек в Resources", WebQualityPolicy.ResourceName == "WebQualitySettings");

        // --- Подстановка строк в надписи
        var en = new Dictionary<string, string> { ["main.play"] = "PLAY", ["main.shop"] = "SHOP" };
        var ru = new Dictionary<string, string> { ["main.play"] = "ИГРАТЬ", ["main.shop"] = "МАГАЗИН" };
        var catalog = new DictionaryLocalizationCatalog("en",
            new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en"] = en, ["ru"] = ru });
        var service = new EditorMockLocalizationService(catalog, "en");

        Text NewText() => new GameObject("t").AddComponent<Text>();
        var root = new GameObject("Screen");
        var child = new GameObject("Child");
        child.transform.SetParent(root.transform, false);
        var playText = NewText();
        var shopText = NewText();
        var skipText = NewText();
        skipText.text = "не трогать";
        var view = child.AddComponent<LocalizedTextView>();
        SetField(view, typeof(LocalizedTextView), "entries", new[]
        {
            new LocalizedTextView.Entry { target = playText, key = "main.play" },
            new LocalizedTextView.Entry { target = shopText, key = "main.shop" },
            new LocalizedTextView.Entry { target = skipText, key = " " },
            new LocalizedTextView.Entry { target = null, key = "main.play" },
            null
        });

        LocalizedTextView.InitializeChildren(root.transform, service);
        check("надписи: строки подставляются при инициализации", playText.text == "PLAY" && shopText.text == "SHOP");
        check("надписи: надпись включается", playText.enabled && shopText.enabled);
        check("надписи: пустой ключ, пустая цель и null пропускаются без ошибок", skipText.text == "не трогать" && !skipText.enabled);

        service.SetLanguage("ru");
        check("надписи: при смене языка обновляются", playText.text == "ИГРАТЬ" && shopText.text == "МАГАЗИН");

        var other = new EditorMockLocalizationService(catalog, "en");
        view.Initialize(other);
        check("надписи: повторная инициализация другим сервисом берёт его язык", playText.text == "PLAY");
        service.SetLanguage("en");
        service.SetLanguage("ru");
        check("надписи: старый сервис отписан и надписи больше не меняет", playText.text == "PLAY");
        other.SetLanguage("ru");
        check("надписи: новый сервис управляет надписями", playText.text == "ИГРАТЬ");

        view.Initialize(other);
        check("надписи: повторный вызов с тем же сервисом не ломает подписку", playText.text == "ИГРАТЬ");
        other.SetLanguage("en");
        check("надписи: после повторного вызова язык по-прежнему обновляется один раз", playText.text == "PLAY");

        typeof(LocalizedTextView).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
        other.SetLanguage("ru");
        check("надписи: после выгрузки подписка снята", playText.text == "PLAY");

        // Подписки считаем напрямую
        var firstCounter = new CountingLocalization();
        var secondCounter = new CountingLocalization();
        var countedView = new GameObject("v").AddComponent<LocalizedTextView>();
        countedView.Initialize(firstCounter);
        check("подписка: одна подписка на первый сервис", firstCounter.Subscribers == 1);
        countedView.Initialize(firstCounter);
        check("подписка: повторная инициализация тем же сервисом не добавляет подписку", firstCounter.Subscribers == 1);
        countedView.Initialize(secondCounter);
        check("подписка: при смене сервиса старая подписка снимается, новая добавляется", firstCounter.Subscribers == 0 && secondCounter.Subscribers == 1);
        typeof(LocalizedTextView).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(countedView, null);
        check("подписка: при выгрузке подписка снимается", secondCounter.Subscribers == 0);

        LocalizedTextView.InitializeChildren(null, service);
        check("надписи: InitializeChildren(null) безопасен", true);
        var withoutService = new GameObject("x").AddComponent<LocalizedTextView>();
        withoutService.Initialize(null);
        check("надписи: Initialize(null) безопасен", true);
    }
}
