using System;
using PawArena.Core.Flow;
using PawArena.Core.Match;
using PawArena.Gameplay.Flow;
using UnityEngine;

namespace PawArena.Gameplay.Match
{
    /// <summary>
    /// Coordinates the offline round lifecycle until an authoritative network service replaces it.
    /// </summary>
    public sealed class MatchManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameFlowController _gameFlow = null;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float _countdownSeconds = 5f;

        private MatchStateMachine _stateMachine;
        private float _countdownRemaining;
        private int _lastReportedCountdownSecond = -1;

        /// <summary>
        /// Raised after a valid round state transition.
        /// </summary>
        public event Action<MatchState, MatchState> StateChanged;

        /// <summary>
        /// Raised when the visible countdown second changes.
        /// </summary>
        public event Action<int> CountdownChanged;

        /// <summary>
        /// Raised when participants should be spawned.
        /// </summary>
        public event Action SpawnRequested;

        /// <summary>
        /// Gets the current round state.
        /// </summary>
        public MatchState CurrentState => _stateMachine?.CurrentState ?? MatchState.Waiting;

        /// <summary>
        /// Gets the remaining pre-match countdown time.
        /// </summary>
        public float CountdownRemaining => _countdownRemaining;

        private void Awake()
        {
            _stateMachine = new MatchStateMachine();
            _stateMachine.StateChanged += HandleStateChanged;
        }

        private void Update()
        {
            if (_stateMachine.CurrentState != MatchState.Countdown)
            {
                return;
            }

            _countdownRemaining = Mathf.Max(0f, _countdownRemaining - Time.deltaTime);
            ReportCountdownIfChanged();
            if (_countdownRemaining > 0f)
            {
                return;
            }

            if (_stateMachine.TryTransitionTo(MatchState.Spawning))
            {
                SpawnRequested?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.StateChanged -= HandleStateChanged;
            }
        }

        /// <summary>
        /// Attempts to begin the pre-match countdown.
        /// </summary>
        public bool TryBeginCountdown()
        {
            if (!_stateMachine.CanTransitionTo(MatchState.Countdown))
            {
                return false;
            }

            if (_gameFlow != null && !_gameFlow.TryChangeState(GameFlowState.PreMatch))
            {
                return false;
            }

            _countdownRemaining = _countdownSeconds;
            _lastReportedCountdownSecond = -1;
            _stateMachine.TryTransitionTo(MatchState.Countdown);
            ReportCountdownIfChanged();
            return true;
        }

        /// <summary>
        /// Cancels countdown or spawning and returns to the waiting state.
        /// </summary>
        public bool TryCancelStart()
        {
            if (!_stateMachine.CanTransitionTo(MatchState.Waiting))
            {
                return false;
            }

            if (_gameFlow != null && _gameFlow.CurrentState == GameFlowState.PreMatch)
            {
                if (!_gameFlow.CanChangeState(GameFlowState.Lobby) ||
                    !_gameFlow.TryChangeState(GameFlowState.Lobby))
                {
                    return false;
                }
            }

            _countdownRemaining = 0f;
            return _stateMachine.TryTransitionTo(MatchState.Waiting);
        }

        /// <summary>
        /// Confirms that spawning completed and gameplay may begin.
        /// </summary>
        public bool TryBeginPlaying()
        {
            if (!_stateMachine.CanTransitionTo(MatchState.Playing))
            {
                return false;
            }

            if (_gameFlow != null && !_gameFlow.TryChangeState(GameFlowState.Playing))
            {
                return false;
            }

            return _stateMachine.TryTransitionTo(MatchState.Playing);
        }

        /// <summary>
        /// Locks the winner and enters result resolution.
        /// </summary>
        public bool TryResolveMatch() => _stateMachine.TryTransitionTo(MatchState.Resolving);

        /// <summary>
        /// Publishes resolved match results and updates the top-level flow.
        /// </summary>
        public bool TryShowResults()
        {
            if (!_stateMachine.CanTransitionTo(MatchState.Results))
            {
                return false;
            }

            if (_gameFlow != null && !_gameFlow.TryChangeState(GameFlowState.Results))
            {
                return false;
            }

            return _stateMachine.TryTransitionTo(MatchState.Results);
        }

        /// <summary>
        /// Resets round state and returns the experience to Lobby.
        /// </summary>
        public bool TryResetToLobby()
        {
            if (!_stateMachine.CanTransitionTo(MatchState.Resetting))
            {
                return false;
            }

            if (_gameFlow != null && _gameFlow.CurrentState == GameFlowState.Results &&
                (!_gameFlow.CanChangeState(GameFlowState.Lobby) ||
                 !_gameFlow.TryChangeState(GameFlowState.Lobby)))
            {
                return false;
            }

            _stateMachine.TryTransitionTo(MatchState.Resetting);
            return _stateMachine.TryTransitionTo(MatchState.Waiting);
        }

        private void ReportCountdownIfChanged()
        {
            var visibleSecond = Mathf.CeilToInt(_countdownRemaining);
            if (visibleSecond == _lastReportedCountdownSecond)
            {
                return;
            }

            _lastReportedCountdownSecond = visibleSecond;
            CountdownChanged?.Invoke(visibleSecond);
        }

        private void HandleStateChanged(MatchState previousState, MatchState currentState)
        {
            StateChanged?.Invoke(previousState, currentState);
        }

        private void OnValidate()
        {
            _countdownSeconds = Mathf.Max(0f, _countdownSeconds);
        }
    }
}
