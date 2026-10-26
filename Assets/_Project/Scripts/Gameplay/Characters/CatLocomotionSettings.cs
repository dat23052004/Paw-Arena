using UnityEngine;

namespace PawArena.Gameplay.Characters
{
    /// <summary>Shared movement tuning for player and bot; no input, combat, or match dependencies.</summary>
    [CreateAssetMenu(menuName = "Paw Arena/Characters/Locomotion Settings")]
    public sealed class CatLocomotionSettings : ScriptableObject
    {
        [Header("Run")]
        [SerializeField, Min(.1f), Tooltip("Maximum planar movement speed. Every non-zero movement input uses this speed.")]
        private float _runSpeed = 6;
        [SerializeField, Min(.1f), Tooltip("Planar speed gained per second while movement input is held.")]
        private float _acceleration = 24;
        [SerializeField, Min(.1f), Tooltip("Planar speed removed per second after movement input is released.")]
        private float _braking = 32;

        [Header("Turn")]
        [SerializeField, Min(1), Tooltip("Maximum normal body turn speed in degrees per second.")]
        private float _turnSpeed = 240;
        [SerializeField, Min(1), Tooltip("Maximum player turn speed while catching up after a fast target change, in degrees per second.")]
        private float _catchUpTurnSpeed = 720;
        [SerializeField, Min(1), Tooltip("Angular speed gained per second. Lower values make the body feel heavier when starting a turn.")]
        private float _turnAcceleration = 900;
        [SerializeField, Min(1), Tooltip("Angular speed removed per second when slowing down or reversing. Lower values preserve more rotational inertia.")]
        private float _turnBraking = 1200;
        [SerializeField, Min(.01f), Tooltip("Time used to convert the remaining angle into correction speed. Higher values allow more visible lag behind the target.")]
        private float _turnSmoothTime = .18f;

        [Header("Jump")]
        [SerializeField, Min(.1f), Tooltip("Target jump apex height in world units.")]
        private float _jumpHeight = 1.2f;
        [SerializeField, Range(0, 1), Tooltip("Multiplier applied to planar acceleration while airborne.")]
        private float _airControl = .25f;
        [SerializeField, Min(0), Tooltip("Seconds a jump press remains queued before landing.")]
        private float _jumpBuffer = .12f;
        [SerializeField, Min(0), Tooltip("Seconds after leaving the ground during which jumping is still allowed.")]
        private float _coyoteTime = .1f;
        public float RunSpeed => _runSpeed;
        public float Acceleration => _acceleration;
        public float Braking => _braking;
        public float TurnSpeed => _turnSpeed;
        public float CatchUpTurnSpeed => _catchUpTurnSpeed;
        public float TurnAcceleration => _turnAcceleration;
        public float TurnBraking => _turnBraking;
        public float TurnSmoothTime => _turnSmoothTime;
        public float JumpHeight => _jumpHeight;
        public float AirControl => _airControl;
        public float JumpBuffer => _jumpBuffer;
        public float CoyoteTime => _coyoteTime;
        private void OnValidate()
        {
            _runSpeed = float.IsFinite(_runSpeed) ? Mathf.Max(.1f, _runSpeed) : 6;
            _acceleration = float.IsFinite(_acceleration) ? Mathf.Max(.1f, _acceleration) : 24;
            _braking = float.IsFinite(_braking) ? Mathf.Max(.1f, _braking) : 32;
            _turnSpeed = float.IsFinite(_turnSpeed) ? Mathf.Max(1, _turnSpeed) : 240;
            _catchUpTurnSpeed = float.IsFinite(_catchUpTurnSpeed) ? Mathf.Max(_turnSpeed, _catchUpTurnSpeed) : 720;
            _turnAcceleration = float.IsFinite(_turnAcceleration) ? Mathf.Max(1, _turnAcceleration) : 900;
            _turnBraking = float.IsFinite(_turnBraking) ? Mathf.Max(1, _turnBraking) : 1200;
            _turnSmoothTime = float.IsFinite(_turnSmoothTime) ? Mathf.Max(.01f, _turnSmoothTime) : .18f;
            _jumpHeight = float.IsFinite(_jumpHeight) ? Mathf.Max(.1f, _jumpHeight) : 1.2f;
            _airControl = float.IsFinite(_airControl) ? Mathf.Clamp01(_airControl) : .25f;
            _jumpBuffer = float.IsFinite(_jumpBuffer) ? Mathf.Max(0, _jumpBuffer) : .12f;
            _coyoteTime = float.IsFinite(_coyoteTime) ? Mathf.Max(0, _coyoteTime) : .1f;
        }
    }
}
