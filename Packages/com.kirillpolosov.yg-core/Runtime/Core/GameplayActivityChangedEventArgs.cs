using System;

namespace YgCore.Core
{
    /// <summary>Данные события «игровая активность началась или остановилась».</summary>
    public sealed class GameplayActivityChangedEventArgs : EventArgs
    {
        public GameplayActivityChangedEventArgs(bool wasActive, bool isActive)
        {
            WasActive = wasActive;
            IsActive = isActive;
        }

        /// <summary>Была ли игра активна до изменения.</summary>
        public bool WasActive { get; }

        /// <summary>Активна ли игра после изменения.</summary>
        public bool IsActive { get; }
    }
}
