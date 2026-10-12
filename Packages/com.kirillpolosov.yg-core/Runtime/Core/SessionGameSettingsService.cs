using System;
using UnityEngine;

namespace YgCore.Core
{
    /// <summary>
    /// Настройки звука в памяти на время сессии. Между запусками их сохраняет слой сохранений:
    /// при загрузке он вызывает <see cref="Restore"/>.
    /// </summary>
    public sealed class SessionGameSettingsService : IGameSettingsService
    {
        /// <summary>Громкость музыки по умолчанию.</summary>
        public const float DefaultMusicVolume = 0.5f;

        /// <summary>Громкость эффектов по умолчанию. Эффекты намеренно тише музыки.</summary>
        public const float DefaultSfxVolume = 0.1f;

        public SessionGameSettingsService()
            : this(DefaultMusicVolume, DefaultSfxVolume)
        {
        }

        /// <summary>Позволяет игре задать свои стартовые громкости (значения ограничиваются диапазоном 0–1).</summary>
        public SessionGameSettingsService(float defaultMusicVolume, float defaultSfxVolume)
        {
            MusicVolume = SanitizeVolume(defaultMusicVolume, DefaultMusicVolume);
            SfxVolume = SanitizeVolume(defaultSfxVolume, DefaultSfxVolume);
        }

        /// <summary>
        /// Приводит громкость к диапазону 0–1. <c>NaN</c> заменяется запасным значением:
        /// <c>Mathf.Clamp01</c> его пропускает (все сравнения с NaN ложны), и громкость осталась бы NaN,
        /// а дальше NaN попал бы в сохранение и в звук.
        /// </summary>
        public static float SanitizeVolume(float volume, float fallback)
        {
            return float.IsNaN(volume) ? Mathf.Clamp01(fallback) : Mathf.Clamp01(volume);
        }

        public event EventHandler<GameSettingsChangedEventArgs> SettingsChanged;

        public bool IsMusicEnabled { get; private set; } = true;

        public bool IsSfxEnabled { get; private set; } = true;

        public float MusicVolume { get; private set; }

        public float SfxVolume { get; private set; }

        public void SetMusicEnabled(bool isEnabled)
        {
            if (IsMusicEnabled == isEnabled)
            {
                return;
            }

            IsMusicEnabled = isEnabled;
            NotifyChanged();
        }

        public void SetSfxEnabled(bool isEnabled)
        {
            if (IsSfxEnabled == isEnabled)
            {
                return;
            }

            IsSfxEnabled = isEnabled;
            NotifyChanged();
        }

        public void SetMusicVolume(float volume)
        {
            // NaN от интерфейса (например, пустой ползунок) игнорируется: текущая громкость остаётся.
            var normalizedVolume = SanitizeVolume(volume, MusicVolume);
            if (Mathf.Approximately(MusicVolume, normalizedVolume))
            {
                return;
            }

            MusicVolume = normalizedVolume;
            NotifyChanged();
        }

        public void SetSfxVolume(float volume)
        {
            var normalizedVolume = SanitizeVolume(volume, SfxVolume);
            if (Mathf.Approximately(SfxVolume, normalizedVolume))
            {
                return;
            }

            SfxVolume = normalizedVolume;
            NotifyChanged();
        }

        /// <summary>
        /// Устанавливает все настройки разом из сохранения и всегда сообщает об изменении,
        /// чтобы аудио гарантированно применило загруженные значения.
        /// </summary>
        public void Restore(
            bool isMusicEnabled,
            bool isSfxEnabled,
            float musicVolume,
            float sfxVolume)
        {
            IsMusicEnabled = isMusicEnabled;
            IsSfxEnabled = isSfxEnabled;
            // Испорченное значение в сохранении (NaN) заменяется громкостью по умолчанию.
            MusicVolume = SanitizeVolume(musicVolume, DefaultMusicVolume);
            SfxVolume = SanitizeVolume(sfxVolume, DefaultSfxVolume);
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            SettingsChanged?.Invoke(
                this,
                new GameSettingsChangedEventArgs(
                    IsMusicEnabled,
                    IsSfxEnabled,
                    MusicVolume,
                    SfxVolume));
        }
    }
}
