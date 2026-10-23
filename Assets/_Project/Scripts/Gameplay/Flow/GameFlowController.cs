using System;
using PawArena.Core.Flow;
using UnityEngine;

namespace PawArena.Gameplay.Flow
{
    /// <summary>
    /// Connects the pure game-flow state machine to Unity scenes and presentation systems.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] private GameFlowState _initialState = GameFlowState.Boot;

        private GameFlowStateMachine _stateMachine;

        /// <summary>
        /// Raised after a valid transition with the previous and current states.
        /// </summary>
        public event Action<GameFlowState, GameFlowState> StateChanged;

        /// <summary>
        /// Gets the current state, including before Unity invokes Awake.
        /// </summary>
        public GameFlowState CurrentState => _stateMachine?.CurrentState ?? _initialState;

        private void Awake()
        {
            _stateMachine = new GameFlowStateMachine(_initialState);
            _stateMachine.StateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.StateChanged -= HandleStateChanged;
            }
        }

        /// <summary>
        /// Attempts to enter the requested state.
        /// </summary>
        public bool TryChangeState(GameFlowState nextState)
        {
            if (_stateMachine == null)
            {
                Debug.LogError($"{nameof(GameFlowController)} is not initialized.", this);
                return false;
            }

            return _stateMachine.TryTransitionTo(nextState);
        }

        private void HandleStateChanged(GameFlowState previousState, GameFlowState currentState)
        {
            StateChanged?.Invoke(previousState, currentState);
        }
    }
}
