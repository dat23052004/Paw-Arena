using System;
using PawArena.Gameplay.Characters;
using PawArena.Gameplay.Combat;
using PawArena.Gameplay.Player;
using UnityEngine;

namespace PawArena.Gameplay.Maps
{
    /// <summary>Owns a local actor's short sinking reaction; restored on warp, exit or disable.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CatLocomotion), typeof(CapsuleCollider))]
    public sealed class CauldronWaterInteractor : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float _sinkSpeed = 1.35f;
        [SerializeField, Min(.1f)] private float _waterDrag = 7;
        [SerializeField, Min(.5f)] private float _sinkDuration = 3.2f;
        private CatLocomotion _motor;
        private CatPlayerController _input;
        private CatPlayerCombat _combatInput;
        private CatCombatant _combatant;
        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private CauldronWaterSurface _water;
        private bool _motorEnabled;
        private bool _inputEnabled;
        private bool _combatEnabled;
        private bool _movementEnabled;
        private bool _completed;
        private float _elapsed;
        private float _bubbleTimer;

        /// <summary>Raised once after sinking; the scene or match decides respawn/elimination.</summary>
        public event Action<CauldronWaterInteractor> SinkCompleted;

        private void Awake()
        {
            _motor = GetComponent<CatLocomotion>();
            _body = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            _input = GetComponent<CatPlayerController>();
            _combatInput = GetComponent<CatPlayerCombat>();
            _combatant = GetComponent<CatCombatant>();
        }

        private void OnEnable() => _motor.Warped += LeaveWater;

        private void OnDisable()
        {
            _motor.Warped -= LeaveWater;
            LeaveWater();
        }

        private void FixedUpdate()
        {
            if (_water == null)
            {
                // Unity objects can disappear while an additive map is unloaded.
                if (_elapsed > 0) { LeaveWater(); }
                return;
            }
            if (!_water.isActiveAndEnabled || !_water.Contains(_body.position, .8f) || _body.isKinematic)
            {
                LeaveWater();
                return;
            }
            _elapsed += Time.fixedDeltaTime;
            var velocity = _body.linearVelocity;
            var target = Vector3.down * _sinkSpeed;
            var next = Vector3.Lerp(velocity, target, 1 - Mathf.Exp(-_waterDrag * Time.fixedDeltaTime));
            // Counter the next gravity step while converging to a controlled terminal sinking speed.
            if (_body.useGravity) { next -= Physics.gravity * Time.fixedDeltaTime; }
            _body.linearVelocity = next;
            _bubbleTimer -= Time.fixedDeltaTime;
            if (_bubbleTimer <= 0)
            {
                _bubbleTimer = .15f;
                var point = _body.position + Vector3.up * .9f + UnityEngine.Random.insideUnitSphere * .2f;
                if (point.y < _water.SurfaceY) { _water.EmitBubble(point, UnityEngine.Random.Range(.06f, .16f)); }
            }
            if (!_completed && _elapsed >= _sinkDuration)
            {
                _completed = true;
                SinkCompleted?.Invoke(this);
            }
        }

        /// <summary>Begins one reaction per entry, deduplicating the actor's multiple colliders.</summary>
        public void EnterWater(CauldronWaterSurface water)
        {
            if (!isActiveAndEnabled || _water != null || water == null || _body.isKinematic) { return; }
            var feet = new Vector3(_body.position.x, _capsule.bounds.min.y, _body.position.z);
            if (!water.Contains(feet, .25f)) { return; }
            var speed = Mathf.Max(0, -_body.linearVelocity.y);
            _water = water;
            _elapsed = .001f;
            _bubbleTimer = 0;
            _completed = false;
            _motorEnabled = _motor.enabled;
            _movementEnabled = _motor.MovementControlEnabled;
            _inputEnabled = _input != null && _input.enabled;
            _combatEnabled = _combatInput != null && _combatInput.enabled;
            if (_combatant != null)
            {
                _combatant.Holder?.ReleaseHeld(Vector3.zero);
                _combatant.ReleaseHeld(Vector3.zero);
            }
            if (_input != null) { _input.enabled = false; }
            if (_combatInput != null) { _combatInput.enabled = false; }
            _motor.enabled = false;
            var velocity = _body.linearVelocity;
            velocity.y = Mathf.Max(velocity.y, -4);
            _body.linearVelocity = velocity;
            water.Splash(feet, speed);
        }

        private void LeaveWater()
        {
            if (_elapsed <= 0) { return; }
            _water = null;
            _elapsed = 0;
            _completed = false;
            if (_motor != null)
            {
                _motor.enabled = _motorEnabled;
                _motor.ClearInput();
            }
            if (_input != null) { _input.enabled = _inputEnabled; }
            if (_combatInput != null) { _combatInput.enabled = _combatEnabled; }
            if (_motor != null) { _motor.SetMovementControl(_movementEnabled); }
        }
    }
}
