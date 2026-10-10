using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YG;
using YgCore.Contracts;
using YgCore.Factories;
using YgCore.Localization;
using YgCore.Yandex;
using YgCore.YandexBridge;

// Проверки Яндекс-модуля вне Unity. Реальные мосты и сервисы работают с заглушкой PluginYG2 (см. Stubs.cs).
// Сервисы находят мост и YG.YG2 через рефлексию, как в игре.
static class P
{
    static int fails;

    static void Check(string name, bool ok)
    {
        Console.WriteLine((ok ? "ок   " : "FAIL ") + name);
        if (!ok) fails++;
    }

    // Подготовка: SDK «готов», игрок гость, хуки очищены.
    static void Ready()
    {
        YG2.ResetValues();
        YG2.isSDKEnabled = true;
        YG2.iPlatform = new object();
    }

    static int Main()
    {
        Ads();
        Leaderboard();
        Authorization();
        ReadinessAndLifecycle();
        LocalizationChecks();
        SaveAndPurchases();
        FactoriesChecks();

        Console.WriteLine(fails == 0 ? "\nВсе проверки пройдены." : $"\nПровалено: {fails}");
        return fails == 0 ? 0 : 1;
    }

    static void Ads()
    {
        Ready();
        using (var ads = new PluginYG2AdsService())
        {
            int started = 0, finished = 0;
            ads.AdStarted += (s, e) => started++;
            ads.AdFinished += (s, e) => finished++;

            YG2.OnRewardedAdvShow = id => { YG2.onOpenRewardedAdv(); YG2.onRewardAdv(id); YG2.onCloseRewardedAdv(); };
            var ok = ads.ShowRewardedAsync("revive").Result;
            Check("реклама с наградой: награда засчитана -> Success, события начала и конца", ok.IsSuccess && started == 1 && finished == 1);

            started = finished = 0;
            YG2.OnRewardedAdvShow = id => { YG2.onOpenRewardedAdv(); YG2.onCloseRewardedAdv(); };
            var noReward = ads.ShowRewardedAsync("revive").Result;
            Check("реклама с наградой: закрыли без награды -> Denied", noReward.Status == PlatformServiceStatus.Denied && started == 1 && finished == 1);

            YG2.OnRewardedAdvShow = id => { YG2.onOpenRewardedAdv(); YG2.onRewardAdv("другое_место"); YG2.onCloseRewardedAdv(); };
            Check("реклама с наградой: награда за другое место -> Denied", ads.ShowRewardedAsync("revive").Result.Status == PlatformServiceStatus.Denied);

            started = finished = 0;
            YG2.OnRewardedAdvShow = id => { YG2.onOpenRewardedAdv(); YG2.onErrorRewardedAdv(); };
            var failedAfterOpen = ads.ShowRewardedAsync("revive").Result;
            Check("реклама: ошибка после открытия -> Error и событие конца", failedAfterOpen.Status == PlatformServiceStatus.Error && started == 1 && finished == 1);

            started = finished = 0;
            YG2.OnRewardedAdvShow = id => { YG2.onErrorRewardedAdv(); };
            var failedBeforeOpen = ads.ShowRewardedAsync("revive").Result;
            Check("реклама: ошибка до открытия -> Error без событий", failedBeforeOpen.Status == PlatformServiceStatus.Error && started == 0 && finished == 0);

            YG2.nowAdsShow = true;
            Check("реклама: другая уже идёт -> Denied", ads.ShowRewardedAsync("revive").Result.Status == PlatformServiceStatus.Denied);
            YG2.nowAdsShow = false;

            YG2.OnInterstitialAdvShow = () => { YG2.onOpenInterAdv(); YG2.onCloseInterAdvWasShow(true); };
            Check("полноэкранная: показана -> Success", ads.ShowInterstitialAsync("break").Result.IsSuccess);
            YG2.OnInterstitialAdvShow = () => { YG2.onCloseInterAdvWasShow(false); };
            Check("полноэкранная: не показана -> Denied", ads.ShowInterstitialAsync("break").Result.Status == PlatformServiceStatus.Denied);
            YG2.isTimerAdvCompleted = false;
            Check("полноэкранная: интервал не прошёл -> Denied", ads.ShowInterstitialAsync("break").Result.Status == PlatformServiceStatus.Denied);
            YG2.isTimerAdvCompleted = true;
        }

        YG2.ResetValues();
        using (var ads = new PluginYG2AdsService())
        {
            Check("реклама: SDK не готов -> Unavailable", !ads.IsPlatformAvailable
                && ads.ShowRewardedAsync("x").Result.Status == PlatformServiceStatus.PlatformUnavailable);
        }
    }

    static void Leaderboard()
    {
        Ready();
        using (var lb = new PluginYG2LeaderboardService())
        {
            var guest = lb.SubmitScoreAsync("main", 10).Result;
            Check("лидерборд: гость не может отправить -> Denied", guest.Status == PlatformServiceStatus.Denied && YG2.SubmittedScores.Count == 0);

            YG2.player = new PlayerData { auth = true, id = "1", name = "N" };
            Check("лидерборд: вошедший -> Success", lb.SubmitScoreAsync("main", 10).Result.IsSuccess && YG2.SubmittedScores[0] == ("main", 10));
            lb.SubmitScoreAsync("main", long.MaxValue).Wait();
            lb.SubmitScoreAsync("main", -5).Wait();
            Check("лидерборд: очки ограничиваются диапазоном int",
                YG2.SubmittedScores[1].score == int.MaxValue && YG2.SubmittedScores[2].score == 0);
            Check("лидерборд: пустой идентификатор -> Error", lb.SubmitScoreAsync(" ", 1).Result.Status == PlatformServiceStatus.Error);

            YG2.OnGetLeaderboard = () =>
            {
                var data = new YG.Utils.LB.LBData();
                data.players.Add(new YG.Utils.LB.LBPlayerData { uniqueID = "a", name = "Аня", score = 500, rank = 1 });
                data.players.Add(new YG.Utils.LB.LBPlayerData { uniqueID = "b", name = "Боря", score = 300, rank = 2 });
                return data;
            };
            var top = lb.GetTopAsync("main", 5).Result;
            Check("лидерборд: чтение топа через мост -> две записи",
                top.IsSuccess && top.Value.Count == 2 && top.Value[0].DisplayName == "Аня" && top.Value[1].Score == 300 && top.Value[1].Rank == 2);
        }
    }

    static void Authorization()
    {
        Ready();
        YG2.player = new PlayerData { auth = true, id = "7", name = "Нео", photo = "p" };
        using (var auth = new PluginYG2AuthorizationService())
        {
            Check("вход: профиль читается при создании", auth.CurrentProfile.IsAuthorized && auth.CurrentProfile.DisplayName == "Нео" && auth.CurrentProfile.PlayerId == "7");
            Check("вход: уже вошли -> Success без окна", auth.SignInAsync().Result.IsSuccess);
        }

        Ready();
        using (var auth = new PluginYG2AuthorizationService())
        {
            int changed = 0;
            auth.ProfileChanged += (s, e) => changed++;
            Check("вход: изначально гость", !auth.CurrentProfile.IsAuthorized);
            YG2.OnOpenAuthDialog = () =>
            {
                YG2.player = new PlayerData { auth = true, id = "1", name = "Игрок" };
                YG2.onGetSDKData();
            };
            var signIn = auth.SignInAsync().Result;
            Check("вход: успешный вход -> Success и событие профиля", signIn.IsSuccess && signIn.Value.DisplayName == "Игрок" && changed == 1);
        }

        Ready();
        using (var auth = new PluginYG2AuthorizationService())
        {
            YG2.OnOpenAuthDialog = () => YgSdkBridge.NotifyAuthDialogClosed();
            Check("вход: окно закрыто без входа -> Denied", auth.SignInAsync().Result.Status == PlatformServiceStatus.Denied);
        }

        Ready();
        using (var auth = new PluginYG2AuthorizationService())
        {
            var first = auth.SignInAsync();
            var second = auth.SignInAsync().Result;
            Check("вход: повторный вызов во время входа -> Error", second.Status == PlatformServiceStatus.Error);
            YgSdkBridge.NotifyAuthDialogClosed();
            Check("вход: первый вызов завершается Denied после закрытия окна", first.Result.Status == PlatformServiceStatus.Denied);
        }

        YG2.ResetValues();
        using (var auth = new PluginYG2AuthorizationService())
        {
            Check("вход: SDK не готов -> Unavailable", auth.SignInAsync().Result.Status == PlatformServiceStatus.PlatformUnavailable);
        }
    }

    static void ReadinessAndLifecycle()
    {
        YG2.ResetValues();
        using (var ready = new PluginYG2GameReadinessService())
        {
            var queued = ready.MarkReadyAsync().Result;
            Check("готовность: SDK не готов -> Success «в очереди», отправки нет", queued.IsSuccess && !ready.IsReadySent && YG2.GameReadyCalls == 0);
            YG2.isSDKEnabled = true; YG2.iPlatform = new object();
            YG2.onGetSDKData();
            Check("готовность: после инициализации SDK отправляется отложенная", ready.IsReadySent && YG2.GameReadyCalls == 1);
            Check("готовность: повторный вызов не отправляет второй раз", ready.MarkReadyAsync().Result.IsSuccess && YG2.GameReadyCalls == 1);
        }

        YG2.ResetValues();
        using (var life = new PluginYG2GameplayLifecycleService())
        {
            var queued = life.SetGameplayActive(true);
            Check("геймплей: SDK не готов -> запрос в очереди", queued.IsSuccess && YG2.GameplayStartCalls == 0);
            YG2.isSDKEnabled = true; YG2.iPlatform = new object();
            YG2.onGetSDKData();
            Check("геймплей: после инициализации отправляется отложенный старт", YG2.GameplayStartCalls == 1 && life.IsGameplayActive);
            life.SetGameplayActive(false);
            Check("геймплей: остановка отправляется", YG2.GameplayStopCalls == 1 && !life.IsGameplayActive);

            bool? paused = null;
            life.PlatformPauseChanged += (s, e) => paused = e.IsPaused;
            YG2.onPauseGame(true);
            Check("геймплей: пауза от платформы доходит до игры", paused == true);
            YG2.onPauseGame(false);
            Check("геймплей: снятие паузы доходит до игры", paused == false);
        }
    }

    static ILocalizationCatalog Catalog()
    {
        var en = new Dictionary<string, string> { ["hello"] = "Hello" };
        var ru = new Dictionary<string, string> { ["hello"] = "Привет" };
        return new DictionaryLocalizationCatalog("en",
            new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en"] = en, ["ru"] = ru });
    }

    static void LocalizationChecks()
    {
        Ready();
        YG2.lang = "ru";
        using (var loc = new PluginYG2LocalizationService(Catalog()))
        {
            int changes = 0;
            loc.LanguageChanged += (s, e) => changes++;
            Check("язык: берётся язык платформы", loc.CurrentLanguage == "ru" && loc.Get("hello") == "Привет");
            loc.SetLanguage("en");
            Check("язык: переключение через платформу -> событие", loc.CurrentLanguage == "en" && loc.Get("hello") == "Hello" && changes == 1);
            YG2.onCorrectLang("ru-RU");
            Check("язык: событие платформы нормализуется", loc.CurrentLanguage == "ru" && changes == 2);
            Check("язык: определение языка -> Success", loc.DetectLanguageAsync().Result.IsSuccess);
        }
    }

    static void SaveAndPurchases()
    {
        Ready();
        using (var save = new PluginYG2SaveService())
        {
            Check("сохранение: сервис доступен при наличии моста", save.IsPlatformAvailable);

            // Вне WebGL мост хранилища не может подтвердить запись: это эмулирует сбой Яндекса.
            var loaded = save.LoadAsync("progress").Result;
            Check("сохранение: сбой загрузки -> Error, а не пустое сохранение", loaded.Status == PlatformServiceStatus.Error && loaded.Value == null);
            var saved = save.SaveAsync("progress", "{}").Result;
            Check("сохранение: нет подтверждения записи -> Error", saved.Status == PlatformServiceStatus.Error);
            Check("сохранение: после сбоя блокировка свободна (повторная запись не зависает)", save.SaveAsync("progress", "{}").Wait(5000));
            Check("сохранение: пустой ключ -> Error", save.LoadAsync("").Result.Status == PlatformServiceStatus.Error);
        }

        Ready();
        using (var save = new PluginYG2SaveService("несуществующее_поле"))
        {
            var result = save.SaveAsync("progress", "{}").Result;
            Check("сохранение: неверное имя поля -> Error с именем поля", result.Status == PlatformServiceStatus.Error && result.Message.Contains("несуществующее_поле"));
        }

        Ready();
        using (var purchases = new PluginYG2PurchasesService())
        {
            Check("покупки: сервис доступен при наличии моста", purchases.IsPlatformAvailable);
            Check("покупки: пустой идентификатор товара -> Error", purchases.PurchaseAsync("").Result.Status == PlatformServiceStatus.Error);
            Check("покупки: пустой токен -> Error", purchases.ConsumeAsync(" ").Result.Status == PlatformServiceStatus.Error);
        }

        // Мост хранилища не падает от посторонних ответов.
        YgStorageBridge.Complete("не json");
        YgStorageBridge.Complete("{\"id\":\"нет-такого\",\"payload\":\"x\"}");
        Check("хранилище: посторонний ответ игнорируется", true);
    }

    static void FactoriesChecks()
    {
        // Без UNITY_EDITOR и UNITY_WEBGL фабрики возвращают заглушки.
        var catalog = Catalog();
        Check("фабрики: вне редактора и WebGL возвращаются заглушки",
            AdsServiceFactory.Create() is UnavailableAdsService
            && SaveServiceFactory.Create() is UnavailableSaveService
            && PurchasesServiceFactory.Create() is UnavailablePurchasesService
            && AuthorizationServiceFactory.Create() is UnavailableAuthorizationService
            && GameReadinessServiceFactory.Create() is UnavailableGameReadinessService
            && GameplayLifecycleServiceFactory.Create() is UnavailableGameplayLifecycleService
            && LeaderboardServiceFactory.Create() is UnavailableLeaderboardService
            && LocalizationServiceFactory.Create(catalog) is UnavailableLocalizationService);

        bool threw = false;
        try { LocalizationServiceFactory.Create(null); } catch (ArgumentNullException) { threw = true; }
        Check("фабрики: локализация без каталога отклоняется", threw);
    }
}
