using System;

namespace YgCore.Core
{
    /// <summary>Стандартная реализация <see cref="IPauseCoordinator"/>: хранит набор причин в одном флаговом поле.</summary>
    public sealed class PauseCoordinator : IPauseCoordinator
    {
        public event EventHandler<PauseChangedEventArgs> PauseChanged;

        public bool IsPaused => ActiveReasons != PauseReason.None;

        public PauseReason ActiveReasons { get; private set; } = PauseReason.None;

        public bool HasReason(PauseReason reason)
        {
            ValidateSingleReason(reason);
            return (ActiveReasons & reason) == reason;
        }

        public void RequestPause(PauseReason reason)
        {
            ValidateSingleReason(reason);
            SetReasons(ActiveReasons | reason);
        }

        public void ReleasePause(PauseReason reason)
        {
            ValidateSingleReason(reason);
            SetReasons(ActiveReasons & ~reason);
        }

        public void ClearAll()
        {
            SetReasons(PauseReason.None);
        }

        private void SetReasons(PauseReason newReasons)
        {
            // Событие нужно только при реальном изменении, иначе подписчики
            // получали бы лишние уведомления на повторные вызовы.
            if (newReasons == ActiveReasons)
            {
                return;
            }

            var previousReasons = ActiveReasons;
            ActiveReasons = newReasons;
            PauseChanged?.Invoke(this, new PauseChangedEventArgs(previousReasons, ActiveReasons));
        }

        private static void ValidateSingleReason(PauseReason reason)
        {
            // Проверка «ровно один бит»: x & (x - 1) обнуляет младший бит,
            // для степени двойки получится 0. Ноль (None) тоже недопустим.
            var rawReason = (int)reason;
            if (reason == PauseReason.None || (rawReason & (rawReason - 1)) != 0)
            {
                throw new ArgumentException("Причина паузы должна быть одним непустым флагом.", nameof(reason));
            }
        }
    }
}
