using System;
using PawArena.Core.Flow;
using UnityEngine;

namespace PawArena.Gameplay.Flow
{
    /// <summary>
    /// Connects the pure game-flow state machine to Unity scenes and presentation systems.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour, IGameFlowService
    {
        [SerializeField] private GameFlowState _initialState = GameFlowState.Boot;

        private GameFlowStateMachine _stateMachine;

        /// <inheritdoc />
        public event Action<GameFlowState, GameFlowState> StateChanged;

        /// <inheritdoc />
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

        /// <inheritdoc />
        public bool CanChangeState(GameFlowState nextState)
        {
            return _stateMachine != null && _stateMachine.CanTransitionTo(nextState);
        }

        /// <inheritdoc />
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
