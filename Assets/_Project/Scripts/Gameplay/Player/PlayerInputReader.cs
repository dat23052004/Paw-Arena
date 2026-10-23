using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawArena.Gameplay.Player
{
    /// <summary>
    /// Reads the configured Input System asset and exposes gameplay intent without polling devices directly.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const string PlayerMapName = "Player";
        private const string MoveActionName = "Move";
        private const string PunchActionName = "Punch";
        private const string GrabActionName = "Grab";
        private const string LiftActionName = "Lift";
        private const string JumpActionName = "Jump";
        private const string AbilityActionName = "Ability";

        [SerializeField] private InputActionAsset _inputActions = null;

        private InputActionMap _playerActions;
        private InputAction _moveAction;
        private InputAction _punchAction;
        private InputAction _grabAction;
        private InputAction _liftAction;
        private InputAction _jumpAction;
        private InputAction _abilityAction;

        /// <summary>
        /// Raised when the player requests a punch.
        /// </summary>
        public event Action PunchPressed;

        /// <summary>
        /// Raised when the grab button starts being held.
        /// </summary>
        public event Action GrabStarted;

        /// <summary>
        /// Raised when the grab button is released.
        /// </summary>
        public event Action GrabReleased;

        /// <summary>
        /// Raised when the player requests a lift.
        /// </summary>
        public event Action LiftPressed;

        /// <summary>
        /// Raised when the player requests a jump.
        /// </summary>
        public event Action JumpPressed;

        /// <summary>
        /// Raised when the player requests the equipped ability.
        /// </summary>
        public event Action AbilityPressed;

        /// <summary>
        /// Gets the latest normalized movement input.
        /// </summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>
        /// Gets whether grab is currently held.
        /// </summary>
        public bool IsGrabHeld { get; private set; }

        private void Awake()
        {
            if (_inputActions == null)
            {
                Debug.LogError($"{nameof(_inputActions)} is not assigned.", this);
                enabled = false;
                return;
            }

            _playerActions = _inputActions.FindActionMap(PlayerMapName, false);
            if (_playerActions == null)
            {
                Debug.LogError($"Input map '{PlayerMapName}' was not found.", this);
                enabled = false;
                return;
            }

            _moveAction = FindRequiredAction(MoveActionName);
            _punchAction = FindRequiredAction(PunchActionName);
            _grabAction = FindRequiredAction(GrabActionName);
            _liftAction = FindRequiredAction(LiftActionName);
            _jumpAction = FindRequiredAction(JumpActionName);
            _abilityAction = FindRequiredAction(AbilityActionName);

            if (_moveAction == null || _punchAction == null || _grabAction == null ||
                _liftAction == null || _jumpAction == null || _abilityAction == null)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_playerActions == null)
            {
                return;
            }

            _moveAction.performed += HandleMove;
            _moveAction.canceled += HandleMove;
            _punchAction.performed += HandlePunch;
            _grabAction.started += HandleGrabStarted;
            _grabAction.canceled += HandleGrabReleased;
            _liftAction.performed += HandleLift;
            _jumpAction.performed += HandleJump;
            _abilityAction.performed += HandleAbility;
            _playerActions.Enable();
        }

        private void OnDisable()
        {
            if (_playerActions == null)
            {
                return;
            }

            _playerActions.Disable();
            _moveAction.performed -= HandleMove;
            _moveAction.canceled -= HandleMove;
            _punchAction.performed -= HandlePunch;
            _grabAction.started -= HandleGrabStarted;
            _grabAction.canceled -= HandleGrabReleased;
            _liftAction.performed -= HandleLift;
            _jumpAction.performed -= HandleJump;
            _abilityAction.performed -= HandleAbility;
            MoveInput = Vector2.zero;
            IsGrabHeld = false;
        }

        private InputAction FindRequiredAction(string actionName)
        {
            var action = _playerActions.FindAction(actionName, false);
            if (action == null)
            {
                Debug.LogError($"Input action '{actionName}' was not found in '{PlayerMapName}'.", this);
            }

            return action;
        }

        private void HandleMove(InputAction.CallbackContext context) => MoveInput = context.ReadValue<Vector2>();
        private void HandlePunch(InputAction.CallbackContext context) => PunchPressed?.Invoke();
        private void HandleLift(InputAction.CallbackContext context) => LiftPressed?.Invoke();
        private void HandleJump(InputAction.CallbackContext context) => JumpPressed?.Invoke();
        private void HandleAbility(InputAction.CallbackContext context) => AbilityPressed?.Invoke();

        private void HandleGrabStarted(InputAction.CallbackContext context)
        {
            IsGrabHeld = true;
            GrabStarted?.Invoke();
        }

        private void HandleGrabReleased(InputAction.CallbackContext context)
        {
            IsGrabHeld = false;
            GrabReleased?.Invoke();
        }
    }
}
