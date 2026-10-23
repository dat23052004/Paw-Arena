using System;

namespace PawArena.Core.Flow
{
    /// <summary>
    /// Owns legal transitions for the top-level game flow without Unity scene dependencies.
    /// </summary>
    public sealed class GameFlowStateMachine
    {
        /// <summary>
        /// Raised after a valid state transition with the previous and current states.
        /// </summary>
        public event Action<GameFlowState, GameFlowState> StateChanged;

        /// <summary>
        /// Gets the active game-flow state.
        /// </summary>
        public GameFlowState CurrentState { get; private set; }

        /// <summary>
        /// Creates a state machine at the provided initial state.
        /// </summary>
        public GameFlowStateMachine(GameFlowState initialState = GameFlowState.Boot)
        {
            CurrentState = initialState;
        }

        /// <summary>
        /// Returns whether the requested state can follow the current state.
        /// </summary>
        public bool CanTransitionTo(GameFlowState nextState)
        {
            if (CurrentState == nextState)
            {
                return false;
            }

            return CurrentState switch
            {
                GameFlowState.Boot => nextState == GameFlowState.Lobby,
                GameFlowState.Lobby => nextState == GameFlowState.PreMatch,
                GameFlowState.PreMatch => nextState is GameFlowState.Lobby or GameFlowState.Playing,
                GameFlowState.Playing => nextState is GameFlowState.Spectating or GameFlowState.Results,
                GameFlowState.Spectating => nextState == GameFlowState.Results,
                GameFlowState.Results => nextState is GameFlowState.Lobby or GameFlowState.PreMatch,
                _ => false
            };
        }

        /// <summary>
        /// Attempts to change state and returns false when the transition is not legal.
        /// </summary>
        public bool TryTransitionTo(GameFlowState nextState)
        {
            if (!CanTransitionTo(nextState))
            {
                return false;
            }

            var previousState = CurrentState;
            CurrentState = nextState;
            StateChanged?.Invoke(previousState, CurrentState);
            return true;
        }
    }
}
