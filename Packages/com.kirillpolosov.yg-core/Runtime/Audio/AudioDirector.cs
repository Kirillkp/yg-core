using System;
using System.Collections.Generic;
using UnityEngine;
using YgCore.Core;

namespace YgCore.Audio
{
    /// <summary>
    /// Единственное место, которое играет звук: музыка, эффекты и зацикленные звуки. Игровой код звук напрямую не включает.
    ///
    /// Класс решает общие задачи: две отдельные шины (музыка и эффекты) с громкостью из настроек игрока,
    /// пул голосов для эффектов с ограничением частоты, приглушение музыки на паузе и экране результата,
    /// остановку музыки при жёсткой паузе (реклама, платформа, покупка, фокус; эффекты и зацикленные звуки
    /// при этом по умолчанию продолжают играть, см. <c>silenceEffectsOnHardPause</c>), заставку на экране результата.
    ///
    /// Что именно звучит в игре (бег, монеты, вода, магазин) знает только игра. Поэтому игра наследует этот класс,
    /// в <see cref="OnInitialized"/> подписывается на свои события и вызывает <see cref="Play"/>,
    /// <see cref="StartLoop"/> и <see cref="StopLoop"/>. Идентификаторы звуков это числа, которые игра задаёт
    /// своим enum и приводит к int.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        /// <summary>Эффект: короткий звук, который проигрывается один раз.</summary>
        [Serializable]
        public sealed class SfxEntry
        {
            [Tooltip("Числовой идентификатор звука (обычно значение enum игры).")]
            public int id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Минимальный интервал между двумя проигрываниями этого звука, секунд.")]
            public float minInterval = 0.05f;
            [Tooltip("Случайный разброс высоты тона, чтобы одинаковые звуки не звучали механически.")]
            public float pitchJitter = 0.04f;
        }

        /// <summary>Зацикленный звук (опасность, пузыри): запускается и останавливается командами.</summary>
        [Serializable]
        public sealed class LoopEntry
        {
            [Tooltip("Числовой идентификатор зацикленного звука (обычно значение enum игры).")]
            public int id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Гасить звук, пока игрок поставил паузу сам.")]
            public bool silenceOnUserPause;
        }

        // Поля с [SerializeField] заполняет инспектор Unity, поэтому предупреждение компилятора о них не нужно.
#pragma warning disable 0649
        [Header("Музыка")]
        [SerializeField] private AudioClip musicClip;
        [SerializeField] private AudioClip resultJingle;
        [SerializeField, Range(0f, 1f)] private float musicBaseVolume = 0.7f;
        [SerializeField, Range(0f, 1f), Tooltip("Во сколько раз тише музыка на экране результата.")]
        private float resultMusicDuck = 0.25f;
        [SerializeField, Range(0f, 1f), Tooltip("Во сколько раз тише музыка на паузе.")]
        private float pauseMusicDuck = 0.4f;

        [Header("Эффекты")]
        [SerializeField] private SfxEntry[] sfx = new SfxEntry[0];
        [SerializeField] private LoopEntry[] loops = new LoopEntry[0];
        [SerializeField, Range(0f, 1f)] private float sfxBaseVolume = 0.45f;
        [SerializeField, Range(1f, 3f), Tooltip("Степень кривой громкости: больше значение, тише середина ползунка.")]
        private float volumeCurve = 1.5f;
        [SerializeField, Tooltip("Сколько эффектов могут звучать одновременно.")]
        private int voices = 10;
        [SerializeField, Tooltip("Останавливать ли эффекты и зацикленные звуки при жёсткой паузе (реклама, платформа, покупка, фокус, отсчёт). По умолчанию выключено, как в «Обби Потоп»: останавливается только музыка.")]
        private bool silenceEffectsOnHardPause;
#pragma warning restore 0649

        private readonly Dictionary<int, SfxEntry> map = new Dictionary<int, SfxEntry>();
        private readonly Dictionary<int, AudioSource> loopSources = new Dictionary<int, AudioSource>();
        private readonly Dictionary<int, LoopEntry> loopEntries = new Dictionary<int, LoopEntry>();
        private readonly SfxThrottle throttle = new SfxThrottle();
        private AudioSource music;
        private AudioSource jingle;
        private AudioSource[] sfxSources;
        private int nextVoice;
        private bool initialized;

        /// <summary>Состояние игры. Доступно после <see cref="Initialize"/>.</summary>
        protected IGameplayStateService StateService { get; private set; }

        protected IPauseCoordinator PauseCoordinator { get; private set; }

        protected IGameSettingsService Settings { get; private set; }

        /// <summary>
        /// Подключает директор звука к сервисам игры. Вызывать один раз при старте, повторные вызовы игнорируются.
        /// Наследники вызывают этот метод из своего <c>Initialize</c>, передав остальные сервисы себе.
        /// </summary>
        public void Initialize(
            IGameplayStateService gameplayState,
            IPauseCoordinator pause,
            IGameSettingsService gameSettings)
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            StateService = gameplayState;
            PauseCoordinator = pause;
            Settings = gameSettings;

            BuildSources();
            foreach (var entry in sfx)
            {
                if (entry != null && entry.clip != null)
                {
                    map[entry.id] = entry;
                }
            }

            Subscribe();
            OnInitialized();
            ApplyVolumes();
            HandleState(StateService != null ? StateService.CurrentState : AppState.Menu);
        }

        /// <summary>
        /// Проигрывает эффект. Ничего не делает, если эффекты выключены, звука с таким идентификатором нет
        /// или он играл слишком недавно (<see cref="SfxEntry.minInterval"/>).
        /// </summary>
        /// <param name="id">Идентификатор звука.</param>
        /// <param name="pitch">Высота тона (1 без изменений), к ней добавляется случайный разброс.</param>
        public void Play(int id, float pitch = 1f)
        {
            // До Initialize источников звука ещё нет.
            if (sfxSources == null || (Settings != null && !Settings.IsSfxEnabled))
            {
                return;
            }

            if (!map.TryGetValue(id, out var entry))
            {
                return;
            }

            if (!throttle.TryAcquire(id, Time.unscaledTime, entry.minInterval))
            {
                return;
            }

            // Голоса берутся по кругу: новый звук занимает самый давний голос.
            var source = sfxSources[nextVoice];
            nextVoice = (nextVoice + 1) % sfxSources.Length;
            source.pitch = pitch * (1f + UnityEngine.Random.Range(-entry.pitchJitter, entry.pitchJitter));
            source.volume = entry.volume * SfxVolume();
            source.PlayOneShot(entry.clip);
        }

        /// <summary>Запускает зацикленный звук, если он ещё не играет.</summary>
        public void StartLoop(int id)
        {
            if (loopSources.TryGetValue(id, out var source)
                && source.clip != null
                && !source.isPlaying)
            {
                source.Play();
            }

            ApplyVolumes();
        }

        /// <summary>Останавливает зацикленный звук.</summary>
        public void StopLoop(int id)
        {
            if (loopSources.TryGetValue(id, out var source))
            {
                source.Stop();
            }
        }

        /// <summary>Играет ли зацикленный звук.</summary>
        public bool IsLoopPlaying(int id)
        {
            return loopSources.TryGetValue(id, out var source) && source.isPlaying;
        }

        /// <summary>Играл ли эффект за последние <paramref name="window"/> секунд.</summary>
        public bool PlayedRecently(int id, float window)
        {
            return throttle.PlayedRecently(id, Time.unscaledTime, window);
        }

        /// <summary>
        /// Вызывается один раз после подключения к сервисам, до первого применения громкости и состояния.
        /// Здесь наследник подписывается на события игры и запоминает начальные значения.
        /// </summary>
        protected virtual void OnInitialized()
        {
        }

        /// <summary>
        /// Вызывается при входе в состояние игры до остановки зацикленных звуков. Например, при входе в Run
        /// наследник сбрасывает счётчики жизней и монет.
        /// </summary>
        protected virtual void OnAppStateEntered(AppState state)
        {
        }

        /// <summary>
        /// Оставить ли зацикленный звук играть при переходе в состояние Result (по умолчанию все останавливаются).
        /// Например, пузыри при финальном затоплении продолжают звучать на экране результата.
        /// </summary>
        protected virtual bool KeepLoopOnResult(int loopId)
        {
            return false;
        }

        /// <summary>Высота тона музыки. Например, музыка ускоряется в момент опасности. По умолчанию 1.</summary>
        protected virtual float GetMusicPitch()
        {
            return 1f;
        }

        /// <summary>Применяет текущие настройки, состояние и паузу к громкости музыки и зацикленных звуков.</summary>
        protected void ApplyVolumes()
        {
            if (music == null)
            {
                return;
            }

            var state = StateService != null ? StateService.CurrentState : AppState.Menu;
            var duck = AudioPolicy.MusicDuck(state, resultMusicDuck, pauseMusicDuck);
            music.volume = MusicVolume() * duck;
            music.pitch = GetMusicPitch();

            var loopVolume = SfxVolume();
            var userPaused = PauseCoordinator != null && PauseCoordinator.HasReason(PauseReason.User);
            foreach (var pair in loopSources)
            {
                var entry = loopEntries[pair.Key];
                pair.Value.volume = loopVolume * entry.volume * (entry.silenceOnUserPause && userPaused ? 0f : 1f);
            }
        }

        /// <summary>Громкость музыки по настройкам игрока.</summary>
        protected float MusicVolume()
        {
            return Settings == null
                ? musicBaseVolume
                : AudioPolicy.BusVolume(Settings.IsMusicEnabled, Settings.MusicVolume, volumeCurve, musicBaseVolume);
        }

        /// <summary>Громкость эффектов по настройкам игрока.</summary>
        protected float SfxVolume()
        {
            return Settings == null
                ? sfxBaseVolume
                : AudioPolicy.BusVolume(Settings.IsSfxEnabled, Settings.SfxVolume, volumeCurve, sfxBaseVolume);
        }

        protected virtual void OnDestroy()
        {
            if (StateService != null) StateService.StateChanged -= OnStateChanged;
            if (PauseCoordinator != null) PauseCoordinator.PauseChanged -= OnPauseChanged;
            if (Settings != null) Settings.SettingsChanged -= OnSettingsChanged;

            // Пауза звука глобальная: при выгрузке снимаем её, чтобы не оставить следующую сцену немой.
            AudioListener.pause = false;
        }

        private void BuildSources()
        {
            music = CreateSource("Music", true, false);
            music.clip = musicClip;

            // Эффекты и зацикленные звуки по умолчанию игнорируют паузу слушателя: они должны звучать, когда
            // игра на паузе (звуки кнопок в меню паузы). Музыка и заставка паузу слушателя учитывают.
            var effectsIgnoreListenerPause = !silenceEffectsOnHardPause;
            jingle = CreateSource("Result Jingle", false, false);
            sfxSources = new AudioSource[Mathf.Max(4, voices)];
            for (var i = 0; i < sfxSources.Length; i++)
            {
                sfxSources[i] = CreateSource("Sfx " + i, false, effectsIgnoreListenerPause);
            }

            foreach (var entry in loops)
            {
                if (entry == null)
                {
                    continue;
                }

                var source = CreateSource("Loop " + entry.id, true, effectsIgnoreListenerPause);
                source.clip = entry.clip;
                loopSources[entry.id] = source;
                loopEntries[entry.id] = entry;
            }
        }

        private AudioSource CreateSource(string name, bool loop, bool ignoreListenerPause)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = ignoreListenerPause;
            return source;
        }

        private void Subscribe()
        {
            if (StateService != null) StateService.StateChanged += OnStateChanged;
            if (PauseCoordinator != null) PauseCoordinator.PauseChanged += OnPauseChanged;
            if (Settings != null) Settings.SettingsChanged += OnSettingsChanged;
        }

        private void OnStateChanged(object sender, AppStateChangedEventArgs args)
        {
            HandleState(args.CurrentState);
        }

        private void HandleState(AppState state)
        {
            OnAppStateEntered(state);
            switch (state)
            {
                case AppState.Result:
                    jingle.Stop();
                    if (resultJingle != null && (Settings == null || Settings.IsMusicEnabled))
                    {
                        jingle.PlayOneShot(resultJingle, MusicVolume());
                    }

                    StopLoops(true);
                    break;
                default:
                    StopLoops(false);
                    EnsureMusic();
                    break;
            }

            ApplyVolumes();
        }

        private void StopLoops(bool onResult)
        {
            foreach (var pair in loopSources)
            {
                if (onResult && KeepLoopOnResult(pair.Key))
                {
                    continue;
                }

                pair.Value.Stop();
            }
        }

        private void EnsureMusic()
        {
            if (music == null || musicClip == null || (Settings != null && !Settings.IsMusicEnabled))
            {
                return;
            }

            if (!music.isPlaying)
            {
                music.Play();
            }
        }

        private void OnSettingsChanged(object sender, GameSettingsChangedEventArgs args)
        {
            ApplyVolumes();
            if (Settings != null && Settings.IsMusicEnabled)
            {
                EnsureMusic();
            }
            else if (music != null)
            {
                music.Pause();
            }
        }

        private void OnPauseChanged(object sender, PauseChangedEventArgs args)
        {
            // Платформа, реклама, покупка и потеря фокуса ставят на паузу слушателя: останавливаются музыка и заставка,
            // а эффекты и зацикленные звуки зависят от настройки silenceEffectsOnHardPause. Пауза игрока только приглушает
            // музыку (в ApplyVolumes) и гасит зацикленные звуки с пометкой «гасить на паузе игрока».
            AudioListener.pause = PauseCoordinator != null
                && AudioPolicy.IsHardPause(PauseCoordinator.ActiveReasons);
            ApplyVolumes();
        }
    }
}
