using UnityEngine;

namespace PawArena.Gameplay.Player
{
    /// <summary>
    /// Stores designer-tunable local movement values for the P0 prototype.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlayerMovementConfig",
        menuName = "Paw Arena/Player/Movement Config")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float _moveSpeed = 6f;
        [SerializeField, Min(0.01f)] private float _acceleration = 30f;
        [SerializeField, Min(0.01f)] private float _turnSpeed = 720f;

        [Header("Jump")]
        [SerializeField, Min(0.01f)] private float _jumpHeight = 1.5f;
        [SerializeField, Min(0f)] private float _coyoteTime = 0.1f;
        [SerializeField, Min(0f)] private float _jumpBufferTime = 0.1f;
        [SerializeField, Min(0.01f)] private float _groundCheckRadius = 0.15f;

        /// <summary>
        /// Gets the maximum planar movement speed in metres per second.
        /// </summary>
        public float MoveSpeed => _moveSpeed;

        /// <summary>
        /// Gets the planar acceleration in metres per second squared.
        /// </summary>
        public float Acceleration => _acceleration;

        /// <summary>
        /// Gets the facing rotation speed in degrees per second.
        /// </summary>
        public float TurnSpeed => _turnSpeed;

        /// <summary>
        /// Gets the target jump height in metres.
        /// </summary>
        public float JumpHeight => _jumpHeight;

        /// <summary>
        /// Gets the grace period after leaving ground during which jumping is still allowed.
        /// </summary>
        public float CoyoteTime => _coyoteTime;

        /// <summary>
        /// Gets how long an early jump request remains buffered.
        /// </summary>
        public float JumpBufferTime => _jumpBufferTime;

        /// <summary>
        /// Gets the radius used for the ground probe.
        /// </summary>
        public float GroundCheckRadius => _groundCheckRadius;
    }
}
