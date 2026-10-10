using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using YgCore.Contracts;
using YgCore.Core;
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

        // Звук и пауза собираются так же, как в игре: платформенные события превращаются в причины паузы,
        // а директор звука реагирует на координатор пауз.
        public IPauseCoordinator Pause { get; private set; }
        public GameplayStateService State { get; private set; }
        public SessionGameSettingsService Settings { get; private set; }
        public StandAudioDirector Audio { get; private set; }

        // Сколько секунд в редакторе держится пауза рекламы: мок-реклама заканчивается мгновенно, а паузу надо услышать.
        private const float EditorAdSeconds = 3f;
        private int adPauseToken;

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

            Pause = new PauseCoordinator();
            State = new GameplayStateService(Pause);
            // Звук выключен, пока его не включит кнопка: браузеры всё равно требуют жест игрока.
            Settings = new SessionGameSettingsService(0.8f, 0.8f);
            Settings.SetMusicEnabled(false);
            Settings.SetSfxEnabled(false);
            Audio = StandAudioDirector.Create(State, Pause, Settings);
            State.CompleteBoot();
            Pause.PauseChanged += (_, e) => StandLog.Info($"Пауза: {(e.IsPaused ? "да" : "нет")}, причины={Pause.ActiveReasons}");

            Auth.ProfileChanged += (_, e) => StandLog.Info($"Профиль изменился: авторизован={e.Profile.IsAuthorized}, имя='{e.Profile.DisplayName}'");
            Ads.AdStarted += (_, e) =>
            {
                StandLog.Info($"Реклама началась: {e.PlacementId}");
                adPauseToken++;
                Pause.RequestPause(PauseReason.Ad);
            };
            Ads.AdFinished += (_, e) =>
            {
                StandLog.Info($"Реклама закончилась: {e.PlacementId}");
                ReleaseAdPause();
            };
            Lifecycle.PlatformPauseChanged += (_, e) =>
            {
                StandLog.Info($"Пауза платформы: {e.IsPaused}");
                if (e.IsPaused) Pause.RequestPause(PauseReason.Platform);
                else Pause.ReleasePause(PauseReason.Platform);
            };
            Language.LanguageChanged += (_, e) => StandLog.Info($"Язык сменился: {e.CurrentLanguage}");
        }

        // На платформе пауза снимается сразу по закрытию рекламы. В редакторе мок заканчивается мгновенно,
        // поэтому пауза держится несколько секунд, а новая реклама за это время продлевает её (токен).
        private async void ReleaseAdPause()
        {
            if (Application.isEditor)
            {
                var token = adPauseToken;
                StandLog.Info($"Мок-реклама: пауза звука держится {EditorAdSeconds:0} с, имитация показа.");
                await YgCore.Core.RealtimeDelay.Delay(TimeSpan.FromSeconds(EditorAdSeconds));
                if (token != adPauseToken) return;
            }

            Pause.ReleasePause(PauseReason.Ad);
        }

        /// <summary>Включает или выключает музыку и эффекты вместе.</summary>
        public void ToggleSound()
        {
            var enable = !(Settings.IsMusicEnabled || Settings.IsSfxEnabled);
            Settings.SetMusicEnabled(enable);
            Settings.SetSfxEnabled(enable);
            StandLog.Info(enable ? "Звук включён." : "Звук выключен.");
        }

        public void PlayClick()
        {
            Audio.Play(StandAudioDirector.ClickId);
            StandLog.Info(Settings.IsSfxEnabled ? "Клик запрошен." : "Клик запрошен, но эффекты выключены.");
        }

        /// <summary>Строка состояния звука для экрана: настройки, причины паузы, что звучит на самом деле.</summary>
        public string DescribeSound()
        {
            var music = Audio.MusicSource;
            var playing = music != null && music.isPlaying;
            var enabled = Settings.IsMusicEnabled || Settings.IsSfxEnabled;
            return $"Звук: {(enabled ? "ВКЛ" : "выкл")}   музыка играет: {(playing ? "да" : "нет")}   " +
                   $"пауза: {Pause.ActiveReasons}   AudioListener.pause: {(AudioListener.pause ? "да" : "нет")}";
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

        /// <summary>Отправляет в таблицу `stand` значение, введённое на экране (или случайное, если значение не задано).</summary>
        public async Task SubmitScoreAsync(long? score = null)
        {
            var value = score ?? UnityEngine.Random.Range(1, 1000);
            Describe($"Лидерборд: отправка {value}", await Leaderboard.SubmitScoreAsync("stand", value));
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

        /// <summary>
        /// Две быстрые записи подряд: должна остаться вторая. Журнал пошаговый, чтобы при зависании было видно,
        /// на какой записи оно произошло, а защита по времени не даёт проверке висеть бесконечно.
        /// </summary>
        public async Task RunSaveOrderCheckAsync()
        {
            StandLog.Info("Порядок записей: запускаю две записи подряд.");

            async Task<PlatformServiceResult> Write(int number)
            {
                var result = await Save.SaveAsync(SaveKey, "{\"n\":" + number + "}");
                StandLog.Info($"Порядок записей: запись {number} завершена ({result.Status}).");
                return result;
            }

            var both = Task.WhenAll(Write(1), Write(2));
            var finished = await Task.WhenAny(both, YgCore.Core.RealtimeDelay.Delay(TimeSpan.FromSeconds(25)));
            if (finished != both)
            {
                StandLog.Fail("Порядок записей: записи не завершились за 25 с (зависание).");
                return;
            }

            var loaded = await Save.LoadAsync(SaveKey);
            StandLog.Check(loaded.IsSuccess && loaded.Value == "{\"n\":2}", $"Порядок записей: итог='{loaded.Value}'");
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
