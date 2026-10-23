using System;

namespace PawArena.Core.Flow
{
    /// <summary>
    /// Exposes legal top-level game-flow transitions.
    /// </summary>
    public interface IGameFlowService
    {
        /// <summary>
        /// Raised after a valid game-flow transition.
        /// </summary>
        event Action<GameFlowState, GameFlowState> StateChanged;

        /// <summary>
        /// Gets the active flow state.
        /// </summary>
        GameFlowState CurrentState { get; }

        /// <summary>
        /// Returns whether a requested transition is currently legal.
        /// </summary>
        bool CanChangeState(GameFlowState nextState);

        /// <summary>
        /// Attempts to enter a requested flow state.
        /// </summary>
        bool TryChangeState(GameFlowState nextState);
    }
}
