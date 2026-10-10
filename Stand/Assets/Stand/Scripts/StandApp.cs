using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using YgCore.Contracts;
using YgCore.Factories;
using YgCore.Localization;

namespace YgCoreStand
{
    /// <summary>
    /// Сервисы и проверки стенда. Здесь нет игровой логики: каждая кнопка вызывает один контракт пакета
    /// и пишет в журнал результат. Секции соответствуют Docs/STAND_CHECKLIST.md.
    /// </summary>
    public sealed class StandApp
    {
        private const string SaveKey = "stand-check";
        private const string StorageFieldName = "ygCoreStorage";

        public IAdsService Ads { get; private set; }
        public IAuthorizationService Auth { get; private set; }
        public IGameReadinessService Readiness { get; private set; }
        public IGameplayLifecycleService Lifecycle { get; private set; }
        public ILeaderboardService Leaderboard { get; private set; }
        public ILocalizationService Language { get; private set; }
        public IPurchasesService Purchases { get; private set; }
        public ISaveService Save { get; private set; }

        /// <summary>Создаёт сервисы через фабрики пакета, как это делает игра.</summary>
        public void Initialize()
        {
            var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["en"] = new Dictionary<string, string> { ["stand.hello"] = "Hello" },
                ["ru"] = new Dictionary<string, string> { ["stand.hello"] = "Привет" },
            };
            var catalog = new DictionaryLocalizationCatalog("en", tables);

            Ads = AdsServiceFactory.Create();
            Auth = AuthorizationServiceFactory.Create();
            Readiness = GameReadinessServiceFactory.Create();
            Lifecycle = GameplayLifecycleServiceFactory.Create();
            Leaderboard = LeaderboardServiceFactory.Create();
            Language = LocalizationServiceFactory.Create(catalog);
            Purchases = PurchasesServiceFactory.Create();
            Save = SaveServiceFactory.Create(storageFieldName: StorageFieldName);

            Auth.ProfileChanged += (_, e) => StandLog.Info($"Профиль изменился: авторизован={e.Profile.IsAuthorized}, имя='{e.Profile.DisplayName}'");
            Ads.AdStarted += (_, e) => StandLog.Info($"Реклама началась: {e.PlacementId}");
            Ads.AdFinished += (_, e) => StandLog.Info($"Реклама закончилась: {e.PlacementId}");
            Lifecycle.PlatformPauseChanged += (_, e) => StandLog.Info($"Пауза платформы: {e.IsPaused}");
            Language.LanguageChanged += (_, e) => StandLog.Info($"Язык сменился: {e.CurrentLanguage}");
        }

        /// <summary>Какие сервисы реально работают на платформе, а какие заглушки/моки.</summary>
        public void ReportAvailability()
        {
            StandLog.Info($"Платформа: {Application.platform}, редактор={Application.isEditor}");
            Report("Реклама", Ads.IsPlatformAvailable);
            Report("Авторизация", Auth.IsPlatformAvailable);
            Report("Готовность", Readiness.IsPlatformAvailable);
            Report("Жизненный цикл", Lifecycle.IsPlatformAvailable);
            Report("Лидерборд", Leaderboard.IsPlatformAvailable);
            Report("Язык", Language.IsPlatformAvailable);
            Report("Покупки", Purchases.IsPlatformAvailable);
            Report("Сохранение", Save.IsPlatformAvailable);
        }

        private static void Report(string name, bool available)
        {
            // В редакторе IsPlatformAvailable == false это норма (работают моки). На Яндексе должно быть true.
            if (Application.isEditor) StandLog.Info($"{name}: платформа={available} (редактор, моки)");
            else StandLog.Check(available, $"{name}: платформа доступна");
        }

        public async Task MarkReadyAsync() => Describe("Готовность", await Readiness.MarkReadyAsync());

        public void SetGameplay(bool active) => Describe(active ? "Геймплей: старт" : "Геймплей: стоп", Lifecycle.SetGameplayActive(active));

        public async Task ShowInterstitialAsync() => Describe("Interstitial", await Ads.ShowInterstitialAsync("stand-interstitial"));

        public async Task ShowRewardedAsync()
        {
            var result = await Ads.ShowRewardedAsync("stand-rewarded");
            Describe("Rewarded", result);
            StandLog.Info(result.IsSuccess ? "Награду можно выдавать (просмотр завершён)." : "Награду НЕ выдавать.");
        }

        public async Task SignInAsync()
        {
            var result = await Auth.SignInAsync();
            Describe("Вход", result);
            if (result.IsSuccess) StandLog.Info($"Игрок: id='{result.Value.PlayerId}', имя='{result.Value.DisplayName}'");
        }

        public void ShowProfile()
        {
            var p = Auth.CurrentProfile;
            StandLog.Info($"Текущий профиль: авторизован={p.IsAuthorized}, id='{p.PlayerId}', имя='{p.DisplayName}'");
        }

        public async Task SubmitScoreAsync()
        {
            var score = UnityEngine.Random.Range(1, 1000);
            Describe($"Лидерборд: отправка {score}", await Leaderboard.SubmitScoreAsync("stand", score));
        }

        public async Task GetTopAsync()
        {
            var result = await Leaderboard.GetTopAsync("stand", 5);
            Describe("Лидерборд: топ", result);
            if (!result.IsSuccess || result.Value == null) return;
            foreach (var e in result.Value) StandLog.Info($"  #{e.Rank} {e.DisplayName}: {e.Score}");
        }

        public async Task ShowCatalogAsync()
        {
            var result = await Purchases.GetCatalogAsync();
            Describe("Покупки: каталог", result);
            if (!result.IsSuccess || result.Value == null) return;
            foreach (var p in result.Value) StandLog.Info($"  {p.Id}: {p.Title}, {p.DisplayPrice}, расходуемый={p.IsConsumable}");
        }

        public async Task BuyAsync(string productId)
        {
            var result = await Purchases.PurchaseAsync(productId);
            Describe($"Покупка {productId}", result);
            if (result.IsSuccess)
            {
                StandLog.Info($"  токен='{result.Value.Token}', погашена={result.Value.IsConsumed}");
                StandBootstrap.RememberReceipt(result.Value.Token);
            }
        }

        public async Task RestoreAsync()
        {
            var result = await Purchases.RestorePurchasesAsync();
            Describe("Покупки: невыданные", result);
            if (!result.IsSuccess || result.Value == null) return;
            StandLog.Info($"  невыданных покупок: {result.Value.Count}");
            foreach (var r in result.Value) StandLog.Info($"  {r.ProductId}, токен='{r.Token}', погашена={r.IsConsumed}");
        }

        public async Task ConsumeAsync(string token) => Describe("Покупки: погашение", await Purchases.ConsumeAsync(token));

        /// <summary>Самопроверка сохранения: запись, чтение, сравнение, удаление.</summary>
        public async Task RunSaveCheckAsync()
        {
            var payload = "{\"stand\":" + DateTime.UtcNow.Ticks + "}";
            var saved = await Save.SaveAsync(SaveKey, payload);
            StandLog.Check(saved.IsSuccess, $"Сохранение: запись ({saved.Status})");
            var loaded = await Save.LoadAsync(SaveKey);
            StandLog.Check(loaded.IsSuccess && loaded.Value == payload, $"Сохранение: чтение вернуло то же ({loaded.Status})");
            var deleted = await Save.DeleteAsync(SaveKey);
            StandLog.Check(deleted.IsSuccess, $"Сохранение: удаление ({deleted.Status})");
            var after = await Save.LoadAsync(SaveKey);
            StandLog.Check(after.IsSuccess && string.IsNullOrEmpty(after.Value), $"Сохранение: после удаления пусто ({after.Status})");
        }

        /// <summary>Две быстрые записи подряд: должна остаться вторая.</summary>
        public async Task RunSaveOrderCheckAsync()
        {
            var first = Save.SaveAsync(SaveKey, "{\"n\":1}");
            var second = Save.SaveAsync(SaveKey, "{\"n\":2}");
            await Task.WhenAll(first, second);
            var loaded = await Save.LoadAsync(SaveKey);
            StandLog.Check(loaded.IsSuccess && loaded.Value == "{\"n\":2}", $"Сохранение: порядок записей, итог='{loaded.Value}'");
            await Save.DeleteAsync(SaveKey);
        }

        /// <summary>Самопроверка языка: платформа возвращает код, а каталог приводит его к поддерживаемому.</summary>
        public async Task RunLanguageCheckAsync()
        {
            var detected = await Language.DetectLanguageAsync();
            StandLog.Check(detected.IsSuccess && !string.IsNullOrEmpty(detected.Value), $"Язык: определён '{detected.Value}' ({detected.Status})");
            Language.SetLanguage(detected.IsSuccess ? detected.Value : "en");
            StandLog.Info($"Текущий язык: {Language.CurrentLanguage}, stand.hello='{Language.Get("stand.hello")}'");
            Language.SetLanguage("ru");
            StandLog.Check(Language.Get("stand.hello") == "Привет", "Язык: русская строка");
            Language.SetLanguage("en");
            StandLog.Check(Language.Get("stand.hello") == "Hello", "Язык: английская строка");
        }

        private static void Describe(string name, PlatformServiceResult result)
        {
            var tail = string.IsNullOrEmpty(result.Message) ? string.Empty : $" — {result.Message}";
            StandLog.Info($"{name}: {result.Status}{tail}");
        }
    }
}
