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
            MusicVolume = Mathf.Clamp01(defaultMusicVolume);
            SfxVolume = Mathf.Clamp01(defaultSfxVolume);
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
            var normalizedVolume = Mathf.Clamp01(volume);
            if (Mathf.Approximately(MusicVolume, normalizedVolume))
            {
                return;
            }

            MusicVolume = normalizedVolume;
            NotifyChanged();
        }

        public void SetSfxVolume(float volume)
        {
            var normalizedVolume = Mathf.Clamp01(volume);
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
            MusicVolume = Mathf.Clamp01(musicVolume);
            SfxVolume = Mathf.Clamp01(sfxVolume);
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
