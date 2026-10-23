using System;
using UnityEngine;

namespace PawArena.Gameplay.Player
{
    /// <summary>
    /// Applies local movement intent to a dynamic Rigidbody.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private PlayerMovementConfig _config = null;
        [SerializeField] private PlayerInputReader _input = null;
        [SerializeField] private Transform _movementReference = null;

        [Header("Collision")]
        [SerializeField] private LayerMask _groundMask = ~0;

        private Rigidbody _rigidbody;
        private CapsuleCollider _capsuleCollider;
        private float _jumpBufferTimer;
        private float _coyoteTimer;

        /// <summary>
        /// Raised after a buffered jump is applied.
        /// </summary>
        public event Action Jumped;

        /// <summary>
        /// Gets whether the ground probe touched a valid ground layer this physics step.
        /// </summary>
        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _capsuleCollider = GetComponent<CapsuleCollider>();

            if (_config == null)
            {
                Debug.LogError($"{nameof(_config)} is not assigned.", this);
                enabled = false;
                return;
            }

            if (_input == null)
            {
                Debug.LogError($"{nameof(_input)} is not assigned.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.JumpPressed += BufferJump;
            }
        }

        private void FixedUpdate()
        {
            var fixedDeltaTime = Time.fixedDeltaTime;
            UpdateGroundedState(fixedDeltaTime);
            UpdateJumpBuffer(fixedDeltaTime);
            ApplyPlanarMovement(fixedDeltaTime);
            TryApplyJump();
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.JumpPressed -= BufferJump;
            }
        }

        private void BufferJump()
        {
            _jumpBufferTimer = _config.JumpBufferTime;
        }

        private void UpdateGroundedState(float fixedDeltaTime)
        {
            var bounds = _capsuleCollider.bounds;
            var probePosition = new Vector3(
                bounds.center.x,
                bounds.min.y + _config.GroundCheckRadius,
                bounds.center.z);

            IsGrounded = Physics.CheckSphere(
                probePosition,
                _config.GroundCheckRadius,
                _groundMask,
                QueryTriggerInteraction.Ignore);

            _coyoteTimer = IsGrounded
                ? _config.CoyoteTime
                : Mathf.Max(0f, _coyoteTimer - fixedDeltaTime);
        }

        private void UpdateJumpBuffer(float fixedDeltaTime)
        {
            _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - fixedDeltaTime);
        }

        private void ApplyPlanarMovement(float fixedDeltaTime)
        {
            var moveInput = Vector2.ClampMagnitude(_input.MoveInput, 1f);
            var forward = _movementReference == null ? Vector3.forward : _movementReference.forward;
            var right = _movementReference == null ? Vector3.right : _movementReference.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            var moveDirection = forward * moveInput.y + right * moveInput.x;
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            var currentVelocity = _rigidbody.linearVelocity;
            var currentPlanarVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            var targetPlanarVelocity = moveDirection * _config.MoveSpeed;
            var nextPlanarVelocity = Vector3.MoveTowards(
                currentPlanarVelocity,
                targetPlanarVelocity,
                _config.Acceleration * fixedDeltaTime);

            _rigidbody.linearVelocity = new Vector3(
                nextPlanarVelocity.x,
                currentVelocity.y,
                nextPlanarVelocity.z);

            if (moveDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            var nextRotation = Quaternion.RotateTowards(
                _rigidbody.rotation,
                targetRotation,
                _config.TurnSpeed * fixedDeltaTime);
            _rigidbody.MoveRotation(nextRotation);
        }

        private void TryApplyJump()
        {
            if (_jumpBufferTimer <= 0f || _coyoteTimer <= 0f)
            {
                return;
            }

            var velocity = _rigidbody.linearVelocity;
            var jumpSpeed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * _config.JumpHeight);
            _rigidbody.linearVelocity = new Vector3(velocity.x, jumpSpeed, velocity.z);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            IsGrounded = false;
            Jumped?.Invoke();
        }
    }
}
