// Проверки модуля Save без Unity: политики защиты прогресса (не затирать нечитаемое, миграция,
// слияние при входе, лимит размера, повтор после сбоя) на образце игры из SampleSave.cs.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YgCore.Contracts;
using YgCore.Core;
using YgCore.Mocks;
using YgCore.Save;

static class SaveChecks
{
    private const string Key = "player_progress";

    // Хранилище-обёртка: считает записи и позволяет сломать конкретные вызовы.
    private sealed class ScriptedSave : ISaveService
    {
        public readonly EditorMockSaveService Inner = new EditorMockSaveService();
        public int SaveCalls;
        public int FailNextSaves;

        public bool IsPlatformAvailable => Inner.IsPlatformAvailable;

        // Если задан, загрузка ждёт, пока проверка не разрешит: так воспроизводится «окно слияния»,
        // в котором игрок успевает получить награду или купить предмет.
        public TaskCompletionSource<bool> LoadGate;
        public volatile bool LoadWaiting;

        public async Task<PlatformServiceResult<string>> LoadAsync(string key)
        {
            var gate = LoadGate;
            if (gate != null)
            {
                LoadWaiting = true;
                await gate.Task;
                LoadWaiting = false;
            }

            return await Inner.LoadAsync(key);
        }

        public Task<PlatformServiceResult> SaveAsync(string key, string payload)
        {
            SaveCalls++;
            if (FailNextSaves > 0)
            {
                FailNextSaves--;
                return Task.FromResult(PlatformServiceResult.Error("Сбой записи для проверки."));
            }

            return Inner.SaveAsync(key, payload);
        }

        public Task<PlatformServiceResult> DeleteAsync(string key) => Inner.DeleteAsync(key);

        public string Stored => Inner.LoadAsync(Key).GetAwaiter().GetResult().Value;
    }

    // Образец игры целиком: экономика, настройки, связь и сервис прогресса.
    private sealed class Rig : IDisposable
    {
        public readonly ScriptedSave Save = new ScriptedSave();
        public readonly SessionEconomyService Economy = new SessionEconomyService();
        public readonly SessionGameSettingsService Settings = new SessionGameSettingsService();
        public readonly EditorMockAuthorizationService Auth = new EditorMockAuthorizationService();
        public readonly SampleBinding Binding;
        public readonly ProgressionService<SampleProgress> Service;
        public readonly ProgressionCodec<SampleProgress> Codec = new ProgressionCodec<SampleProgress>(new SampleSchema());

        public Rig(ProgressionOptions options = null)
        {
            Binding = new SampleBinding(Economy, Settings);
            Service = new ProgressionService<SampleProgress>(
                Save, Auth, new SampleSchema(), Binding,
                options ?? new ProgressionOptions
                {
                    StorageKey = Key,
                    BatchDelay = TimeSpan.FromMilliseconds(60),
                    RetryDelay = TimeSpan.FromMilliseconds(120)
                });
        }

        public void Init() => Service.InitializeAsync().GetAwaiter().GetResult();

        public SampleProgress StoredData()
        {
            Codec.TryDeserialize(Save.Stored, out var data, out _, out _);
            return data;
        }

        public void Dispose() => Service.Dispose();
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition()) return true;
            Thread.Sleep(10);
        }

        return condition();
    }

    private static string Payload(SampleProgress data) =>
        new ProgressionCodec<SampleProgress>(new SampleSchema()).Serialize(data);

    public static void Run(Action<string, bool> check)
    {
        // --- Кодек: заголовок, версии, слияние
        var codec = new ProgressionCodec<SampleProgress>(new SampleSchema());
        check("кодек: пустая строка -> Empty, использовать можно", codec.TryDeserialize("", out _, out var st, out _) && st == ProgressionLoadState.Empty);
        check("кодек: мусор -> Corrupted", !codec.TryDeserialize("{не json", out _, out st, out _) && st == ProgressionLoadState.Corrupted);
        check("кодек: чужой формат -> Corrupted", !codec.TryDeserialize("{\"format\":\"другая-игра\",\"schemaVersion\":1}", out _, out st, out _) && st == ProgressionLoadState.Corrupted);
        check("кодек: отрицательная версия -> Corrupted", !codec.TryDeserialize("{\"format\":\"sample-progress\",\"schemaVersion\":-1}", out _, out st, out _) && st == ProgressionLoadState.Corrupted);
        check("кодек: версия новее -> UnsupportedVersion", !codec.TryDeserialize("{\"format\":\"sample-progress\",\"schemaVersion\":9}", out _, out st, out _) && st == ProgressionLoadState.UnsupportedVersion);
        var legacyOk = codec.TryDeserialize("{\"format\":\"sample-progress\",\"schemaVersion\":0,\"revision\":3,\"coins\":75,\"recordMeters\":210,\"items\":[\"b\",\"a\",\"a\"]}", out var migrated, out st, out _);
        check("кодек: старая схема -> Migrated с приведением", legacyOk && st == ProgressionLoadState.Migrated && migrated.balance == 75 && migrated.bestDistance == 210
            && migrated.ownedItemIds.Count == 2 && migrated.ownedItemIds[0] == "a" && migrated.schemaVersion == 1 && migrated.revision == 3);
        check("кодек: ошибка в миграции -> Corrupted", !codec.TryDeserialize("{\"format\":\"sample-progress\",\"schemaVersion\":0,\"coins\":\"текст\"}", out _, out st, out _) && st == ProgressionLoadState.Corrupted);
        var roundTrip = new SampleProgress { balance = -5, bestDistance = 7, ownedItemIds = new List<string> { "z", "a", "a", " " } };
        codec.TryDeserialize(codec.Serialize(roundTrip), out var restored, out st, out _);
        check("кодек: запись и чтение приводят данные (минус -> 0, список без повторов)", st == ProgressionLoadState.Loaded && restored.balance == 0 && restored.bestDistance == 7
            && restored.ownedItemIds.Count == 2 && restored.format == "sample-progress");
        var guestData = new SampleProgress { revision = 2, balance = 30, ownedItemIds = new List<string> { "b" } };
        var cloudData = new SampleProgress { revision = 5, balance = 100, ownedItemIds = new List<string> { "a" } };
        var mergedData = codec.Merge(guestData, cloudData);
        check("кодек: слияние (числа максимум, списки объединение, номер записи на 1 больше)", mergedData.balance == 100 && mergedData.ownedItemIds.Count == 2 && mergedData.revision == 6);

        // --- Помощники
        check("списки: Distinct без пустых и повторов, отсортирован", string.Join(",", ProgressionLists.Distinct(new[] { "b", null, "a", "b", " " })) == "a,b");
        check("списки: Union", string.Join(",", ProgressionLists.Union(new[] { "b" }, new[] { "a", "b" })) == "a,b");
        var settingsNormalized = SettingsProgressData.Normalized(new SettingsProgressData { musicVolume = 5f, sfxVolume = -1f });
        check("настройки: громкости ограничиваются 0..1", settingsNormalized.musicVolume == 1f && settingsNormalized.sfxVolume == 0f);

        // --- Сервис: новый игрок
        using (var rig = new Rig())
        {
            rig.Init();
            check("сервис: новый игрок -> Empty, запись разрешена", rig.Service.LoadState == ProgressionLoadState.Empty && !rig.Service.IsWriteBlocked && rig.Save.SaveCalls == 0);
            rig.Economy.TryCredit("t1", 10, "run_reward");
            check("сервис: награда за забег записывается сразу", WaitFor(() => rig.Save.SaveCalls == 1) && rig.StoredData().balance == 10 && rig.StoredData().revision == 1);
        }

        // --- Пакетная запись
        using (var rig = new Rig())
        {
            rig.Init();
            for (var i = 0; i < 5; i++) rig.Economy.TryCredit("batch" + i, 1, "coin");
            check("сервис: пакетная запись не пишет мгновенно", rig.Save.SaveCalls == 0);
            var written = WaitFor(() => rig.Save.SaveCalls >= 1);
            Thread.Sleep(250); // ждём, не придут ли лишние записи
            check("сервис: пять изменений объединяются в одну запись", written && rig.Save.SaveCalls == 1 && rig.StoredData().balance == 5);
        }

        // --- Защита прогресса: сбой загрузки
        using (var rig = new Rig())
        {
            rig.Save.Inner.SetPayload(Key, "ОРИГИНАЛ");
            rig.Save.Inner.SimulateTimeout = true;
            rig.Init();
            rig.Save.Inner.SimulateTimeout = false;
            var blockedSave = rig.Service.SaveNowAsync().GetAwaiter().GetResult();
            check("защита: сбой загрузки -> Error и запись заблокирована", rig.Service.LoadState == ProgressionLoadState.Error && rig.Service.IsWriteBlocked);
            check("защита: прогресс в хранилище не затёрт после сбоя загрузки", blockedSave.Status == PlatformServiceStatus.Error && rig.Save.SaveCalls == 0 && rig.Save.Stored == "ОРИГИНАЛ");
            rig.Economy.TryCredit("x", 5, "run_reward");
            Thread.Sleep(120);
            check("защита: изменения при блокировке ничего не пишут", rig.Save.SaveCalls == 0 && rig.Save.Stored == "ОРИГИНАЛ");
        }

        foreach (var (name, bad, expected) in new[]
        {
            ("повреждённые данные", "{not-valid-progress", ProgressionLoadState.Corrupted),
            ("версия новее", "{\"format\":\"sample-progress\",\"schemaVersion\":99}", ProgressionLoadState.UnsupportedVersion),
            ("чужой формат", "{\"format\":\"other\",\"schemaVersion\":1}", ProgressionLoadState.Corrupted),
        })
        {
            using (var rig = new Rig())
            {
                rig.Save.Inner.SetPayload(Key, bad);
                rig.Init();
                rig.Economy.TryCredit("x", 5, "run_reward");
                rig.Service.SaveNowAsync().GetAwaiter().GetResult();
                Thread.Sleep(80);
                check("защита: " + name + " -> " + expected + ", запись заблокирована, данные целы",
                    rig.Service.LoadState == expected && rig.Service.IsWriteBlocked && rig.Save.SaveCalls == 0 && rig.Save.Stored == bad);
            }
        }

        using (var rig = new Rig())
        {
            rig.Save.Inner.IsPlatformAvailable = false;
            rig.Init();
            check("защита: нет платформы -> PlatformUnavailable, запись заблокирована", rig.Service.LoadState == ProgressionLoadState.PlatformUnavailable && rig.Service.IsWriteBlocked);
        }

        // --- Загрузка существующего прогресса
        using (var rig = new Rig())
        {
            var existing = new SampleProgress
            {
                revision = 7, balance = 123, bestDistance = 456,
                ownedItemIds = new List<string> { "hat" },
                settings = new SettingsProgressData { musicVolume = 0.9f, sfxEnabled = false }
            };
            rig.Save.Inner.SetPayload(Key, Payload(existing));
            rig.Init();
            check("загрузка: данные применены к сервисам игры", rig.Service.LoadState == ProgressionLoadState.Loaded && rig.Economy.Balance == 123
                && rig.Binding.BestDistance == 456 && rig.Binding.OwnedItems.Count == 1 && rig.Settings.MusicVolume == 0.9f && !rig.Settings.IsSfxEnabled);
            check("загрузка: применение данных не вызывает записи", rig.Save.SaveCalls == 0 && rig.Service.Revision == 7);
            rig.Economy.TryCredit("r", 1, "run_reward");
            check("загрузка: следующая запись получает номер на 1 больше", WaitFor(() => rig.Save.SaveCalls == 1) && rig.StoredData().revision == 8 && rig.StoredData().balance == 124);
        }

        // --- Миграция
        using (var rig = new Rig())
        {
            rig.Save.Inner.SetPayload(Key, "{\"format\":\"sample-progress\",\"schemaVersion\":0,\"revision\":3,\"coins\":75,\"recordMeters\":210,\"items\":[\"old\"]}");
            rig.Init();
            check("миграция: старые данные применены", rig.Service.LoadState == ProgressionLoadState.Migrated && rig.Economy.Balance == 75 && rig.Binding.OwnedItems[0] == "old");
            check("миграция: сразу записано в новой схеме", rig.Save.SaveCalls == 1 && rig.StoredData().schemaVersion == 1 && rig.StoredData().balance == 75 && rig.StoredData().revision == 4);
        }

        // --- Явный сброс снимает блокировку
        using (var rig = new Rig())
        {
            rig.Save.Inner.SetPayload(Key, "{мусор");
            rig.Init();
            var reset = rig.Service.ResetProgressAsync().GetAwaiter().GetResult();
            check("сброс: явный сброс снимает блокировку и пишет пустой прогресс", reset.IsSuccess && !rig.Service.IsWriteBlocked && rig.StoredData().balance == 0 && rig.StoredData().format == "sample-progress");
        }

        // --- Лимит размера
        using (var rig = new Rig(new ProgressionOptions { StorageKey = Key, MaximumPayloadBytes = 60 }))
        {
            rig.Init();
            rig.Economy.TryCredit("t", 10, "coin");
            var tooBig = rig.Service.SaveNowAsync().GetAwaiter().GetResult();
            check("размер: слишком большая запись не отправляется", tooBig.Status == PlatformServiceStatus.Error && rig.Save.SaveCalls == 0 && rig.Service.LastPayloadBytes > 60);
        }

        // --- Повтор после сбоя записи
        using (var rig = new Rig())
        {
            rig.Init();
            rig.Save.FailNextSaves = 1;
            rig.Economy.TryCredit("t", 10, "run_reward");
            check("повтор: после неудачной записи выполняется повторная", WaitFor(() => rig.Save.SaveCalls == 2) && rig.StoredData().balance == 10);
        }

        // --- Составное действие одной записью
        using (var rig = new Rig())
        {
            rig.Init();
            using (rig.Service.SuppressChangeTracking())
            {
                rig.Binding.BestDistance = 500;
                rig.Economy.TryCredit("t", 10, "run_reward");
            }

            Thread.Sleep(100);
            check("составное действие: внутри SuppressChangeTracking записей нет", rig.Save.SaveCalls == 0);
            rig.Service.RequestCriticalSave();
            check("составное действие: одна общая запись после", WaitFor(() => rig.Save.SaveCalls == 1) && rig.StoredData().balance == 10 && rig.StoredData().bestDistance == 500);
        }

        // --- SaveCriticalAsync
        using (var rig = new Rig())
        {
            rig.Init();
            rig.Economy.TryCredit("t", 3, "coin");
            check("SaveCriticalAsync: возвращает true после подтверждённой записи", rig.Service.SaveCriticalAsync().GetAwaiter().GetResult() && rig.StoredData().balance == 3);
            rig.Save.FailNextSaves = 1;
            check("SaveCriticalAsync: возвращает false при сбое записи", !rig.Service.SaveCriticalAsync().GetAwaiter().GetResult());
        }

        // --- Слияние гостя с облаком при входе
        using (var rig = new Rig())
        {
            rig.Init();
            rig.Economy.TryCredit("g", 30, "run_reward");
            rig.Binding.AddItem("b");
            WaitFor(() => rig.Save.SaveCalls >= 1);

            // «Облако» появилось после входа: в нём другие данные.
            rig.Save.Inner.SetPayload(Key, Payload(new SampleProgress { revision = 50, balance = 100, ownedItemIds = new List<string> { "a" } }));
            var callsBefore = rig.Save.SaveCalls;
            rig.Auth.SetAuthorized();
            check("вход: гость и облако объединяются и записываются", WaitFor(() => rig.Save.SaveCalls > callsBefore)
                && rig.Economy.Balance == 100 && rig.Binding.OwnedItems.Count == 2 && rig.StoredData().balance == 100 && rig.StoredData().ownedItemIds.Count == 2 && rig.StoredData().revision > 50);
        }

        using (var rig = new Rig())
        {
            rig.Init();
            rig.Economy.TryCredit("g", 30, "run_reward");
            WaitFor(() => rig.Save.SaveCalls >= 1);
            rig.Save.Inner.SetPayload(Key, "{повреждено");
            var callsBefore = rig.Save.SaveCalls;
            rig.Auth.SetAuthorized();
            Thread.Sleep(150);
            check("вход: нечитаемое облако не затирается данными гостя", rig.Service.IsWriteBlocked && rig.Service.LoadState == ProgressionLoadState.Corrupted
                && rig.Save.SaveCalls == callsBefore && rig.Save.Stored == "{повреждено");
        }


        // --- Слияние при входе не теряет то, что игрок получил за время слияния (исправление v0.2.0)
        using (var rig = new Rig())
        {
            rig.Init();
            rig.Economy.TryCredit("g", 30, "run_reward");
            rig.Binding.AddItem("b");
            WaitFor(() => rig.Save.SaveCalls >= 1);

            rig.Save.Inner.SetPayload(Key, Payload(new SampleProgress { revision = 50, balance = 20, ownedItemIds = new List<string> { "a" } }));
            rig.Save.LoadGate = new TaskCompletionSource<bool>();
            var callsBefore = rig.Save.SaveCalls;
            rig.Auth.SetAuthorized();
            var waiting = WaitFor(() => rig.Save.LoadWaiting);

            // «Окно слияния»: облако ещё отвечает, а игрок уже получил награду и купил предмет.
            rig.Economy.TryCredit("w", 50, "run_reward");
            rig.Binding.AddItem("w");
            var purchaseSave = rig.Service.SaveCriticalAsync();
            rig.Save.LoadGate.SetResult(true);

            check("вход: награда и покупка за время слияния не теряются (баланс 30+50, предметы a, b, w)",
                waiting && purchaseSave.Wait(3000) && WaitFor(() => rig.Save.SaveCalls > callsBefore)
                && rig.Economy.Balance == 80 && rig.Binding.OwnedItems.Count == 3 && rig.Binding.OwnedItems.Contains("w"));
            check("вход: в хранилище записан итог вместе с наградой за время слияния",
                WaitFor(() => rig.StoredData().balance == 80) && rig.StoredData().ownedItemIds.Contains("w") && rig.StoredData().revision > 50);
            check("вход: запрос записи во время слияния завершается успехом (покупка не зависает)", purchaseSave.Result);
        }

        // Слияние не состоялось (сбой облака), но за время ожидания была награда: запись всё равно выполняется.
        using (var rig = new Rig())
        {
            rig.Init();
            rig.Save.Inner.SetPayload(Key, Payload(new SampleProgress { revision = 5, balance = 1 }));
            rig.Save.LoadGate = new TaskCompletionSource<bool>();
            rig.Auth.SetAuthorized();
            var waiting = WaitFor(() => rig.Save.LoadWaiting);
            rig.Economy.TryCredit("w", 40, "run_reward");
            rig.Save.Inner.SimulateTimeout = true;
            rig.Save.LoadGate.SetResult(true);
            WaitFor(() => !rig.Service.IsChangeTrackingSuppressed);
            rig.Save.Inner.SimulateTimeout = false;
            check("вход: сбой облака при слиянии, награда за это время всё равно записывается",
                waiting && WaitFor(() => rig.StoredData() != null && rig.StoredData().balance == 40) && rig.Economy.Balance == 40);
        }

        // --- NaN в громкости (исправление v0.2.0)
        var nan = float.NaN;
        var nanSettings = SettingsProgressData.Normalized(new SettingsProgressData { musicVolume = nan, sfxVolume = nan });
        check("настройки: громкость NaN заменяется значением по умолчанию",
            nanSettings.musicVolume == SessionGameSettingsService.DefaultMusicVolume && nanSettings.sfxVolume == SessionGameSettingsService.DefaultSfxVolume);
        var infSettings = SettingsProgressData.Normalized(new SettingsProgressData { musicVolume = float.PositiveInfinity, sfxVolume = float.NegativeInfinity });
        check("настройки: бесконечность ограничивается 0..1", infSettings.musicVolume == 1f && infSettings.sfxVolume == 0f);
        var sessionSettings = new SessionGameSettingsService(nan, nan);
        check("сервис настроек: NaN в конструкторе -> значения по умолчанию",
            sessionSettings.MusicVolume == SessionGameSettingsService.DefaultMusicVolume && sessionSettings.SfxVolume == SessionGameSettingsService.DefaultSfxVolume);
        sessionSettings.SetMusicVolume(0.7f);
        sessionSettings.SetMusicVolume(nan);
        sessionSettings.SetSfxVolume(nan);
        check("сервис настроек: SetVolume(NaN) оставляет текущую громкость", sessionSettings.MusicVolume == 0.7f && !float.IsNaN(sessionSettings.SfxVolume));
        sessionSettings.Restore(true, true, nan, nan);
        check("сервис настроек: Restore(NaN) -> значения по умолчанию",
            sessionSettings.MusicVolume == SessionGameSettingsService.DefaultMusicVolume && sessionSettings.SfxVolume == SessionGameSettingsService.DefaultSfxVolume);

        // --- Выгрузка
        using (var rig = new Rig())
        {
            rig.Init();
            rig.Service.Dispose();
            rig.Economy.TryCredit("t", 3, "run_reward");
            Thread.Sleep(100);
            check("выгрузка: после Dispose изменения ничего не пишут", rig.Save.SaveCalls == 0
                && rig.Service.SaveNowAsync().GetAwaiter().GetResult().Status == PlatformServiceStatus.PlatformUnavailable);
        }
    }
}
