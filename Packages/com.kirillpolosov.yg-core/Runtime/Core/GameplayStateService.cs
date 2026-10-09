using System;

namespace YgCore.Core
{
    /// <summary>
    /// Стандартная реализация <see cref="IGameplayStateService"/>.
    /// Подписывается на <see cref="IPauseCoordinator"/> и сама переводит игру в состояние Pause и обратно.
    ///
    /// ВАЖНО: сервис сам ставит причину <see cref="PauseReason.Countdown"/> (при старте не из меню
    /// и при возобновлении после паузы). Если игра не снимет эту причину, она останется на паузе.
    /// Снимать причину должен слой игры после отсчёта «3-2-1».
    /// </summary>
    public sealed class GameplayStateService : IGameplayStateService
    {
        private readonly IPauseCoordinator pauseCoordinator;

        // Состояние, в которое нужно вернуться после снятия паузы.
        private AppState resumeState = AppState.Run;

        public GameplayStateService(IPauseCoordinator pauseCoordinator)
        {
            this.pauseCoordinator = pauseCoordinator ?? throw new ArgumentNullException(nameof(pauseCoordinator));
            this.pauseCoordinator.PauseChanged += HandlePauseChanged;
        }

        public event EventHandler<AppStateChangedEventArgs> StateChanged;

        public event EventHandler<GameplayActivityChangedEventArgs> GameplayActivityChanged;

        public AppState CurrentState { get; private set; } = AppState.Boot;

        public bool IsGameplayActive { get; private set; }

        public void CompleteBoot()
        {
            MoveTo(AppState.Menu);
        }

        public void StartRun()
        {
            resumeState = AppState.Run;

            // Из меню игра выходит на трассу с собственной анимацией перехода,
            // поэтому отсчёт нужен только при перезапуске и возрождении за рекламу.
            if (CurrentState != AppState.Menu)
            {
                pauseCoordinator.RequestPause(PauseReason.Countdown);
            }

            MoveTo(pauseCoordinator.IsPaused ? AppState.Pause : AppState.Run);
            SetGameplayActive(CurrentState == AppState.Run);
        }

        public void FinishRun()
        {
            resumeState = AppState.Result;
            MoveTo(AppState.Result);
            NotifyGameplayStopped();
            pauseCoordinator.ReleasePause(PauseReason.Countdown);
        }

        public void ReturnToMenu()
        {
            resumeState = AppState.Menu;
            MoveTo(AppState.Menu);
            NotifyGameplayStopped();
            pauseCoordinator.ReleasePause(PauseReason.Countdown);
        }

        public void NotifyGameplayStarted()
        {
            SetGameplayActive(true);
        }

        public void NotifyGameplayStopped()
        {
            SetGameplayActive(false);
        }

        private void HandlePauseChanged(object sender, PauseChangedEventArgs args)
        {
            if (args.IsPaused)
            {
                // Пауза имеет смысл только во время игры: в меню и на экране результата
                // состояние не меняем.
                if (CurrentState == AppState.Run)
                {
                    resumeState = AppState.Run;
                    MoveTo(AppState.Pause);
                    NotifyGameplayStopped();
                }

                return;
            }

            if (CurrentState == AppState.Pause)
            {
                // Перед возвратом в игру всегда делаем отсчёт: сначала ставим Countdown
                // и выходим, возврат произойдёт, когда игра снимет и его. Исключение: паузу
                // только что снял сам отсчёт, тогда продолжаем сразу.
                // Игровая активность остаётся выключенной до уведомления подписчиков Run.
                if (resumeState == AppState.Run && args.PreviousReasons != PauseReason.Countdown)
                {
                    pauseCoordinator.RequestPause(PauseReason.Countdown);
                    return;
                }

                MoveTo(resumeState);
                SetGameplayActive(CurrentState == AppState.Run);
            }
        }

        private void MoveTo(AppState nextState)
        {
            if (nextState == CurrentState)
            {
                return;
            }

            var previousState = CurrentState;
            CurrentState = nextState;
            StateChanged?.Invoke(this, new AppStateChangedEventArgs(previousState, CurrentState));
        }

        private void SetGameplayActive(bool isActive)
        {
            if (isActive == IsGameplayActive)
            {
                return;
            }

            var wasActive = IsGameplayActive;
            IsGameplayActive = isActive;
            GameplayActivityChanged?.Invoke(this, new GameplayActivityChangedEventArgs(wasActive, IsGameplayActive));
        }
    }
}
