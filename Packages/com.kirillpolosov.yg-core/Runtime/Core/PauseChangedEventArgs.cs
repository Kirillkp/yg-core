using System;

namespace YgCore.Core
{
    /// <summary>Данные события изменения набора причин паузы.</summary>
    public sealed class PauseChangedEventArgs : EventArgs
    {
        public PauseChangedEventArgs(PauseReason previousReasons, PauseReason currentReasons)
        {
            PreviousReasons = previousReasons;
            CurrentReasons = currentReasons;
        }

        /// <summary>Причины паузы до изменения.</summary>
        public PauseReason PreviousReasons { get; }

        /// <summary>Причины паузы после изменения.</summary>
        public PauseReason CurrentReasons { get; }

        /// <summary>Была ли пауза до изменения.</summary>
        public bool WasPaused => PreviousReasons != PauseReason.None;

        /// <summary>Есть ли пауза после изменения.</summary>
        public bool IsPaused => CurrentReasons != PauseReason.None;
    }
}
