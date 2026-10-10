using System;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>Мок аудиофокуса: фокус меняется вручную вызовом <see cref="SetAudioFocus"/>.</summary>
    public sealed class EditorMockAudioFocusService : EditorMockPlatformService, IAudioFocusService
    {
        public event EventHandler<AudioFocusChangedEventArgs> AudioFocusChanged;

        public bool HasAudioFocus { get; private set; } = true;

        /// <summary>Меняет фокус и сообщает об этом. Повтор того же значения событие не вызывает.</summary>
        public void SetAudioFocus(bool hasAudioFocus)
        {
            if (hasAudioFocus == HasAudioFocus)
            {
                return;
            }

            var hadFocus = HasAudioFocus;
            HasAudioFocus = hasAudioFocus;
            AudioFocusChanged?.Invoke(this, new AudioFocusChangedEventArgs(hadFocus, HasAudioFocus));
        }
    }
}
