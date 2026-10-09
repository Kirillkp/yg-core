using System;

namespace YgCore.Core
{
    /// <summary>Данные события изменения настроек звука.</summary>
    public sealed class GameSettingsChangedEventArgs : EventArgs
    {
        public GameSettingsChangedEventArgs(
            bool isMusicEnabled,
            bool isSfxEnabled,
            float musicVolume,
            float sfxVolume)
        {
            IsMusicEnabled = isMusicEnabled;
            IsSfxEnabled = isSfxEnabled;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
        }

        /// <summary>Включена ли музыка.</summary>
        public bool IsMusicEnabled { get; }

        /// <summary>Включены ли звуковые эффекты.</summary>
        public bool IsSfxEnabled { get; }

        /// <summary>Громкость музыки от 0 до 1.</summary>
        public float MusicVolume { get; }

        /// <summary>Громкость звуковых эффектов от 0 до 1.</summary>
        public float SfxVolume { get; }
    }
}
