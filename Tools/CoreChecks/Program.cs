using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YgCore.Contracts;
using YgCore.Core;
using YgCore.Factories;
using YgCore.Localization;
using YgCore.Mocks;

// Проверка поведения чистой логики Core вне Unity.
static class P
{
    static int fails;
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "ок   " : "FAIL ") + name); if (!ok) fails++; }

    static int Main()
    {
        // Пауза: складывается из причин, снимается только когда сняты все.
        var pc = new PauseCoordinator();
        int events = 0; pc.PauseChanged += (s, e) => events++;
        pc.RequestPause(PauseReason.User); pc.RequestPause(PauseReason.Ad); pc.RequestPause(PauseReason.Ad);
        Check("пауза: две причины, повтор не шлёт событие", pc.IsPaused && events == 2);
        pc.ReleasePause(PauseReason.Ad);
        Check("пауза остаётся, пока есть User", pc.IsPaused && pc.HasReason(PauseReason.User));
        pc.ReleasePause(PauseReason.User);
        Check("пауза снята, когда сняты все", !pc.IsPaused);
        bool threw = false; try { pc.RequestPause(PauseReason.User | PauseReason.Ad); } catch (ArgumentException) { threw = true; }
        Check("составной флаг отклоняется", threw);
        threw = false; try { pc.RequestPause(PauseReason.None); } catch (ArgumentException) { threw = true; }
        Check("None отклоняется", threw);

        // Экономика: транзакции идемпотентны.
        var eco = new SessionEconomyService();
        Check("начисление", eco.TryCredit("t1", 100, "test") && eco.Balance == 100);
        Check("повтор транзакции игнорируется", !eco.TryCredit("t1", 100, "test") && eco.Balance == 100);
        Check("списание", eco.TrySpend("t2", 30, "test") && eco.Balance == 70);
        Check("нельзя уйти в минус", !eco.TrySpend("t3", 71, "test") && eco.Balance == 70);
        Check("пустой id отклоняется", !eco.TryCredit("", 5, "x") && !eco.TryCredit(null, 5, "x"));
        Check("ноль и минус отклоняются", !eco.TryCredit("t4", 0, "x") && !eco.TryCredit("t5", -5, "x"));
        eco.RestoreBalance(-10);
        Check("восстановление не даёт отрицательный баланс", eco.Balance == 0);

        // Состояния: старт из меню без отсчёта.
        var pc2 = new PauseCoordinator(); var gs = new GameplayStateService(pc2);
        gs.CompleteBoot();
        Check("Boot -> Menu", gs.CurrentState == AppState.Menu);
        gs.StartRun();
        Check("старт из меню: сразу Run без отсчёта", gs.CurrentState == AppState.Run && gs.IsGameplayActive && !pc2.IsPaused);
        pc2.RequestPause(PauseReason.User);
        Check("пауза переводит в Pause и останавливает геймплей", gs.CurrentState == AppState.Pause && !gs.IsGameplayActive);
        pc2.ReleasePause(PauseReason.User);
        Check("после снятия паузы ставится отсчёт Countdown", gs.CurrentState == AppState.Pause && pc2.HasReason(PauseReason.Countdown));
        pc2.ReleasePause(PauseReason.Countdown);
        Check("после отсчёта возврат в Run", gs.CurrentState == AppState.Run && gs.IsGameplayActive);
        gs.FinishRun();
        Check("FinishRun -> Result", gs.CurrentState == AppState.Result && !gs.IsGameplayActive);
        gs.StartRun();
        Check("рестарт не из меню: Pause с отсчётом", gs.CurrentState == AppState.Pause && pc2.HasReason(PauseReason.Countdown));
        pc2.ReleasePause(PauseReason.Countdown);
        Check("после отсчёта рестарта Run", gs.CurrentState == AppState.Run && gs.IsGameplayActive);

        // Игра, которой отсчёт не нужен, снимает Countdown сразу в обработчике события паузы.
        var pc3 = new PauseCoordinator(); var gs3 = new GameplayStateService(pc3);
        pc3.PauseChanged += (s, e) => { if (e.CurrentReasons.HasFlag(PauseReason.Countdown)) pc3.ReleasePause(PauseReason.Countdown); };
        gs3.CompleteBoot(); gs3.StartRun();
        pc3.RequestPause(PauseReason.User); pc3.ReleasePause(PauseReason.User);
        Check("без отсчёта: после паузы сразу Run", gs3.CurrentState == AppState.Run && gs3.IsGameplayActive && !pc3.IsPaused);
        gs3.FinishRun(); gs3.StartRun();
        Check("без отсчёта: рестарт сразу Run", gs3.CurrentState == AppState.Run && gs3.IsGameplayActive && !pc3.IsPaused);

        // Настройки.
        var st = new SessionGameSettingsService();
        Check("громкости по умолчанию 0.5 / 0.1", st.MusicVolume == 0.5f && st.SfxVolume == 0.1f);
        st.SetMusicVolume(2f);
        Check("громкость ограничивается 0..1", st.MusicVolume == 1f);
        var st2 = new SessionGameSettingsService(0.8f, 0.3f);
        Check("свои громкости по умолчанию", st2.MusicVolume == 0.8f && st2.SfxVolume == 0.3f);

        // Каталог локализации: нормализация языка, запасной язык, подстановка аргументов.
        var en = new Dictionary<string, string> { ["hello"] = "Hello {0}", ["only_en"] = "Only EN" };
        var ru = new Dictionary<string, string> { ["hello"] = "Привет {0}" };
        var catalog = new DictionaryLocalizationCatalog("en",
            new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en"] = en, ["ru"] = ru });
        Check("язык: ru-RU и RU_ru -> ru", catalog.NormalizeLanguage("ru-RU") == "ru" && catalog.NormalizeLanguage(" RU_ru ") == "ru");
        Check("язык: неизвестный и пустой -> по умолчанию", catalog.NormalizeLanguage("de") == "en" && catalog.NormalizeLanguage("") == "en" && catalog.NormalizeLanguage(null) == "en");
        Check("строка на языке с аргументом", catalog.Get("ru", "hello", "Мир") == "Привет Мир");
        Check("нет ключа на языке -> берётся язык по умолчанию", catalog.Get("ru", "only_en") == "Only EN");
        Check("нет ключа нигде -> возвращается ключ", catalog.Get("ru", "missing") == "missing");
        Check("пустой ключ -> пустая строка", catalog.Get("en", " ") == "");
        bool noTable = false; try { new DictionaryLocalizationCatalog("de", new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en"] = en }); } catch (ArgumentException) { noTable = true; }
        Check("язык по умолчанию без таблицы отклоняется", noTable);

        // Моки: сценарии, события, сохранение, покупки, локализация.
        var save = new EditorMockSaveService();
        Check("мок-сохранение: нет ключа -> успех со значением null", save.LoadAsync("k").GetAwaiter().GetResult() is var r1 && r1.IsSuccess && r1.Value == null);
        Check("мок-сохранение: запись и чтение", save.SaveAsync("k", "v").GetAwaiter().GetResult().IsSuccess && save.LoadAsync("k").GetAwaiter().GetResult().Value == "v");
        save.SimulateTimeout = true;
        Check("мок-сохранение: таймаут -> Error и данные не меняются", save.LoadAsync("k").GetAwaiter().GetResult().Status == PlatformServiceStatus.Error
            && save.SaveAsync("k", "other").GetAwaiter().GetResult().Status == PlatformServiceStatus.Error);
        save.SimulateTimeout = false;
        Check("мок-сохранение: после таймаута старые данные целы", save.LoadAsync("k").GetAwaiter().GetResult().Value == "v");
        save.Scenario = MockPlatformScenario.Denied;
        Check("мок: сценарий Denied", save.LoadAsync("k").GetAwaiter().GetResult().Status == PlatformServiceStatus.Denied);
        save.Scenario = MockPlatformScenario.Success; save.IsPlatformAvailable = false;
        Check("мок: платформа недоступна", save.LoadAsync("k").GetAwaiter().GetResult().Status == PlatformServiceStatus.PlatformUnavailable);

        var ads = new EditorMockAdsService(); int adEvents = 0;
        ads.AdStarted += (s, e) => adEvents++; ads.AdFinished += (s, e) => adEvents++;
        Check("мок-реклама: успех шлёт начало и конец", ads.ShowRewardedAsync("p").GetAwaiter().GetResult().IsSuccess && adEvents == 2);
        ads.Scenario = MockPlatformScenario.Denied; adEvents = 0;
        Check("мок-реклама: отказ без событий", !ads.ShowInterstitialAsync("p").GetAwaiter().GetResult().IsSuccess && adEvents == 0);

        var purchases = new EditorMockPurchasesService();
        Check("мок-покупки: общий каталог из 4 товаров", purchases.GetCatalogAsync().GetAwaiter().GetResult().Value.Count == 4);
        var own = new EditorMockPurchasesService(new[] { new PurchaseProduct("x", "X", "1", "YAN", true) });
        Check("мок-покупки: каталог игры заменяет общий", own.GetCatalogAsync().GetAwaiter().GetResult().Value.Count == 1);
        var receipt = own.PurchaseAsync("x").GetAwaiter().GetResult().Value;
        Check("мок-покупки: подтверждение выдачи", !own.WasConsumed(receipt.Token) && own.ConsumeAsync(receipt.Token).GetAwaiter().GetResult().IsSuccess && own.WasConsumed(receipt.Token));
        own.AddPendingPurchase("x", "t-pending");
        Check("мок-покупки: невыданная покупка возвращается", own.RestorePurchasesAsync().GetAwaiter().GetResult().Value.Count == 1);

        var auth = new EditorMockAuthorizationService(); int profileEvents = 0;
        auth.ProfileChanged += (s, e) => profileEvents++;
        Check("мок-вход: стартует гостем", !auth.CurrentProfile.IsAuthorized);
        auth.SignInAsync().GetAwaiter().GetResult(); auth.SignInAsync().GetAwaiter().GetResult();
        Check("мок-вход: профиль меняется один раз", auth.CurrentProfile.IsAuthorized && profileEvents == 1);

        var locMock = new EditorMockLocalizationService(catalog, "ru-RU"); int langEvents = 0;
        locMock.LanguageChanged += (s, e) => langEvents++;
        Check("мок-язык: начальный язык нормализуется", locMock.CurrentLanguage == "ru" && locMock.Get("hello", "А") == "Привет А");
        locMock.SetLanguage("ru"); locMock.SetLanguage("en");
        Check("мок-язык: событие только при смене", langEvents == 1 && locMock.CurrentLanguage == "en");

        // Заглушки: платформы нет, ничего не падает.
        Check("заглушка рекламы", new UnavailableAdsService().ShowRewardedAsync("p").GetAwaiter().GetResult().Status == PlatformServiceStatus.PlatformUnavailable);
        Check("заглушка сохранения (загрузка не даёт пустого сохранения)", new UnavailableSaveService().LoadAsync("k").GetAwaiter().GetResult().Status == PlatformServiceStatus.PlatformUnavailable);
        Check("заглушка авторизации: гость", !new UnavailableAuthorizationService().CurrentProfile.IsAuthorized);
        var locStub = new UnavailableLocalizationService(catalog);
        Check("заглушка языка: по умолчанию, строки работают", locStub.CurrentLanguage == "en" && locStub.Get("hello", "W") == "Hello W");

        // Модуль Save: политики защиты прогресса.
        SaveChecks.Run(Check);

        // Модуль Store: покупки, экипировка, выдача в два шага.
        StoreChecks.Run(Check);

        Console.WriteLine(fails == 0 ? "\nВсе проверки пройдены." : $"\nПровалено: {fails}");
        return fails == 0 ? 0 : 1;
    }
}
