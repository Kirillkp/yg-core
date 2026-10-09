using System;

namespace YgCore.Contracts
{
    /// <summary>
    /// Аудиофокус: платформа сообщает, можно ли сейчас играть звук.
    /// При потере фокуса звук нужно останавливать, при возврате продолжать.
    /// </summary>
    public interface IAudioFocusService
    {
        event EventHandler<AudioFocusChangedEventArgs> AudioFocusChanged;

        bool IsPlatformAvailable { get; }

        /// <summary>Есть ли сейчас аудиофокус.</summary>
        bool HasAudioFocus { get; }
    }
}
