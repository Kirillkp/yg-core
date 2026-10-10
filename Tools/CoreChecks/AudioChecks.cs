// Проверки модуля Audio без Unity: чистые правила звука и настоящий код AudioDirector на заглушках Unity
// (источник звука запоминает все действия). Образец игрового наследника: SampleAudio.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YgCore.Audio;
using YgCore.Audio.UI;
using YgCore.Core;

static class AudioChecks
{
    private static AudioClip Clip(string name) => new AudioClip { name = name };

    private static void SetField(object target, Type type, string name, object value)
    {
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }

    // Как в инспекторе: клипы и записи звуков задаются полями директора.
    private sealed class Rig
    {
        public readonly PauseCoordinator Pause = new PauseCoordinator();
        public readonly GameplayStateService State;
        public readonly SessionGameSettingsService Settings = new SessionGameSettingsService();
        public readonly SampleGameEvents Events = new SampleGameEvents();
        public readonly SampleAudioDirector Director;
        public readonly AudioClip Music = Clip("music"), Jingle = Clip("jingle");

        public Rig(bool initialize = true)
        {
            State = new GameplayStateService(Pause);
            Director = new GameObject("Audio").AddComponent<SampleAudioDirector>();
            var type = typeof(AudioDirector);
            SetField(Director, type, "musicClip", Music);
            SetField(Director, type, "resultJingle", Jingle);
            SetField(Director, type, "sfx", new[]
            {
                Entry(SampleSfx.Click, "click"),
                Entry(SampleSfx.Coin, "coin"),
                Entry(SampleSfx.Hit, "hit"),
                Entry(SampleSfx.Splash, "splash")
            });
            SetField(Director, type, "loops", new[]
            {
                new AudioDirector.LoopEntry { id = (int)SampleSfx.DangerLoop, clip = Clip("danger"), volume = 0.55f, silenceOnUserPause = true },
                new AudioDirector.LoopEntry { id = (int)SampleSfx.BubblesLoop, clip = Clip("bubbles"), volume = 0.7f }
            });
            if (initialize)
            {
                Director.Initialize(State, Pause, Settings, Events);
            }
        }

        private static AudioDirector.SfxEntry Entry(SampleSfx id, string name, float volume = 1f)
        {
            return new AudioDirector.SfxEntry { id = (int)id, clip = Clip(name), volume = volume, minInterval = 0.05f, pitchJitter = 0.04f };
        }

        public AudioSource Source(string name) => Director.gameObject.GetComponentsInChildren<AudioSource>().First(s => s.gameObject.name == name);

        public List<(string source, AudioClip clip, float volume, float pitch)> Plays(string clipName) =>
            AudioSource.Log.Where(e => e.clip.name == clipName).ToList();
    }

    private static void Reset()
    {
        AudioSource.Log.Clear();
        AudioListener.pause = false;
        Time.unscaledTime = 100f;
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;

    public static void Run(Action<string, bool> check)
    {
        // --- Чистые правила
        check("правила: выключенная шина даёт 0", AudioPolicy.BusVolume(false, 1f, 1.5f, 0.7f) == 0f);
        check("правила: кривая громкости (0,5 в степени 1,5, умножить на базовую)", Near(AudioPolicy.BusVolume(true, 0.5f, 1.5f, 0.7f), (float)Math.Pow(0.5, 1.5) * 0.7f));
        check("правила: громкость вне 0..1 ограничивается", Near(AudioPolicy.BusVolume(true, 9f, 1.5f, 0.7f), 0.7f) && AudioPolicy.BusVolume(true, -1f, 1.5f, 0.7f) == 0f);
        check("правила: приглушение музыки по состоянию", AudioPolicy.MusicDuck(AppState.Result, 0.25f, 0.4f) == 0.25f && AudioPolicy.MusicDuck(AppState.Pause, 0.25f, 0.4f) == 0.4f && AudioPolicy.MusicDuck(AppState.Run, 0.25f, 0.4f) == 1f);
        check("правила: пауза игрока не жёсткая", !AudioPolicy.IsHardPause(PauseReason.User) && !AudioPolicy.IsHardPause(PauseReason.None));
        check("правила: реклама, платформа, покупка и фокус жёсткие",
            AudioPolicy.IsHardPause(PauseReason.Ad) && AudioPolicy.IsHardPause(PauseReason.Platform) && AudioPolicy.IsHardPause(PauseReason.Purchase)
            && AudioPolicy.IsHardPause(PauseReason.Focus) && AudioPolicy.IsHardPause(PauseReason.User | PauseReason.Ad) && AudioPolicy.IsHardPause(PauseReason.Ad | PauseReason.Countdown));
        check("правила: отсчёт звук не останавливает (ни сам, ни вместе с паузой игрока)",
            !AudioPolicy.IsHardPause(PauseReason.Countdown) && !AudioPolicy.IsHardPause(PauseReason.Countdown | PauseReason.User));
        var throttle = new SfxThrottle();
        check("ограничитель: первый звук разрешён, повтор раньше интервала нет, позже да",
            throttle.TryAcquire(1, 10f, 0.5f) && !throttle.TryAcquire(1, 10.2f, 0.5f) && throttle.TryAcquire(1, 10.6f, 0.5f) && throttle.TryAcquire(2, 10.2f, 0.5f));
        check("ограничитель: недавно ли играл", throttle.PlayedRecently(1, 10.8f, 0.6f) && !throttle.PlayedRecently(1, 12f, 0.6f) && !throttle.PlayedRecently(99, 12f, 0.6f));

        // --- Запуск директора
        Reset();
        var r = new Rig();
        check("директор: созданы источники (музыка, заставка, 10 голосов, 2 зацикленных)", r.Director.gameObject.GetComponentsInChildren<AudioSource>().Count == 1 + 1 + 10 + 2);
        check("директор: музыка играет сразу, на нужном клипе и зациклена", r.Source("Music").isPlaying && r.Source("Music").clip == r.Music && r.Source("Music").loop);
        check("директор: по умолчанию весь звук подчиняется паузе слушателя (музыка, эффекты, зацикленные)", !r.Source("Sfx 0").ignoreListenerPause && !r.Source("Loop 100").ignoreListenerPause && !r.Source("Music").ignoreListenerPause);
        check("директор: громкость музыки по настройкам (кривая, база 0,7)", Near(r.Source("Music").volume, (float)Math.Pow(0.5, 1.5) * 0.7f));
        var sourcesBefore = r.Director.gameObject.GetComponentsInChildren<AudioSource>().Count;
        r.Director.Initialize(r.State, r.Pause, r.Settings, r.Events);
        check("директор: повторный Initialize игнорируется", r.Director.gameObject.GetComponentsInChildren<AudioSource>().Count == sourcesBefore);

        // --- Настройки
        r.Settings.SetMusicEnabled(false);
        check("настройки: музыка выключена -> остановлена", !r.Source("Music").isPlaying && r.Source("Music").volume == 0f);
        r.Settings.SetMusicEnabled(true);
        r.Settings.SetMusicVolume(1f);
        check("настройки: музыка включена -> снова играет с новой громкостью", r.Source("Music").isPlaying && Near(r.Source("Music").volume, 0.7f));

        // --- Состояния и пауза
        r.State.CompleteBoot();
        r.State.StartRun();
        check("состояния: в игре музыка без приглушения", Near(r.Source("Music").volume, 0.7f));
        r.Pause.RequestPause(PauseReason.User);
        check("пауза игрока: музыка тише (0,4), звук не остановлен", Near(r.Source("Music").volume, 0.7f * 0.4f) && !AudioListener.pause);
        r.Pause.ReleasePause(PauseReason.User);
        r.Pause.ReleasePause(PauseReason.Countdown);
        r.Pause.RequestPause(PauseReason.Ad);
        check("реклама: весь звук останавливается (пауза слушателя)", AudioListener.pause);
        r.Pause.ReleasePause(PauseReason.Ad);
        check("после рекламы звук возвращается сразу, хотя отсчёт ещё идёт", !AudioListener.pause && r.Pause.HasReason(PauseReason.Countdown));
        r.Pause.ReleasePause(PauseReason.Countdown);
        r.Pause.RequestPause(PauseReason.Platform);
        check("платформа: звук останавливается", AudioListener.pause);
        r.Pause.ClearAll();
        check("пауза снята: звук продолжается, отсчёт его не держит", !AudioListener.pause);
        r.Pause.ReleasePause(PauseReason.Countdown);

        // --- Заставка результата
        Reset();
        r = new Rig();
        r.State.CompleteBoot(); r.State.StartRun(); r.Pause.ClearAll();
        r.State.FinishRun();
        check("результат: заставка играет один раз, музыка приглушена до 0,25", r.Plays("jingle").Count == 1 && Near(r.Source("Music").volume, (float)Math.Pow(0.5, 1.5) * 0.7f * 0.25f));
        Reset();
        r = new Rig();
        r.State.CompleteBoot(); r.State.StartRun(); r.Pause.ClearAll();
        r.Settings.SetMusicEnabled(false);
        r.State.FinishRun();
        check("результат: при выключенной музыке заставки нет", r.Plays("jingle").Count == 0);

        // --- Эффекты
        Reset();
        r = new Rig();
        r.Director.Play((int)SampleSfx.Click);
        r.Director.Play((int)SampleSfx.Click);
        check("эффекты: повтор в тот же момент отсекается ограничителем", r.Plays("click").Count == 1);
        Time.unscaledTime += 0.06f;
        r.Director.Play((int)SampleSfx.Click);
        check("эффекты: после интервала звук снова играет", r.Plays("click").Count == 2);
        r.Director.Play(9999);
        check("эффекты: неизвестный идентификатор молчит", AudioSource.Log.Count == 2);
        r.Settings.SetSfxEnabled(false);
        Time.unscaledTime += 1f;
        r.Director.Play((int)SampleSfx.Click);
        check("эффекты: при выключенных эффектах тишина", r.Plays("click").Count == 2);
        r.Settings.SetSfxEnabled(true);
        Time.unscaledTime += 1f;
        r.Director.Play((int)SampleSfx.Click);
        var lastClick = r.Plays("click").Last();
        check("эффекты: громкость по настройкам (кривая, база 0,45), тон без разброса", Near(lastClick.volume, (float)Math.Pow(0.1, 1.5) * 0.45f) && Near(lastClick.pitch, 1f));

        // Голоса используются по кругу
        Reset();
        r = new Rig();
        for (var i = 0; i < 11; i++)
        {
            Time.unscaledTime += 1f;
            r.Director.Play((int)SampleSfx.Click);
        }

        check("эффекты: голоса по кругу, одиннадцатый звук занимает первый голос", r.Plays("click").Count == 11 && r.Plays("click")[0].source == "Sfx 0" && r.Plays("click")[10].source == "Sfx 0" && r.Plays("click")[1].source == "Sfx 1");

        // Жёсткая пауза и новые эффекты
        Reset();
        r = new Rig();
        r.State.CompleteBoot(); r.State.StartRun();
        r.Pause.RequestPause(PauseReason.Ad);
        Time.unscaledTime += 1f;
        r.Director.Play((int)SampleSfx.Click);
        check("реклама: новые эффекты во время рекламы не запускаются", r.Plays("click").Count == 0);
        r.Pause.ReleasePause(PauseReason.Ad);
        r.Pause.ReleasePause(PauseReason.Countdown);
        Time.unscaledTime += 1f;
        r.Director.Play((int)SampleSfx.Click);
        check("после рекламы эффекты снова запускаются", r.Plays("click").Count == 1);
        r.Pause.RequestPause(PauseReason.User);
        Time.unscaledTime += 1f;
        r.Director.Play((int)SampleSfx.Click);
        check("пауза игрока: эффекты работают (звуки кнопок меню паузы)", r.Plays("click").Count == 2);
        r.Pause.ReleasePause(PauseReason.User);
        r.Pause.ReleasePause(PauseReason.Countdown);
        r.Pause.RequestPause(PauseReason.Countdown);
        Time.unscaledTime += 1f;
        r.Director.Play((int)SampleSfx.Click);
        check("отсчёт: эффекты работают", r.Plays("click").Count == 3);
        r.Pause.ClearAll();

        // Настройка: оставить эффекты звучать во время жёсткой паузы
        Reset();
        var keep = new Rig(initialize: false);
        SetField(keep.Director, typeof(AudioDirector), "silenceEffectsOnHardPause", false);
        keep.Director.Initialize(keep.State, keep.Pause, keep.Settings, keep.Events);
        keep.State.CompleteBoot(); keep.State.StartRun();
        check("настройка silenceEffectsOnHardPause=false: эффекты и зацикленные игнорируют паузу слушателя, музыка нет",
            keep.Source("Sfx 0").ignoreListenerPause && keep.Source("Loop 100").ignoreListenerPause && !keep.Source("Music").ignoreListenerPause);
        keep.Pause.RequestPause(PauseReason.Ad);
        Time.unscaledTime += 1f;
        keep.Director.Play((int)SampleSfx.Click);
        check("настройка silenceEffectsOnHardPause=false: эффекты звучат и во время рекламы", keep.Plays("click").Count == 1);

        // Звук до инициализации безопасен
        var early = new Rig(initialize: false);
        early.Director.Play((int)SampleSfx.Click);
        check("эффекты: вызов до Initialize ничего не ломает", true);

        // --- События игры через наследника
        Reset();
        r = new Rig();
        r.State.CompleteBoot(); r.State.StartRun(); r.Pause.ClearAll();
        Time.unscaledTime = 200f; r.Events.RaiseCoin();
        Time.unscaledTime = 200.3f; r.Events.RaiseCoin();
        Time.unscaledTime = 200.6f; r.Events.RaiseCoin();
        var coins = r.Plays("coin");
        check("монеты: цепочка повышает тон (1,00 -> 1,06 -> 1,12)", coins.Count == 3 && Near(coins[0].pitch, 1f) && Near(coins[1].pitch, 1.06f) && Near(coins[2].pitch, 1.12f));
        Time.unscaledTime = 210f; r.Events.RaiseCoin();
        check("монеты: после паузы в 0,9 с цепочка сбрасывается", Near(r.Plays("coin").Last().pitch, 1f));

        r.Events.RaiseLives(2);
        check("жизни: удар играет звук, опасности пока нет", r.Plays("hit").Count == 1 && !r.Director.IsLoopPlaying((int)SampleSfx.DangerLoop));
        Time.unscaledTime = 220f;
        r.Events.RaiseLives(1);
        check("жизни: последняя жизнь запускает зацикленный звук опасности", r.Plays("hit").Count == 2 && r.Director.IsLoopPlaying((int)SampleSfx.DangerLoop));
        check("опасность: громкость зацикленного = эффекты * 0,55", Near(r.Source("Loop 100").volume, (float)Math.Pow(0.1, 1.5) * 0.45f * 0.55f));
        r.Pause.RequestPause(PauseReason.User);
        check("опасность: на паузе игрока гасится, пузыри не гасятся",
            r.Source("Loop 100").volume == 0f && Near(r.Source("Loop 101").volume, (float)Math.Pow(0.1, 1.5) * 0.45f * 0.7f));
        r.Pause.ReleasePause(PauseReason.User);
        r.Pause.ReleasePause(PauseReason.Countdown);
        check("музыка: опасность ускоряет тон только во время игры", Near(r.Source("Music").pitch, 1f));
        r.Events.IsDanger = true;
        r.Settings.SetSfxVolume(0.2f);
        check("музыка: при опасности в игре тон 1,06", Near(r.Source("Music").pitch, 1.06f));

        Time.unscaledTime = 230f;
        r.Events.IsFinalFlood = true;
        r.Events.RaiseFlood(true);
        check("затопление: всплеск, пузыри запущены, опасность остановлена", r.Plays("splash").Count == 1 && r.Director.IsLoopPlaying((int)SampleSfx.BubblesLoop) && !r.Director.IsLoopPlaying((int)SampleSfx.DangerLoop));
        r.State.FinishRun();
        check("результат: пузыри при финальном затоплении продолжают звучать", r.Director.IsLoopPlaying((int)SampleSfx.BubblesLoop));
        r.State.StartRun();
        r.Pause.ClearAll();
        check("новый забег: зацикленные звуки остановлены", !r.Director.IsLoopPlaying((int)SampleSfx.BubblesLoop) && !r.Director.IsLoopPlaying((int)SampleSfx.DangerLoop));

        // --- Выгрузка
        r.Pause.RequestPause(PauseReason.Ad);
        var logBefore = AudioSource.Log.Count;
        r.Director.SendMessage_OnDestroy();
        Time.unscaledTime = 300f;
        r.Events.RaiseCoin();
        check("выгрузка: пауза звука снята и события отписаны", !AudioListener.pause && AudioSource.Log.Count == logBefore);

        // --- Подчинение источников сцены настройкам
        var applierObject = new GameObject("Applier").AddComponent<AudioSettingsApplier>();
        var musicSource = new GameObject("m").AddComponent<AudioSource>();
        var sfxSource = new GameObject("s").AddComponent<AudioSource>();
        SetField(applierObject, typeof(AudioSettingsApplier), "musicSources", new[] { musicSource });
        SetField(applierObject, typeof(AudioSettingsApplier), "sfxSources", new[] { sfxSource, null });
        var settings = new SessionGameSettingsService();
        applierObject.Initialize(settings);
        check("подчинение источников: громкость из настроек применяется сразу", Near(musicSource.volume, 0.5f) && Near(sfxSource.volume, 0.1f) && !musicSource.mute);
        settings.SetMusicEnabled(false);
        settings.SetSfxVolume(0.8f);
        check("подчинение источников: выключенная шина заглушается, громкость обновляется", musicSource.mute && Near(sfxSource.volume, 0.8f));

        // --- Звуки интерфейса
        var uiObject = new GameObject("Ui").AddComponent<UiPressSounds>();
        SetField(uiObject, typeof(UiPressSounds), "toggleSfxId", (int)SampleSfx.Coin);
        SetField(uiObject, typeof(UiPressSounds), "sliderSfxId", (int)SampleSfx.Hit);
        SetField(uiObject, typeof(UiPressSounds), "defaultSfxId", (int)SampleSfx.Click);
        Reset();
        r = new Rig();
        uiObject.Initialize(r.Director);
        var es = new EventSystem();
        EventSystem.current = es;
        Mouse.current = new Mouse();
        Touchscreen.current = null;

        Func<Selectable> button = () => new GameObject("Button").AddComponent<Selectable>();
        void Press(params GameObject[] hits)
        {
            es.Hits.Clear();
            foreach (var hit in hits) es.Hits.Add(new RaycastResult { gameObject = hit });
            Mouse.current.leftButton.wasPressedThisFrame = true;
            typeof(UiPressSounds).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(uiObject, null);
            Mouse.current.leftButton.wasPressedThisFrame = false;
            Time.unscaledTime += 1f;
        }

        var btn = button();
        Press(btn.gameObject);
        check("звук интерфейса: обычная кнопка получает общий звук нажатия", r.Plays("click").Count == 1);
        Press(new GameObject("Toggle").AddComponent<Toggle>().gameObject);
        check("звук интерфейса: переключатель получает свой звук", r.Plays("coin").Count == 1);
        Press(new GameObject("Slider").AddComponent<Slider>().gameObject);
        check("звук интерфейса: ползунок получает свой звук", r.Plays("hit").Count == 1);

        var disabled = button(); disabled.interactable = false;
        var below = button();
        var before = AudioSource.Log.Count;
        Press(disabled.gameObject, below.gameObject);
        check("звук интерфейса: недоступный элемент пропускается, звучит тот, что под ним", AudioSource.Log.Count == before + 1);
        Press(disabled.gameObject);
        check("звук интерфейса: только недоступный элемент -> тишина", AudioSource.Log.Count == before + 1);
        Press(new GameObject("Empty"));
        check("звук интерфейса: нажатие мимо элементов -> тишина", AudioSource.Log.Count == before + 1);

        uiObject.Resolver = s => s.gameObject.name == "Close" ? (int)SampleSfx.Splash : UiPressSounds.NoSound;
        var close = button(); close.gameObject.name = "Close";
        Press(close.gameObject);
        Press(button().gameObject);
        check("звук интерфейса: правила игры по имени выбирают звук, остальные получают общий", r.Plays("splash").Count == 1 && r.Plays("click").Count == 3);

        EventSystem.current = null;
        before = AudioSource.Log.Count;
        Press(button().gameObject);
        check("звук интерфейса: без EventSystem тишина", AudioSource.Log.Count == before);
        Reset();
    }

    // Вызов защищённого OnDestroy у наследника, как это делает Unity при удалении объекта.
    private static void SendMessage_OnDestroy(this AudioDirector director)
    {
        typeof(AudioDirector).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(director, null);
    }
}
