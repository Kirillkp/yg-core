using System;

namespace YgCore.Contracts
{
    /// <summary>Данные события изменения аудиофокуса.</summary>
    public sealed class AudioFocusChangedEventArgs : EventArgs
    {
        public AudioFocusChangedEventArgs(bool hadFocus, bool hasFocus)
        {
            HadFocus = hadFocus;
            HasFocus = hasFocus;
        }

        /// <summary>Был ли фокус до изменения.</summary>
        public bool HadFocus { get; }

        /// <summary>Есть ли фокус после изменения.</summary>
        public bool HasFocus { get; }
    }
}
