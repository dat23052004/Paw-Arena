using System;
using PawArena.Gameplay.Cameras;
using PawArena.Gameplay.Characters;
using PawArena.Gameplay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawArena.Gameplay.Player
{
    /// <summary>Human-only input and clip audition. Bot prefabs never carry this component.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class CatPlayerController : MonoBehaviour
    {
        [SerializeField] private CatLocomotion _motor;
        [SerializeField] private CatAnimationController _animation;
        [SerializeField] private CatPlayerCombat _combat;
        [SerializeField, Tooltip("Camera pitch at which the cat stands upright.")] private float _neutralCameraPitch = 16;
        [SerializeField] private CatPlayerControlMode _controlMode = CatPlayerControlMode.Lobby;
        private Camera _camera;
        private ICameraHeadingProvider _cameraHeading;
        private CatSecondaryMotion _motion;
        private Vector3 _home;
        private Quaternion _homeRotation;
        private bool _focused = true;
        private bool _inputEnabled = true;
        public string CurrentClip => _animation.CurrentClip;
        public bool IsPreviewing => _animation.IsPreviewing;
        public bool IsPaused => _animation.IsPaused;
        public float PlaybackSpeed { get => _animation.PlaybackSpeed; set => _animation.PlaybackSpeed = Mathf.Clamp(value, .1f, 2); }
        public float NormalizedTime => _animation.NormalizedTime;
        public CatPlayerControlMode ControlMode => _controlMode;
        public event Action TestControlModeToggleRequested;
        private void Awake()
        {
            var playerLayer = LayerMask.NameToLayer("Player");
            var botLayer = LayerMask.NameToLayer("Bot");
            if (playerLayer >= 0)
            {
                SetActorLayerRecursively(transform, playerLayer, playerLayer, botLayer);
            }
        }
        private void Start()
        { BindCamera(_camera == null ? Camera.main : _camera, _cameraHeading); _motion = GetComponent<CatSecondaryMotion>(); _combat ??= GetComponent<CatPlayerCombat>(); SetHome(transform.position, transform.rotation); }
        private void OnDisable() { _motor?.ClearInput(); _combat?.ClearInput(); _motion?.SetLookPitch(0); }
        private void OnApplicationFocus(bool focused) { _focused = focused; if (!focused) { _motor.ClearInput(); _combat?.ClearInput(); _motion?.SetLookPitch(0); } }
        private void Update()
        {
            if (!_inputEnabled)
            {
                _motor.ClearInput();
                _combat?.ClearInput();
                _motion?.SetLookPitch(0);
                return;
            }

            var keys = Keyboard.current;
            if (!_focused || keys == null) { _motor.ClearInput(); _combat?.ClearInput(); _motion?.SetLookPitch(0); return; }
            if (keys.backspaceKey.wasPressedThisFrame) { ResetPlayer(); return; }
            if (keys.lKey.wasPressedThisFrame) { RequestTestControlModeToggle(); }
            if (keys.digit0Key.wasPressedThisFrame) { ResumeMovement(); }
            if (keys.digit1Key.wasPressedThisFrame) { PreviewClip(0); }
            if (keys.digit2Key.wasPressedThisFrame) { PreviewClip(1); }
            if (keys.digit3Key.wasPressedThisFrame) { PreviewClip(2); }
            if (IsPreviewing)
            { _combat?.ClearInput(); if (keys.spaceKey.wasPressedThisFrame) { TogglePause(); } return; }
            // Facing is evaluated before combat movement locks so combat cannot reset turn inertia every frame.
            var forward = _camera == null ? transform.forward : Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            if (_combat != null)
            {
                var mouse = Mouse.current;
                if (mouse?.leftButton.wasPressedThisFrame == true) { _combat.RequestAttack(); }
                _combat.SetGrabHeld(mouse?.rightButton.isPressed == true);
                if (keys.leftShiftKey.wasPressedThisFrame) { _combat.RequestLift(); }
                if (keys.qKey.wasPressedThisFrame) { _combat.RequestAbility(); }
                if (keys.zKey.wasPressedThisFrame) { _combat.CycleAbility(-1); }
                if (keys.xKey.wasPressedThisFrame) { _combat.CycleAbility(1); }
                if (_combat.CanTurn) { SubmitFacing(forward); }
                if (!_combat.CanMove)
                {
                    if (_combat.CanTurn) { _motor.ClearMovementInput(); }
                    else { _motor.ClearInput(); }
                    return;
                }
            }
            else { SubmitFacing(forward); }
            var x = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
            var y = (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0);
            _motor.Submit(forward * y + right * x, keys.spaceKey.wasPressedThisFrame);
        }
        // Camera presents first (order 0), then look intent (100), then soft bones (120).
        private void LateUpdate()
        {
            if (_motion == null) { return; }
            if (!_inputEnabled || !_focused || IsPreviewing || _camera == null) { _motion.SetLookPitch(0); return; }
            var forward = _camera.transform.forward;
            var pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg;
            _motion.SetLookPitch(pitch - _neutralCameraPitch);
        }
        public void BindCamera(Camera camera, ICameraHeadingProvider cameraHeading = null)
        {
            _camera = camera;
            _cameraHeading = cameraHeading;
        }
        /// <summary>Switches between RMB Lobby orbit and continuous Party Match mouse-look.</summary>
        public void SetControlMode(CatPlayerControlMode mode)
        {
            if (_controlMode == mode) { return; }
            _controlMode = mode;
            _motor.ClearInput();
        }

        /// <summary>Enables or disables local device input without disabling presentation components.</summary>
        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
            if (!enabled)
            {
                _motor.ClearInput();
                _combat?.ClearInput();
                _motion?.SetLookPitch(0);
            }
        }
        private void RequestTestControlModeToggle()
        {
            if (TestControlModeToggleRequested != null)
            {
                TestControlModeToggleRequested.Invoke();
                return;
            }

            SetControlMode(_controlMode == CatPlayerControlMode.Lobby
                ? CatPlayerControlMode.PartyMatch
                : CatPlayerControlMode.Lobby);
        }
        public void SetHome(Vector3 position, Quaternion rotation) { _home = position; _homeRotation = rotation; }
        public void PreviewClip(int index)
        { if (index < 0 || index >= CatAnimationController.PreviewClipCount) { return; } _motor.SetSuspended(true); _motor.Warp(_home, _homeRotation); _cameraHeading?.AlignBehindTarget(); _animation.Preview(index); }
        public void ResumeMovement() { _animation.ResumeMovement(); _motor.SetSuspended(false); }
        public void TogglePause() => _animation.TogglePause();
        public void RestartClip() => _animation.RestartClip();
        public void Seek(float normalizedTime) => _animation.Seek(normalizedTime);
        public void ResetPlayer()
        {
            ResumeMovement();
            var combatant = GetComponent<CatCombatant>();
            if (combatant != null) { combatant.SetSpawn(_home, _homeRotation); combatant.Respawn(); }
            else { _motor.Warp(_home, _homeRotation); }
            _cameraHeading?.AlignBehindTarget();
        }
        private void SubmitFacing(Vector3 fallbackForward)
        {
            if (_cameraHeading != null) { _motor.SubmitFacing(_cameraHeading.PlanarForward, _cameraHeading.PlanarTurnSpeed); }
            else { _motor.SubmitFacing(fallbackForward); }
        }
        private static void SetActorLayerRecursively(
            Transform node,
            int targetLayer,
            int playerLayer,
            int botLayer)
        {
            var currentLayer = node.gameObject.layer;
            if (currentLayer == 0 || currentLayer == playerLayer || currentLayer == botLayer)
            {
                node.gameObject.layer = targetLayer;
            }
            for (var i = 0; i < node.childCount; i++)
            {
                SetActorLayerRecursively(node.GetChild(i), targetLayer, playerLayer, botLayer);
            }
        }
    }
}
