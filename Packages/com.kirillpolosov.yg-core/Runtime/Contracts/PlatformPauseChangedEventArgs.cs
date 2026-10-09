using System;

namespace YgCore.Contracts
{
    /// <summary>Данные события «платформа просит поставить игру на паузу или снять её».</summary>
    public sealed class PlatformPauseChangedEventArgs : EventArgs
    {
        public PlatformPauseChangedEventArgs(bool isPaused)
        {
            IsPaused = isPaused;
        }

        public bool IsPaused { get; }
    }
}
