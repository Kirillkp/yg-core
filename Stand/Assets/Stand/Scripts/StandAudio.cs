using System;
using System.Reflection;
using UnityEngine;
using YgCore.Audio;
using YgCore.Core;

namespace YgCoreStand
{
    /// <summary>Директор звука стенда: настоящий AudioDirector пакета, клипы которому подставляет стенд.</summary>
    public sealed class StandAudioDirector : AudioDirector
    {
        /// <summary>Идентификатор звука клика (число, как в играх: enum, приведённый к int).</summary>
        public const int ClickId = 0;

        /// <summary>
        /// Создаёт объект с директором и подставляет клипы. Поля директора закрытые и заполняются в инспекторе игры,
        /// а у стенда сцены нет, поэтому клипы записываются отражением до инициализации.
        /// </summary>
        public static StandAudioDirector Create(
            IGameplayStateService state,
            IPauseCoordinator pause,
            IGameSettingsService settings)
        {
            var go = new GameObject("StandAudio");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var director = go.AddComponent<StandAudioDirector>();

            Set(director, "musicClip", MakeMusic());
            var entry = new SfxEntry { id = ClickId, clip = MakeClick(), volume = 1f, minInterval = 0.05f, pitchJitter = 0f };
            Set(director, "sfx", new[] { entry });

            director.Initialize(state, pause, settings);
            return director;
        }

        /// <summary>Источник музыки директора: по нему стенд показывает, играет ли музыка на самом деле.</summary>
        public AudioSource MusicSource => transform.Find("Music")?.GetComponent<AudioSource>();

        private static void Set(AudioDirector target, string field, object value)
        {
            var info = typeof(AudioDirector).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (info == null) throw new MissingFieldException(nameof(AudioDirector), field);
            info.SetValue(target, value);
        }

        // Музыка: восемь секунд простого арпеджио до-ми-соль-до, чтобы было слышно, играет она или нет.
        private static AudioClip MakeMusic()
        {
            const int rate = 22050;
            const float noteSeconds = 0.25f;
            var notes = new[] { 261.63f, 329.63f, 392f, 523.25f, 392f, 329.63f, 293.66f, 349.23f };
            var samplesPerNote = (int)(rate * noteSeconds);
            var data = new float[samplesPerNote * notes.Length * 4];
            for (var i = 0; i < data.Length; i++)
            {
                var noteIndex = (i / samplesPerNote) % notes.Length;
                var t = (i % samplesPerNote) / (float)samplesPerNote;
                var envelope = Mathf.Clamp01(1f - t) * Mathf.Clamp01(t * 20f);
                data[i] = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * i / rate) * envelope * 0.5f;
            }

            var clip = AudioClip.Create("stand-music", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Эффект клика: короткий затухающий тон.
        private static AudioClip MakeClick()
        {
            const int rate = 22050;
            var data = new float[(int)(rate * 0.12f)];
            for (var i = 0; i < data.Length; i++)
            {
                var envelope = 1f - i / (float)data.Length;
                data[i] = Mathf.Sin(2f * Mathf.PI * 880f * i / rate) * envelope * 0.6f;
            }

            var clip = AudioClip.Create("stand-click", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
