using System;

namespace YgCore.Core
{
    /// <summary>
    /// Хранит текущее состояние приложения (<see cref="AppState"/>) и флаг активного геймплея.
    /// Связан с паузой: пауза переводит игру в состояние Pause, снятие паузы возвращает обратно.
    /// </summary>
    public interface IGameplayStateService
    {
        /// <summary>Срабатывает при каждой смене состояния приложения.</summary>
        event EventHandler<AppStateChangedEventArgs> StateChanged;

        /// <summary>Срабатывает, когда игровая активность включается или выключается.
        /// Платформе нужно сообщать о начале и конце геймплея именно по этому событию.</summary>
        event EventHandler<GameplayActivityChangedEventArgs> GameplayActivityChanged;

        /// <summary>Текущее состояние приложения.</summary>
        AppState CurrentState { get; }

        /// <summary>Идёт ли сейчас активный геймплей (состояние Run и нет паузы).</summary>
        bool IsGameplayActive { get; }

        /// <summary>Загрузка завершена: переход Boot → Menu.</summary>
        void CompleteBoot();

        /// <summary>Начать игровую сессию. Из меню старт без отсчёта, во всех остальных случаях
        /// включается пауза с причиной Countdown, которую должна снять игра.</summary>
        void StartRun();

        /// <summary>Закончить сессию: переход в Result.</summary>
        void FinishRun();

        /// <summary>Вернуться в меню.</summary>
        void ReturnToMenu();

        /// <summary>Явно сообщить, что игровая активность началась (для случаев вне цикла Run).</summary>
        void NotifyGameplayStarted();

        /// <summary>Явно сообщить, что игровая активность остановилась.</summary>
        void NotifyGameplayStopped();
    }
}
