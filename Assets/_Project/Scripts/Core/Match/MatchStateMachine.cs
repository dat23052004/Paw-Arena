using System;

namespace PawArena.Core.Match
{
    /// <summary>
    /// Enforces legal match lifecycle transitions without Unity dependencies.
    /// </summary>
    public sealed class MatchStateMachine
    {
        /// <summary>
        /// Creates a match state machine.
        /// </summary>
        public MatchStateMachine(MatchState initialState = MatchState.Waiting)
        {
            CurrentState = initialState;
        }

        /// <summary>
        /// Raised after a valid state transition.
        /// </summary>
        public event Action<MatchState, MatchState> StateChanged;

        /// <summary>
        /// Gets the active round state.
        /// </summary>
        public MatchState CurrentState { get; private set; }

        /// <summary>
        /// Returns whether a requested state may follow the current state.
        /// </summary>
        public bool CanTransitionTo(MatchState nextState)
        {
            if (CurrentState == nextState)
            {
                return false;
            }

            return CurrentState switch
            {
                MatchState.Waiting => nextState == MatchState.Countdown,
                MatchState.Countdown => nextState is MatchState.Waiting or MatchState.Spawning,
                MatchState.Spawning => nextState is MatchState.Waiting or MatchState.Playing,
                MatchState.Playing => nextState == MatchState.Resolving,
                MatchState.Resolving => nextState == MatchState.Results,
                MatchState.Results => nextState == MatchState.Resetting,
                MatchState.Resetting => nextState == MatchState.Waiting,
                _ => false
            };
        }

        /// <summary>
        /// Attempts to enter a requested state.
        /// </summary>
        public bool TryTransitionTo(MatchState nextState)
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
