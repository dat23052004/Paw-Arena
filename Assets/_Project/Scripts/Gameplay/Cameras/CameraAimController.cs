using UnityEngine;
using UnityEngine.InputSystem;

namespace PawArena.Gameplay.Cameras
{
    /// <summary>Owns local camera input and a world-space pivot consumed by Cinemachine rigs.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-150)]
    public sealed class CameraAimController : MonoBehaviour, ICameraHeadingProvider
    {
        [SerializeField] private Transform _pivot;

        private Transform _target;
        private float _followHeight = 1.05f;
        private float _lookSensitivity = 1;
        private float _zoomSensitivity = 1;
        private CameraInputMode _inputMode = CameraInputMode.Disabled;
        private float _pitch = 16;
        private float _distance = 4.5f;
        private float _minimumPitch = -20;
        private float _maximumPitch = 80;
        private float _minimumDistance = 1.5f;
        private float _maximumDistance = 20;
        private float _yaw;
        private float _planarTurnSpeed;
        private bool _focused = true;
        private bool _ownsCursor;
        private CursorLockMode _previousLock;
        private bool _previousVisibility;

        public Transform Pivot => _pivot;
        public Transform Target => _target;
        public CameraInputMode InputMode => _inputMode;
        public float Pitch => _pitch;
        public float Distance => _distance;
        public Vector3 PlanarForward => Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
        public float PlanarTurnSpeed => _planarTurnSpeed;

        private void Awake()
        {
            if (_pivot == null)
            {
                Debug.LogError($"{nameof(CameraAimController)} requires a pivot transform.", this);
                enabled = false;
                return;
            }

            ValidateLimits();
            PresentPivot();
        }

        private void OnDisable() => ReleaseCursor();

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (!focused)
            {
                ReleaseCursor();
            }
        }

        private void Update()
        {
            _planarTurnSpeed = 0;
            if (_target == null || !_focused || _inputMode == CameraInputMode.Disabled)
            {
                ReleaseCursor();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                ReleaseCursor();
                return;
            }

            var shouldLook = _inputMode switch
            {
                CameraInputMode.Match => true,
                CameraInputMode.Spectator => mouse.rightButton.isPressed,
                CameraInputMode.Lobby => mouse.rightButton.isPressed,
                _ => false
            };
            if (shouldLook)
            {
                AcquireCursor();
                var delta = mouse.delta.ReadValue();
                var requestedYawDelta = delta.x * _lookSensitivity;
                var previousYaw = _yaw;
                _yaw = Mathf.Repeat(_yaw + requestedYawDelta, 360);
                var appliedYawDelta = Mathf.DeltaAngle(previousYaw, _yaw);
                if (Time.unscaledDeltaTime > 0)
                {
                    _planarTurnSpeed = appliedYawDelta / Time.unscaledDeltaTime;
                }

                _pitch = Mathf.Clamp(
                    _pitch - delta.y * _lookSensitivity,
                    _minimumPitch,
                    _maximumPitch);
            }
            else
            {
                ReleaseCursor();
            }

            if (_inputMode is CameraInputMode.Lobby or CameraInputMode.Match or CameraInputMode.Spectator)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(scroll, 0))
                {
                    _distance = Mathf.Clamp(
                        _distance - Mathf.Sign(scroll) * _zoomSensitivity,
                        _minimumDistance,
                        _maximumDistance);
                }
            }
        }

        private void LateUpdate() => PresentPivot();

        /// <summary>Changes the tracked actor and aligns requested heading behind it.</summary>
        public void Follow(Transform target)
        {
            _target = target;
            AlignBehindTarget();
            PresentPivot();
        }

        /// <summary>Copies shared tuning from the immutable camera configuration asset.</summary>
        public void ApplySystemConfig(CameraSystemConfig config)
        {
            if (config == null)
            {
                Debug.LogError($"{nameof(CameraAimController)} requires a camera system config.", this);
                return;
            }

            _followHeight = config.FollowHeight;
            _lookSensitivity = config.LookSensitivity;
            _zoomSensitivity = config.ZoomSensitivity;
            PresentPivot();
        }

        /// <summary>Copies one view preset into runtime camera state.</summary>
        public void ApplyPreset(CameraAimPreset preset)
        {
            preset.Validate();
            _inputMode = preset.InputMode;
            _minimumPitch = preset.MinimumPitch;
            _maximumPitch = preset.MaximumPitch;
            _minimumDistance = preset.MinimumDistance;
            _maximumDistance = preset.MaximumDistance;
            _pitch = preset.DefaultPitch;
            _distance = preset.DefaultDistance;
            ReleaseCursor();

            if (_inputMode == CameraInputMode.Match)
            {
                AcquireCursor();
            }
            else
            {
                EnsureCursorVisible();
            }

            PresentPivot();
        }

        public void AlignBehindTarget()
        {
            if (_target != null)
            {
                _yaw = _target.eulerAngles.y;
            }
        }

        private void PresentPivot()
        {
            if (_pivot == null || _target == null)
            {
                return;
            }

            _pivot.SetPositionAndRotation(
                _target.position + Vector3.up * _followHeight,
                Quaternion.Euler(_pitch, _yaw, 0));
        }

        private void ValidateLimits()
        {
            _minimumPitch = Mathf.Clamp(_minimumPitch, -80, 80);
            _maximumPitch = Mathf.Clamp(_maximumPitch, _minimumPitch, 80);
            _minimumDistance = Mathf.Max(.5f, _minimumDistance);
            _maximumDistance = Mathf.Max(_minimumDistance, _maximumDistance);
            _pitch = Mathf.Clamp(float.IsFinite(_pitch) ? _pitch : 16, _minimumPitch, _maximumPitch);
            _distance = Mathf.Clamp(float.IsFinite(_distance) ? _distance : 4.5f, _minimumDistance, _maximumDistance);
        }

        private void AcquireCursor()
        {
            if (_ownsCursor || !_focused || _target == null)
            {
                return;
            }

            _previousLock = Cursor.lockState;
            _previousVisibility = Cursor.visible;
            _ownsCursor = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ReleaseCursor()
        {
            if (!_ownsCursor)
            {
                return;
            }

            Cursor.lockState = _previousLock;
            Cursor.visible = _previousVisibility;
            _ownsCursor = false;
        }

        private static void EnsureCursorVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
