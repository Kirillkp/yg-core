// Проверки v0.2.0 без Unity: моки оценки, ярлыка и серверного времени, заглушки и составной каталог строк.
using System;
using System.Collections.Generic;
using YgCore.Contracts;
using YgCore.Factories;
using YgCore.Localization;
using YgCore.Mocks;

static class PlatformChecks
{
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Tables(
        Dictionary<string, string> en, Dictionary<string, string> ru = null, Dictionary<string, string> de = null)
    {
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en"] = en };
        if (ru != null) tables["ru"] = ru;
        if (de != null) tables["de"] = de;
        return tables;
    }

    public static void Run(Action<string, bool> check)
    {
        // --- Оценка игры
        var review = new EditorMockReviewService();
        check("мок-оценка: запрос без CanReview -> Error (как у платформы)", review.RequestReviewAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.Error && review.RequestCount == 0);
        var canReview = review.CanReviewAsync().GetAwaiter().GetResult();
        check("мок-оценка: CanReview -> Success(true)", canReview.IsSuccess && canReview.Value);
        var sent = review.RequestReviewAsync().GetAwaiter().GetResult();
        check("мок-оценка: игрок оценил -> Success(true)", sent.IsSuccess && sent.Value && review.RequestCount == 1);
        var again = review.CanReviewAsync().GetAwaiter().GetResult();
        check("мок-оценка: после запроса в этой сессии CanReview -> false", again.IsSuccess && !again.Value
            && review.RequestReviewAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.Denied);
        var closing = new EditorMockReviewService { UserSendsFeedback = false };
        closing.CanReviewAsync().GetAwaiter().GetResult();
        var closed = closing.RequestReviewAsync().GetAwaiter().GetResult();
        check("мок-оценка: игрок закрыл окно -> Success(false)", closed.IsSuccess && !closed.Value);
        var noReview = new EditorMockReviewService { CanReview = false };
        var cannot = noReview.CanReviewAsync().GetAwaiter().GetResult();
        check("мок-оценка: недоступна -> Success(false) с пояснением", cannot.IsSuccess && !cannot.Value && !string.IsNullOrEmpty(cannot.Message));
        var missingReview = new EditorMockReviewService { IsPlatformAvailable = false };
        check("мок-оценка: платформы нет -> PlatformUnavailable", missingReview.CanReviewAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.PlatformUnavailable);

        // --- Ярлык
        var shortcut = new EditorMockShortcutService();
        check("мок-ярлык: показ без CanShowPrompt -> Error", shortcut.ShowPromptAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.Error);
        check("мок-ярлык: CanShowPrompt -> Success(true)", shortcut.CanShowPromptAsync().GetAwaiter().GetResult() is var canShow && canShow.IsSuccess && canShow.Value);
        var accepted = shortcut.ShowPromptAsync().GetAwaiter().GetResult();
        check("мок-ярлык: согласие -> Success(true)", accepted.IsSuccess && accepted.Value && shortcut.PromptCount == 1);
        check("мок-ярлык: после согласия диалог недоступен", !shortcut.CanShowPromptAsync().GetAwaiter().GetResult().Value);
        var declining = new EditorMockShortcutService { UserAccepts = false };
        declining.CanShowPromptAsync().GetAwaiter().GetResult();
        check("мок-ярлык: отказ -> Success(false), диалог остаётся доступным",
            declining.ShowPromptAsync().GetAwaiter().GetResult() is var rejected && rejected.IsSuccess && !rejected.Value && declining.CanShow);
        shortcut.Scenario = MockPlatformScenario.Denied;
        check("мок-ярлык: сценарий Denied", shortcut.CanShowPromptAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.Denied);

        // --- Серверное время
        var clock = new EditorMockServerClock();
        var before = DateTime.UtcNow;
        var now = clock.GetUtcNowAsync().GetAwaiter().GetResult();
        check("мок-время: по умолчанию системные часы UTC", now.IsSuccess && now.Value.Kind == DateTimeKind.Utc && now.Value >= before && now.Value <= DateTime.UtcNow.AddSeconds(1));
        clock.SetUtcNow(new DateTime(2026, 10, 12, 23, 59, 30));
        check("мок-время: фиксированное время", clock.GetUtcNowAsync().GetAwaiter().GetResult().Value == new DateTime(2026, 10, 12, 23, 59, 30, DateTimeKind.Utc));
        clock.Advance(TimeSpan.FromSeconds(45));
        check("мок-время: сдвиг переводит через границу суток", clock.GetUtcNowAsync().GetAwaiter().GetResult().Value.Date == new DateTime(2026, 10, 13));
        clock.Advance(TimeSpan.FromDays(-2));
        check("мок-время: часы можно отмотать назад", clock.GetUtcNowAsync().GetAwaiter().GetResult().Value.Date == new DateTime(2026, 10, 11));
        clock.Release();
        check("мок-время: Release возвращает системные часы", Math.Abs((clock.UtcNow - DateTime.UtcNow).TotalSeconds) < 2);
        clock.Scenario = MockPlatformScenario.Error;
        check("мок-время: сценарий Error", clock.GetUtcNowAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.Error);

        // --- Заглушки
        check("заглушка оценки", !new UnavailableReviewService().IsPlatformAvailable
            && new UnavailableReviewService().CanReviewAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.PlatformUnavailable
            && new UnavailableReviewService().RequestReviewAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.PlatformUnavailable);
        check("заглушка ярлыка", !new UnavailableShortcutService().IsPlatformAvailable
            && new UnavailableShortcutService().CanShowPromptAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.PlatformUnavailable
            && new UnavailableShortcutService().ShowPromptAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.PlatformUnavailable);
        check("заглушка серверного времени: нет времени, запасной вариант выбирает игра",
            !new UnavailableServerClock().IsPlatformAvailable
            && new UnavailableServerClock().GetUtcNowAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.PlatformUnavailable);

        // --- Составной каталог строк (несколько каталогов в одном сервисе локализации)
        var shellEn = new Dictionary<string, string> { ["shell.menu.title"] = "Menu", ["shell.buy"] = "Buy {0}", ["shared"] = "shell-en", ["shell.only_en"] = "Only EN" };
        var shellRu = new Dictionary<string, string> { ["shell.menu.title"] = "Меню", ["shell.buy"] = "Купить {0}", ["shared"] = "shell-ru" };
        var gameEn = new Dictionary<string, string> { ["game.title"] = "Flood", ["shared"] = "game-en" };
        var gameRu = new Dictionary<string, string> { ["game.title"] = "Потоп" };
        var shell = new DictionaryLocalizationCatalog("en", Tables(shellEn, shellRu));
        var game = new DictionaryLocalizationCatalog("en", Tables(gameEn, gameRu));
        var composite = new CompositeLocalizationCatalog(game, shell);

        check("составной каталог: язык по умолчанию общий", composite.DefaultLanguage == "en");
        check("составной каталог: строки обоих каталогов", composite.Get("ru", "game.title") == "Потоп" && composite.Get("ru", "shell.menu.title") == "Меню");
        check("составной каталог: подстановка аргументов", composite.Get("ru-RU", "shell.buy", 50) == "Купить 50");
        check("составной каталог: английский запасной вариант игры не перебивает русскую строку оболочки",
            composite.Get("ru", "shared") == "shell-ru");
        check("составной каталог: общий ключ берётся из каталога с высшим приоритетом на нужном языке",
            composite.Get("en", "shared") == "game-en" && new CompositeLocalizationCatalog(shell, game).Get("en", "shared") == "shell-en");
        check("составной каталог: нет строки на языке -> язык по умолчанию", composite.Get("ru", "shell.only_en") == "Only EN");
        check("составной каталог: нет ключа нигде -> ключ", composite.Get("ru", "нет.такого") == "нет.такого");
        check("составной каталог: пустой ключ -> пустая строка", composite.Get("ru", " ") == string.Empty && composite.Get("ru", null) == string.Empty);
        check("составной каталог: язык поддерживается, если его знает хотя бы один каталог",
            new CompositeLocalizationCatalog(new DictionaryLocalizationCatalog("en", Tables(gameEn)), shell).NormalizeLanguage("ru-RU") == "ru");
        check("составной каталог: неизвестный и пустой язык -> по умолчанию", composite.NormalizeLanguage("de") == "en" && composite.NormalizeLanguage("") == "en" && composite.NormalizeLanguage(null) == "en");
        check("составной каталог: язык только в одном каталоге и ключ только в другом -> запасной вариант",
            new CompositeLocalizationCatalog(new DictionaryLocalizationCatalog("en", Tables(gameEn)), shell).Get("ru", "game.title") == "Flood");

        var withDe = new CompositeLocalizationCatalog(game, new DictionaryLocalizationCatalog("en", Tables(shellEn, shellRu, new Dictionary<string, string> { ["shell.menu.title"] = "Menü" })));
        check("составной каталог: три языка из разных каталогов", withDe.Get("de", "shell.menu.title") == "Menü" && withDe.Get("de", "game.title") == "Flood" && withDe.Get("ru", "game.title") == "Потоп");

        // Каталог без строгого поиска (чужая реализация) тоже работает.
        var plain = new PlainCatalog();
        var mixed = new CompositeLocalizationCatalog(plain, shell);
        check("составной каталог: чужой каталог без строгого поиска", mixed.Get("ru", "plain.key") == "простая строка" && mixed.Get("ru", "shell.menu.title") == "Меню" && mixed.Get("ru", "нет") == "нет");

        // Проверки аргументов конструктора.
        bool emptyThrew = false; try { new CompositeLocalizationCatalog(); } catch (ArgumentException) { emptyThrew = true; }
        bool nullThrew = false; try { new CompositeLocalizationCatalog(game, null); } catch (ArgumentException) { nullThrew = true; }
        bool defaultsThrew = false; try { new CompositeLocalizationCatalog(game, new DictionaryLocalizationCatalog("ru", Tables(gameEn, gameRu))); } catch (ArgumentException) { defaultsThrew = true; }
        check("составной каталог: пустой список, null и разные языки по умолчанию отклоняются", emptyThrew && nullThrew && defaultsThrew);

        // Через сервис локализации (мок): каталог оболочки и игры в одном сервисе, смена языка.
        var service = new EditorMockLocalizationService(composite, "ru-RU");
        check("сервис локализации с составным каталогом: язык и обе группы строк",
            service.CurrentLanguage == "ru" && service.Get("game.title") == "Потоп" && service.Get("shell.buy", 7) == "Купить 7");
        service.SetLanguage("en");
        check("сервис локализации с составным каталогом: смена языка", service.Get("game.title") == "Flood" && service.Get("shell.menu.title") == "Menu");
        var stub = new UnavailableLocalizationService(composite);
        check("заглушка языка с составным каталогом", stub.CurrentLanguage == "en" && stub.Get("shell.menu.title") == "Menu");
    }

    // Минимальный каталог без ILocalizationCatalogLookup: проверяет приближённое правило для чужих каталогов.
    private sealed class PlainCatalog : ILocalizationCatalog
    {
        public string DefaultLanguage => "en";
        public string NormalizeLanguage(string language) => string.IsNullOrWhiteSpace(language) ? "en" : (language.StartsWith("ru") ? "ru" : "en");
        public string Get(string language, string key, params object[] arguments) =>
            key == "plain.key" ? (NormalizeLanguage(language) == "ru" ? "простая строка" : "plain string") : key;
    }
}
