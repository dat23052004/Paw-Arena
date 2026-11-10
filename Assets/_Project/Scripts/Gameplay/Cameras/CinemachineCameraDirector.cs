using PawArena.Core.Flow;
using PawArena.Gameplay.Flow;
using Unity.Cinemachine;
using UnityEngine;

namespace PawArena.Gameplay.Cameras
{
    /// <summary>Maps top-level game flow to one real camera and four Cinemachine views.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class CinemachineCameraDirector : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private CameraSystemConfig _config;

        [Header("Scene services")]
        [SerializeField] private Camera _outputCamera;
        [SerializeField] private CameraAimController _aim;
        [SerializeField] private GameFlowController _gameFlow;

        [Header("Virtual cameras")]
        [SerializeField] private CinemachineCamera _lobbyCamera;
        [SerializeField] private CinemachineCamera _matchCamera;
        [SerializeField] private CinemachineCamera _spectatorCamera;
        [SerializeField] private CinemachineCamera _resultsCamera;

        [Header("Procedural rigs")]
        [SerializeField] private CinemachineThirdPersonFollow _lobbyFollow;
        [SerializeField] private CinemachineThirdPersonFollow _matchFollow;
        [SerializeField] private CinemachineThirdPersonFollow _spectatorFollow;
        [SerializeField] private CinemachineOrbitalFollow _resultsOrbit;

        private Transform _defaultTarget;
        private Transform _spectatorTarget;
        private CinemachineCamera _activeCamera;
        private GameFlowState _currentState = GameFlowState.Boot;

        public Camera OutputCamera => _outputCamera;
        public ICameraHeadingProvider HeadingProvider => _aim;

        private void Awake()
        {
            if (_config == null || _outputCamera == null || _aim == null || _lobbyCamera == null ||
                _matchCamera == null || _spectatorCamera == null ||
                _resultsCamera == null)
            {
                Debug.LogError($"{nameof(CinemachineCameraDirector)} has incomplete scene references.", this);
                enabled = false;
                return;
            }

            _aim.ApplySystemConfig(_config);
        }

        private void OnEnable()
        {
            if (_gameFlow != null)
            {
                _gameFlow.StateChanged += HandleFlowChanged;
            }
        }

        private void Start()
        {
            ApplyState(_gameFlow == null ? GameFlowState.Lobby : _gameFlow.CurrentState);
        }

        private void Update()
        {
            if (_activeCamera == _lobbyCamera && _lobbyFollow != null)
            {
                _lobbyFollow.CameraDistance = _aim.Distance;
            }
            else if (_activeCamera == _matchCamera && _matchFollow != null)
            {
                _matchFollow.CameraDistance = _aim.Distance;
            }
            else if (_activeCamera == _spectatorCamera && _spectatorFollow != null)
            {
                _spectatorFollow.CameraDistance = _aim.Distance;
            }
            else if (_activeCamera == _resultsCamera && _resultsOrbit != null)
            {
                var axis = _resultsOrbit.HorizontalAxis;
                axis.Value = Mathf.Repeat(
                    axis.Value + _config.ResultsOrbitSpeed * Time.unscaledDeltaTime + 180,
                    360) - 180;
                _resultsOrbit.HorizontalAxis = axis;
            }
        }

        private void OnDisable()
        {
            if (_gameFlow != null)
            {
                _gameFlow.StateChanged -= HandleFlowChanged;
            }
        }

        /// <summary>Sets the player target used by lobby, match and fallback result views.</summary>
        public void SetDefaultTarget(Transform target)
        {
            _defaultTarget = target;
            if (_spectatorTarget == null)
            {
                FollowAimTarget(target);
            }

            if (_currentState == GameFlowState.Results && _spectatorTarget == null)
            {
                SetResultsTarget(target);
            }
        }

        /// <summary>Moves the spectator camera to another actor without changing the flow state.</summary>
        public void SetSpectatorTarget(Transform target)
        {
            _spectatorTarget = target;
            FollowAimTarget(target);
            SetResultsTarget(target);

            if (_currentState == GameFlowState.Spectating)
            {
                _spectatorCamera.CancelDamping();
            }
            else if (_currentState == GameFlowState.Results)
            {
                _resultsCamera.CancelDamping();
            }
        }

        /// <summary>Returns all follow views to the local player.</summary>
        public void RestoreDefaultTarget()
        {
            _spectatorTarget = null;
            FollowAimTarget(_defaultTarget);
            SetResultsTarget(_defaultTarget);
            _activeCamera?.CancelDamping();
        }

        public void AlignBehindTarget() => _aim?.AlignBehindTarget();

        private void HandleFlowChanged(GameFlowState previousState, GameFlowState currentState) =>
            ApplyState(currentState);

        private void ApplyState(GameFlowState state)
        {
            _currentState = state;
            switch (state)
            {
                case GameFlowState.PreMatch:
                    _aim.ApplyPreset(_config.PreMatch);
                    Activate(_lobbyCamera);
                    break;
                case GameFlowState.Playing:
                    _spectatorTarget = null;
                    FollowAimTarget(_defaultTarget);
                    _aim.ApplyPreset(_config.Match);
                    Activate(_matchCamera);
                    break;
                case GameFlowState.Spectating:
                    FollowAimTarget(_spectatorTarget != null ? _spectatorTarget : _defaultTarget);
                    _aim.ApplyPreset(_config.Spectator);
                    Activate(_spectatorCamera);
                    break;
                case GameFlowState.Results:
                    _aim.ApplyPreset(_config.Results);
                    SetResultsTarget(_spectatorTarget != null ? _spectatorTarget : _defaultTarget);
                    Activate(_resultsCamera);
                    break;
                case GameFlowState.Boot:
                case GameFlowState.Lobby:
                default:
                    _spectatorTarget = null;
                    FollowAimTarget(_defaultTarget);
                    _aim.ApplyPreset(_config.Lobby);
                    Activate(_lobbyCamera);
                    break;
            }
        }

        private void FollowAimTarget(Transform target)
        {
            _aim.Follow(target);
            if (_aim.Pivot == null)
            {
                return;
            }

            _lobbyCamera.Follow = _aim.Pivot;
            _matchCamera.Follow = _aim.Pivot;
            _spectatorCamera.Follow = _aim.Pivot;
        }

        private void SetResultsTarget(Transform target)
        {
            _resultsCamera.Follow = target;
            _resultsCamera.LookAt = target;
        }

        private void Activate(CinemachineCamera camera)
        {
            if (camera == null || _activeCamera == camera)
            {
                return;
            }

            var cameras = new[]
            {
                _lobbyCamera,
                _matchCamera,
                _spectatorCamera,
                _resultsCamera
            };
            foreach (var candidate in cameras)
            {
                if (candidate != null)
                {
                    candidate.gameObject.SetActive(candidate == camera);
                }
            }

            _activeCamera = camera;
            _activeCamera.CancelDamping();
        }
    }
}
