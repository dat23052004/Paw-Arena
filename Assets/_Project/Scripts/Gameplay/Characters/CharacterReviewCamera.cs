using UnityEngine;
using UnityEngine.InputSystem;

namespace PawArena.Gameplay.Characters
{
    /// <summary>Rear follow camera: Lobby looks while RMB is held; Party Match looks continuously.</summary>
    public sealed class CharacterReviewCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _distance = 5;
        [SerializeField, Min(.5f)] private float _minimumDistance = 2;
        [SerializeField, Min(.5f)] private float _maximumDistance = 18;
        [SerializeField, Min(0), Tooltip("Zoom distance per scroll unit. Default: 0.004. Higher values zoom faster; 0 disables scroll zoom.")]
        private float _zoomSensitivity = .004f;
        [SerializeField, Range(-80, 80)] private float _minimumPitch = -35;
        [SerializeField, Range(-80, 80)] private float _maximumPitch = 80;
        private readonly RaycastHit[] _obstructions = new RaycastHit[16];
        private float _yaw, _pitch = 16, _planarTurnSpeed;
        private bool _focused = true, _ownsCursor, _partyMatchMode;
        private CursorLockMode _previousLock;
        private bool _previousVisibility;
        /// <summary>Requested orbit pitch, independent of zoom or collision shortening.</summary>
        public float Pitch => _pitch;
        public float Distance => _distance;
        public bool IsPartyMatchMode => _partyMatchMode;
        private void Awake() => ValidateLimits();
        private void OnValidate() => ValidateLimits();
        private void ValidateLimits()
        {
            _minimumDistance = float.IsFinite(_minimumDistance) ? Mathf.Max(.5f, _minimumDistance) : 2;
            _maximumDistance = float.IsFinite(_maximumDistance) ? Mathf.Max(_minimumDistance, _maximumDistance) : Mathf.Max(_minimumDistance, 18);
            _zoomSensitivity = float.IsFinite(_zoomSensitivity) ? Mathf.Max(0, _zoomSensitivity) : .004f;
            _minimumPitch = float.IsFinite(_minimumPitch) ? Mathf.Clamp(_minimumPitch, -80, 80) : -35;
            _maximumPitch = float.IsFinite(_maximumPitch) ? Mathf.Clamp(_maximumPitch, _minimumPitch, 80) : 80;
            _distance = Mathf.Clamp(float.IsFinite(_distance) ? _distance : 5, _minimumDistance, _maximumDistance);
            _pitch = Mathf.Clamp(_pitch, _minimumPitch, _maximumPitch);
        }
        /// <summary>Requested horizontal heading for the player motor, without feedback from rendered camera yaw.</summary>
        public Vector3 PlanarForward => Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
        /// <summary>Latest requested horizontal turn rate in degrees per second.</summary>
        public float PlanarTurnSpeed => _planarTurnSpeed;
        public void AlignBehindTarget() { if (_target != null) { _yaw = _target.eulerAngles.y; } }
        public void Follow(Transform target) { _target = target; if (target != null) { _yaw = target.eulerAngles.y; } }
        /// <summary>Enables continuous mouse-look for Party Match while preserving RMB orbit in Lobby.</summary>
        public void SetPartyMatchMode(bool enabled)
        {
            if (_partyMatchMode == enabled) { return; }
            _partyMatchMode = enabled;
            ReleaseCursor();
            if (_target != null) { _yaw = _target.eulerAngles.y; }
        }
        private void OnDisable() => ReleaseCursor();
        private void OnApplicationFocus(bool focused) { _focused = focused; if (!focused) { ReleaseCursor(); } }
        private void Update()
        {
            _planarTurnSpeed = 0;
            if (_target == null || !_focused) { ReleaseCursor(); return; }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (_partyMatchMode || mouse.rightButton.isPressed)
                {
                    if (!_ownsCursor)
                    { _previousLock = Cursor.lockState; _previousVisibility = Cursor.visible; _ownsCursor = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                    var delta = mouse.delta.ReadValue();
                    var yawDelta = delta.x * .18f;
                    _yaw = Mathf.Repeat(_yaw + yawDelta, 360);
                    if (Time.unscaledDeltaTime > 0) { _planarTurnSpeed = yawDelta / Time.unscaledDeltaTime; }
                    _pitch = Mathf.Clamp(_pitch - delta.y * .18f, _minimumPitch, _maximumPitch);
                }
                else { ReleaseCursor(); }
                _distance = Mathf.Clamp(_distance - mouse.scroll.ReadValue().y * _zoomSensitivity, _minimumDistance, _maximumDistance);
            }
            else { ReleaseCursor(); }
        }
        private void LateUpdate()
        {
            if (_target == null) { return; }
            var centre = _target.position + Vector3.up * 1.05f;
            // Render behind the interpolated body yaw so the camera turns with the cat without snapping ahead of it.
            var direction = Quaternion.Euler(_pitch, _target.eulerAngles.y, 0) * Vector3.back;
            var distance = _distance;
            var count = Physics.SphereCastNonAlloc(centre, .12f, direction, _obstructions, distance, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                if (_obstructions[i].transform == _target || _obstructions[i].transform.IsChildOf(_target)) { continue; }
                distance = Mathf.Min(distance, Mathf.Max(.2f, _obstructions[i].distance - .04f));
            }
            transform.position = centre + direction * distance;
            transform.LookAt(centre);
        }
        private void ReleaseCursor()
        {
            if (!_ownsCursor) { return; }
            Cursor.lockState = _previousLock; Cursor.visible = _previousVisibility; _ownsCursor = false;
        }
    }
}
