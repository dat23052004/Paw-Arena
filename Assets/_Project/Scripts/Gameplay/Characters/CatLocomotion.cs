using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawArena.Gameplay.Characters
{
    /// <summary>Shared body movement. Owners submit intent; this component never reads devices or finds opponents.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class CatLocomotion : MonoBehaviour
    {
        private const float FacingCompletionAngle = .1f;
        private const int EverythingMask = ~0;
        [SerializeField] private CatLocomotionSettings _settings;
        [SerializeField] private LayerMask _groundMask = ~0;
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private PhysicsScene _physics;
        private Vector3 _move, _facing;
        private bool _initialized;
        private float _jumpBuffer, _coyote, _takeoffLock;
        private float _yawVelocity, _facingTurnSpeed;
        private float _speedMultiplier = 1;
        private bool _catchingUpFacing, _hasFacingTurnSample;

        public CatLocomotionSettings Settings => _settings;
        public Rigidbody Body => _body;
        public bool Grounded { get; private set; }
        public bool Suspended { get; private set; }
        public bool MovementControlEnabled { get; private set; } = true;
        public float PlanarSpeed => _body == null ? 0 : new Vector2(_body.linearVelocity.x, _body.linearVelocity.z).magnitude;
        public float SpeedMultiplier => _speedMultiplier;
        public event Action Jumped;
        public event Action Warped;

        private void Awake() => Initialize();
        private void FixedUpdate() => Step(Time.fixedDeltaTime);
        private void OnDisable() => ClearInput();

        /// <summary>Idempotent setup also available to isolated simulation tests.</summary>
        public void Initialize()
        {
            if (_initialized) { return; }
            if (_settings == null) { Debug.LogError("Cat locomotion settings are missing.", this); enabled = false; return; }
            _body = GetComponent<Rigidbody>(); _capsule = GetComponent<CapsuleCollider>();
            _physics = gameObject.scene.GetPhysicsScene();
            _groundMask = ResolveEnvironmentMask(_groundMask);
            _body.constraints = RigidbodyConstraints.FreezeRotation;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            // Characters are not fast enough to justify the most expensive sweep mode every physics step.
            // Speculative CCD still protects short bursts and remains valid while the body is kinematic.
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _initialized = true;
        }

        private static LayerMask ResolveEnvironmentMask(LayerMask configured)
        {
            if (configured.value != 0 && configured.value != EverythingMask) { return configured; }
            var mask = LayerMask.GetMask("Ground", "Default");
            return mask != 0 ? mask : 1 << LayerMask.NameToLayer("Default");
        }

        /// <summary>World-space run intent; jump edges survive until a physics step.</summary>
        public void Submit(Vector3 direction, bool jump)
        {
            if (Suspended || !enabled) { return; }
            direction.y = 0; _move = Vector3.ClampMagnitude(direction, 1);
            if (jump && _settings != null) { _jumpBuffer = _settings.JumpBuffer; }
        }
        /// <summary>Optional independent facing intent. Bots without it continue facing their movement.</summary>
        public void SubmitFacing(Vector3 direction)
        {
            SubmitFacing(direction, 0, false);
        }
        /// <summary>Facing intent with the target's latest signed angular speed, used for responsive mouse tracking.</summary>
        public void SubmitFacing(Vector3 direction, float turnSpeed)
        {
            SubmitFacing(direction, turnSpeed, true);
        }
        private void SubmitFacing(Vector3 direction, float turnSpeed, bool hasTurnSample)
        {
            if (Suspended || !enabled) { return; }
            direction.y = 0; _facing = direction.sqrMagnitude > .001f ? direction.normalized : Vector3.zero;
            if (_facing == Vector3.zero) { _facingTurnSpeed = 0; _hasFacingTurnSample = false; return; }
            _hasFacingTurnSample |= hasTurnSample;
            turnSpeed = float.IsFinite(turnSpeed) ? turnSpeed : 0;
            if (Mathf.Abs(turnSpeed) >= Mathf.Abs(_facingTurnSpeed)) { _facingTurnSpeed = turnSpeed; }
            if (_settings != null && Mathf.Abs(turnSpeed) > _settings.TurnSpeed) { _catchingUpFacing = true; }
        }
        public void ClearInput()
        {
            _move = _facing = Vector3.zero; _jumpBuffer = 0; _yawVelocity = _facingTurnSpeed = 0;
            _catchingUpFacing = _hasFacingTurnSample = false;
        }

        /// <summary>Stops translation without discarding the camera-facing target or angular momentum.</summary>
        public void ClearMovementInput()
        {
            _move = Vector3.zero;
            _jumpBuffer = 0;
        }

        /// <summary>Applies a temporary combat movement modifier without mutating the shared settings asset.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = float.IsFinite(multiplier) ? Mathf.Clamp(multiplier, .1f, 2) : 1;
        }

        /// <summary>Temporarily lets combat own planar velocity while physics and gravity remain active.</summary>
        public void SetMovementControl(bool enabled)
        {
            MovementControlEnabled = enabled;
            if (!enabled) { ClearInput(); }
        }

        /// <summary>Stops physical travel for in-place clip review; never toggles global time.</summary>
        public void SetSuspended(bool suspended)
        {
            Initialize(); if (!_initialized || Suspended == suspended) { return; }
            ClearInput();
            if (!_body.isKinematic) { _body.linearVelocity = _body.angularVelocity = Vector3.zero; }
            _body.isKinematic = suspended; Suspended = suspended;
            _jumpBuffer = _coyote = _takeoffLock = 0;
        }

        /// <summary>Move this existing actor to a spawn point; future map cycles can reuse the same instance.</summary>
        public void Warp(Vector3 position, Quaternion rotation)
        {
            Initialize(); if (!_initialized) { return; }
            ClearInput(); _jumpBuffer = _coyote = _takeoffLock = 0; Grounded = false;
            _body.position = position; _body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            if (!_body.isKinematic) { _body.linearVelocity = _body.angularVelocity = Vector3.zero; }
            Warped?.Invoke();
        }

        /// <summary>Explicit physics step, independent of input and scene-wide managers.</summary>
        public void Step(float delta)
        {
            Initialize(); if (!_initialized || Suspended || delta <= 0 || !float.IsFinite(delta)) { return; }
            _takeoffLock = Mathf.Max(0, _takeoffLock - delta);
            Grounded = false;
            var foot = _capsule.bounds.min.y;
            var origin = new Vector3(_body.position.x, foot + .18f, _body.position.z);
            var count = _physics.Raycast(origin, Vector3.down, _hits, .28f, _groundMask, QueryTriggerInteraction.Ignore);
            if (_takeoffLock <= 0 && _body.linearVelocity.y <= .2f)
            {
                for (var i = 0; i < count; i++)
                { if (_hits[i].rigidbody != _body && _hits[i].normal.y > .65f) { Grounded = true; break; } }
            }
            _coyote = Grounded ? _settings.CoyoteTime : Mathf.Max(0, _coyote - delta);
            if (!MovementControlEnabled)
            {
                _jumpBuffer = Mathf.Max(0, _jumpBuffer - delta);
                return;
            }
            var velocity = _body.linearVelocity;
            var planar = new Vector3(velocity.x, 0, velocity.z);
            var acceleration = _move.sqrMagnitude > .001f ? _settings.Acceleration : _settings.Braking;
            if (!Grounded) { acceleration *= _settings.AirControl; }
            planar = Vector3.MoveTowards(planar, _move * (_settings.RunSpeed * _speedMultiplier), acceleration * delta);
            _body.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
            var hasIndependentFacing = _facing.sqrMagnitude > .001f;
            var facing = hasIndependentFacing ? _facing : _move;
            if (facing.sqrMagnitude > .001f)
            {
                var currentYaw = _body.rotation.eulerAngles.y;
                var targetYaw = Quaternion.LookRotation(facing).eulerAngles.y;
                if (!hasIndependentFacing) { _catchingUpFacing = false; }
                var yaw = CalculateYaw(currentYaw, targetYaw, delta, hasIndependentFacing);
                _body.MoveRotation(Quaternion.Euler(0, yaw, 0));
                _facingTurnSpeed = 0; _hasFacingTurnSample = false;
            }
            else { _yawVelocity = _facingTurnSpeed = 0; _catchingUpFacing = _hasFacingTurnSample = false; }
            if (_jumpBuffer > 0 && _coyote > 0)
            {
                _body.linearVelocity = new Vector3(planar.x, Mathf.Sqrt(2 * Mathf.Abs(Physics.gravity.y) * _settings.JumpHeight), planar.z);
                _jumpBuffer = _coyote = 0; _takeoffLock = .15f; Grounded = false; Jumped?.Invoke();
            }
            _jumpBuffer = Mathf.Max(0, _jumpBuffer - delta);
        }

        private float CalculateYaw(float currentYaw, float targetYaw, float delta, bool hasIndependentFacing)
        {
            var error = Mathf.DeltaAngle(currentYaw, targetYaw);
            var inputSpeed = hasIndependentFacing && _hasFacingTurnSample ? Mathf.Abs(_facingTurnSpeed) : 0;
            if (_catchingUpFacing && inputSpeed <= _settings.TurnSpeed && Mathf.Abs(error) <= FacingCompletionAngle)
            { _catchingUpFacing = false; }
            if (Mathf.Abs(error) <= FacingCompletionAngle)
            {
                _yawVelocity = Mathf.MoveTowards(_yawVelocity, 0, _settings.TurnBraking * delta);
                return targetYaw;
            }

            var maximumSpeed = _catchingUpFacing ? _settings.CatchUpTurnSpeed : _settings.TurnSpeed;
            var desiredSpeed = Mathf.Min(maximumSpeed, inputSpeed + Mathf.Abs(error) / _settings.TurnSmoothTime);
            if (!_hasFacingTurnSample)
            {
                var stoppingSpeed = Mathf.Sqrt(2 * _settings.TurnBraking * Mathf.Abs(error));
                desiredSpeed = Mathf.Min(desiredSpeed, stoppingSpeed);
            }
            var desiredVelocity = Mathf.Sign(error) * desiredSpeed;
            var sameDirection = Mathf.Approximately(_yawVelocity, 0) || Mathf.Sign(_yawVelocity) == Mathf.Sign(desiredVelocity);
            var speedingUp = sameDirection && Mathf.Abs(desiredVelocity) > Mathf.Abs(_yawVelocity);
            var response = speedingUp ? _settings.TurnAcceleration : _settings.TurnBraking;
            _yawVelocity = Mathf.MoveTowards(_yawVelocity, desiredVelocity, response * delta);

            var step = _yawVelocity * delta;
            if (Mathf.Sign(step) == Mathf.Sign(error) && Mathf.Abs(step) >= Mathf.Abs(error))
            {
                _yawVelocity = _hasFacingTurnSample ? Mathf.Clamp(_facingTurnSpeed, -maximumSpeed, maximumSpeed) : 0;
                return targetYaw;
            }
            return Mathf.Repeat(currentYaw + step, 360);
        }
    }
}
