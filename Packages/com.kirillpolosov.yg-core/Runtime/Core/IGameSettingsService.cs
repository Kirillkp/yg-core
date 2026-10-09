using System;

namespace YgCore.Core
{
    /// <summary>Настройки звука, которые игрок меняет в меню настроек.</summary>
    public interface IGameSettingsService
    {
        /// <summary>Срабатывает при любом реальном изменении настроек.</summary>
        event EventHandler<GameSettingsChangedEventArgs> SettingsChanged;

        bool IsMusicEnabled { get; }

        bool IsSfxEnabled { get; }

        /// <summary>Громкость музыки от 0 до 1.</summary>
        float MusicVolume { get; }

        /// <summary>Громкость звуковых эффектов от 0 до 1.</summary>
        float SfxVolume { get; }

        void SetMusicEnabled(bool isEnabled);

        void SetSfxEnabled(bool isEnabled);

        /// <summary>Устанавливает громкость музыки. Значение ограничивается диапазоном 0–1.</summary>
        void SetMusicVolume(float volume);

        /// <summary>Устанавливает громкость эффектов. Значение ограничивается диапазоном 0–1.</summary>
        void SetSfxVolume(float volume);
    }
}
