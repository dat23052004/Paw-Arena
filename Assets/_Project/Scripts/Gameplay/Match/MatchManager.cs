using System;
using PawArena.Core.Flow;
using PawArena.Core.Match;
using PawArena.Gameplay.Flow;
using UnityEngine;

namespace PawArena.Gameplay.Match
{
    /// <summary>
    /// Presents the authoritative round lifecycle without choosing map, participants, or start time.
    /// </summary>
    public sealed class MatchManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameFlowController _gameFlow = null;

        private MatchStateMachine _stateMachine;
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
        public event Action<MatchAssignment> SpawnRequested;

        /// <summary>
        /// Gets the current round state.
        /// </summary>
        public MatchState CurrentState => _stateMachine?.CurrentState ?? MatchState.Waiting;

        /// <summary>
        /// Gets the remaining pre-match countdown time.
        /// </summary>
        public float CountdownRemaining => CurrentAssignment == null
            ? 0f
            : (float)Math.Max(0d, (CurrentAssignment.StartsAtUtc - DateTimeOffset.UtcNow).TotalSeconds);

        /// <summary>Gets the latest assignment received from the authoritative server.</summary>
        public MatchAssignment CurrentAssignment { get; private set; }

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

            ReportCountdownIfChanged();
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
        public bool TryBeginCountdown(MatchAssignment assignment)
        {
            if (assignment == null)
            {
                return false;
            }

            if (!_stateMachine.CanTransitionTo(MatchState.Countdown))
            {
                return false;
            }

            if (_gameFlow != null && !_gameFlow.TryChangeState(GameFlowState.PreMatch))
            {
                return false;
            }

            CurrentAssignment = assignment;
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

            CurrentAssignment = null;
            return _stateMachine.TryTransitionTo(MatchState.Waiting);
        }

        /// <summary>Accepts the server's start authorization for the current assignment.</summary>
        public bool TryAuthorizeSpawn(MatchAssignment assignment)
        {
            if (assignment == null || CurrentAssignment == null ||
                !string.Equals(assignment.MatchId, CurrentAssignment.MatchId, StringComparison.Ordinal) ||
                !_stateMachine.CanTransitionTo(MatchState.Spawning))
            {
                return false;
            }

            CurrentAssignment = assignment;
            if (!_stateMachine.TryTransitionTo(MatchState.Spawning))
            {
                return false;
            }

            SpawnRequested?.Invoke(assignment);
            return true;
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

        /// <summary>Moves only the local top-level flow into spectator presentation.</summary>
        public bool TryBeginSpectating()
        {
            if (_stateMachine.CurrentState != MatchState.Playing)
            {
                return false;
            }

            return _gameFlow == null ||
                   (_gameFlow.CurrentState == GameFlowState.Playing &&
                    _gameFlow.TryChangeState(GameFlowState.Spectating));
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
            CurrentAssignment = null;
            return _stateMachine.TryTransitionTo(MatchState.Waiting);
        }

        private void ReportCountdownIfChanged()
        {
            var visibleSecond = Mathf.CeilToInt(CountdownRemaining);
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

    }
}
