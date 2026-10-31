using PawArena.Gameplay.Characters;
using PawArena.Core.Flow;
using PawArena.Gameplay.Flow;
using PawArena.Gameplay.Player;
using UnityEngine;

namespace PawArena.Bootstrap
{
    /// <summary>Step-one single-scene wiring. No rounds, timer, combat, inventory writes or bot spawning.</summary>
    public sealed class CharacterReviewSession : MonoBehaviour
    {
        [SerializeField] private CatPlayerController _player;
        [SerializeField] private CharacterReviewCamera _camera;
        [SerializeField] private GameFlowController _gameFlow;
        private CatLocomotion _motor;
        private void OnEnable()
        {
            if (_gameFlow != null) { _gameFlow.StateChanged += HandleFlowChanged; }
            if (_player != null) { _player.TestControlModeToggleRequested += ToggleTestFlow; }
        }
        private void Start()
        {
            _motor = _player.GetComponent<CatLocomotion>();
            _player.BindCamera(_camera.GetComponent<Camera>()); _camera.Follow(_player.transform);
            ApplyControlMode(_gameFlow == null ? GameFlowState.Lobby : _gameFlow.CurrentState);
        }
        private void Update()
        {
            if (_motor != null && (_gameFlow == null || _gameFlow.CurrentState == GameFlowState.Lobby) &&
                _player.transform.position.y < -8)
            {
                _player.ResetPlayer();
            }
        }
        private void OnDisable()
        {
            if (_gameFlow != null) { _gameFlow.StateChanged -= HandleFlowChanged; }
            if (_player != null) { _player.TestControlModeToggleRequested -= ToggleTestFlow; }
        }
        private void HandleFlowChanged(GameFlowState previousState, GameFlowState currentState) => ApplyControlMode(currentState);
        private void ApplyControlMode(GameFlowState state) =>
            _player.SetControlMode(state == GameFlowState.Playing ? CatPlayerControlMode.PartyMatch : CatPlayerControlMode.Lobby);

        /// <summary>Test-only shortcut that follows the legal game-flow path between Lobby and Playing.</summary>
        private void ToggleTestFlow()
        {
            if (_gameFlow == null)
            {
                var next = _player.ControlMode == CatPlayerControlMode.Lobby
                    ? CatPlayerControlMode.PartyMatch
                    : CatPlayerControlMode.Lobby;
                _player.SetControlMode(next);
                return;
            }

            if (_gameFlow.CurrentState == GameFlowState.Lobby)
            {
                _gameFlow.TryChangeState(GameFlowState.PreMatch);
                _gameFlow.TryChangeState(GameFlowState.Playing);
            }
            else if (_gameFlow.CurrentState == GameFlowState.Playing)
            {
                _gameFlow.TryChangeState(GameFlowState.Results);
                _gameFlow.TryChangeState(GameFlowState.Lobby);
            }
        }
    }
}
