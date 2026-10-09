using System;

namespace YgCore.Core
{
    /// <summary>Данные события смены состояния приложения.</summary>
    public sealed class AppStateChangedEventArgs : EventArgs
    {
        public AppStateChangedEventArgs(AppState previousState, AppState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
        }

        /// <summary>Состояние до перехода.</summary>
        public AppState PreviousState { get; }

        /// <summary>Состояние после перехода.</summary>
        public AppState CurrentState { get; }
    }
}
