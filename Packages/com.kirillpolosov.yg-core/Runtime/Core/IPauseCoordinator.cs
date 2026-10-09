using System;

namespace YgCore.Core
{
    /// <summary>
    /// Собирает причины паузы в одном месте. Игра на паузе, пока активна хотя бы одна причина.
    /// Так реклама не может возобновить игру, пока игрок сам держит паузу, и наоборот.
    /// </summary>
    public interface IPauseCoordinator
    {
        /// <summary>Срабатывает при любом изменении набора причин.</summary>
        event EventHandler<PauseChangedEventArgs> PauseChanged;

        /// <summary>Есть ли хотя бы одна активная причина.</summary>
        bool IsPaused { get; }

        /// <summary>Все активные причины паузы.</summary>
        PauseReason ActiveReasons { get; }

        /// <summary>Активна ли указанная причина. Принимает ровно один флаг.</summary>
        bool HasReason(PauseReason reason);

        /// <summary>Добавляет причину. Повторный вызов с той же причиной ничего не меняет.</summary>
        void RequestPause(PauseReason reason);

        /// <summary>Снимает причину. Если её не было, ничего не происходит.</summary>
        void ReleasePause(PauseReason reason);

        /// <summary>Снимает все причины сразу (например, при выходе в меню).</summary>
        void ClearAll();
    }
}
