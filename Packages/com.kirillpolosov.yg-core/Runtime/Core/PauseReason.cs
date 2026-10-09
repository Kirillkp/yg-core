using System;

namespace YgCore.Core
{
    /// <summary>
    /// Причины паузы. Это флаги: пауза может иметь несколько причин одновременно,
    /// и игра возобновляется только когда сняты все.
    /// </summary>
    [Flags]
    public enum PauseReason
    {
        /// <summary>Паузы нет.</summary>
        None = 0,

        /// <summary>Игрок сам нажал паузу.</summary>
        User = 1 << 0,

        /// <summary>Пауза по требованию платформы (событие от платформы, см. IGameplayLifecycleService).</summary>
        Platform = 1 << 1,

        /// <summary>Показывается реклама.</summary>
        Ad = 1 << 2,

        /// <summary>Идёт покупка.</summary>
        Purchase = 1 << 3,

        /// <summary>Игра потеряла фокус.</summary>
        Focus = 1 << 4,

        /// <summary>
        /// Отсчёт перед стартом или возобновлением. Ставится самим <see cref="GameplayStateService"/>,
        /// а снимать её должен слой игры, когда отсчёт закончился.
        /// </summary>
        Countdown = 1 << 5
    }
}
