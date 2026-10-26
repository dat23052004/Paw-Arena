using System;
using System.Collections.Generic;
using PawArena.Gameplay.Characters;
using UnityEngine;

namespace PawArena.Gameplay.Combat
{
    /// <summary>Health, reactions, temporary statuses and physical grab links shared by players and targets.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CatLocomotion), typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class CatCombatant : MonoBehaviour
    {
        private static readonly List<CatCombatant> ActiveCombatants = new(16);

        [SerializeField] private CatCombatSettings _settings;
        [SerializeField] private CatLocomotion _motor;
        [SerializeField] private bool _isPlayer;

        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private float _phaseRemaining;
        private float _hitImmunity;
        private float _anchorRemaining;
        private float _catnipRemaining;
        private float _fluffRemaining;
        private float _slipRemaining;
        private bool _initialized;

        public static IReadOnlyList<CatCombatant> Active => ActiveCombatants;
        public CatCombatSettings Settings => _settings;
        public CatLocomotion Motor => _motor;
        public Rigidbody Body => _body;
        public CapsuleCollider Capsule => _capsule;
        public bool IsPlayer => _isPlayer;
        public float Health { get; private set; }
        public float HealthNormalized => _settings == null ? 0 : Health / _settings.MaxHealth;
        public CatCombatPhase Phase { get; private set; } = CatCombatPhase.Free;
        public float PhaseRemaining => _phaseRemaining;
        public CatCombatant Held { get; private set; }
        public CatCombatant Holder { get; private set; }
        public bool IsKnockedOut => Phase == CatCombatPhase.KnockedOut;
        public bool CanAct => Phase == CatCombatPhase.Free;
        public bool CanMove => Phase is CatCombatPhase.Free or CatCombatPhase.Grab or CatCombatPhase.Holding or CatCombatPhase.Carrying;
        public bool CanTurn => Phase is CatCombatPhase.Free or CatCombatPhase.Punch or CatCombatPhase.Grab or
            CatCombatPhase.Holding or CatCombatPhase.Lifting or CatCombatPhase.Carrying or CatCombatPhase.Throwing;
        public float DamageMultiplier => _fluffRemaining > 0 ? 1.6f : 1;
        public float KnockbackMultiplier => _anchorRemaining > 0 ? .2f : 1;
        public float MovementMultiplier => _anchorRemaining > 0 ? .4f : _catnipRemaining > 0 ? 1.45f : _slipRemaining > 0 ? .55f : 1;
        public event Action<CatCombatant> Changed;
        public event Action<CatCombatant, CatCombatant> HitReceived;
        public event Action<CatCombatant> KnockedOut;

        private void Awake() => Initialize();

        private void OnEnable()
        {
            if (!ActiveCombatants.Contains(this)) { ActiveCombatants.Add(this); }
        }

        private void Start() => SetSpawn(transform.position, transform.rotation);

        private void Update() => Step(Time.deltaTime);

        private void FixedUpdate()
        {
            if (Held == null || Held._body == null) { return; }
            var carrying = Phase is CatCombatPhase.Lifting or CatCombatPhase.Carrying or CatCombatPhase.Throwing;
            var height = carrying ? _settings.CarryHeight : .15f;
            var target = _body.position + transform.forward * _settings.GripDistance + Vector3.up * height;
            Held._body.MovePosition(target);
            Held._body.MoveRotation(_body.rotation);
        }

        private void OnDisable()
        {
            ActiveCombatants.Remove(this);
            ReleaseHeld(Vector3.zero);
            if (Holder != null) { Holder.ReleaseHeld(Vector3.zero); }
            _motor?.SetSpeedMultiplier(1);
            _motor?.SetMovementControl(true);
        }

        /// <summary>Idempotent setup for runtime and isolated tests.</summary>
        public void Initialize()
        {
            if (_initialized) { return; }
            if (_settings == null || _motor == null)
            {
                Debug.LogError($"{nameof(CatCombatant)} requires settings and locomotion.", this);
                enabled = false;
                return;
            }
            _motor.Initialize();
            _body = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            Health = _settings.MaxHealth;
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            if (!ActiveCombatants.Contains(this)) { ActiveCombatants.Add(this); }
            _initialized = true;
        }

        /// <summary>Advances reactions and status effects without allocating.</summary>
        public void Step(float delta)
        {
            Initialize();
            if (!_initialized || !float.IsFinite(delta) || delta <= 0) { return; }
            _hitImmunity = Mathf.Max(0, _hitImmunity - delta);
            _anchorRemaining = Mathf.Max(0, _anchorRemaining - delta);
            _catnipRemaining = Mathf.Max(0, _catnipRemaining - delta);
            _fluffRemaining = Mathf.Max(0, _fluffRemaining - delta);
            _slipRemaining = Mathf.Max(0, _slipRemaining - delta);
            _motor.SetSpeedMultiplier(MovementMultiplier);
            if (transform.position.y < _settings.RingOutY) { Respawn(); return; }
            if (_phaseRemaining <= 0) { return; }
            _phaseRemaining = Mathf.Max(0, _phaseRemaining - delta);
            if (_phaseRemaining > 0) { return; }
            if (Phase == CatCombatPhase.KnockedOut)
            {
                Health = Mathf.Max(_settings.MaxHealth * .5f, 1);
                _hitImmunity = .8f;
            }
            if (Holder == null && Held == null) { SetPhase(CatCombatPhase.Free); }
            Changed?.Invoke(this);
        }

        /// <summary>Sets the point used by ring-out recovery and manual reset.</summary>
        public void SetSpawn(Vector3 position, Quaternion rotation)
        {
            _spawnPosition = position;
            _spawnRotation = rotation;
        }

        /// <summary>Applies a validated hit once, including health, reaction and impulse.</summary>
        public bool ReceiveHit(CatCombatant attacker, float damage, Vector3 impulse)
        {
            Initialize();
            if (!_initialized || _hitImmunity > 0 || IsKnockedOut) { return false; }
            if (Holder != null) { Holder.ReleaseHeld(Vector3.zero); }
            ReleaseHeld(Vector3.zero);
            Health = Mathf.Max(0, Health - Mathf.Max(0, damage));
            _motor.SetMovementControl(true);
            _motor.SetSuspended(false);
            if (!_body.isKinematic) { _body.AddForce(impulse * KnockbackMultiplier, ForceMode.Impulse); }
            _hitImmunity = _settings.HitImmunitySeconds;
            if (Health <= 0)
            {
                SetPhase(CatCombatPhase.KnockedOut, _settings.KnockoutSeconds);
                KnockedOut?.Invoke(this);
            }
            else { SetPhase(CatCombatPhase.HitStun, _settings.HitStunSeconds); }
            HitReceived?.Invoke(attacker, this);
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>Attempts to create a single non-cyclic grab link.</summary>
        public bool TryHold(CatCombatant target)
        {
            Initialize();
            if (!_initialized || target == null || target == this || Held != null || Holder != null ||
                target.Holder != null || target.Held != null || target.IsKnockedOut) { return false; }
            Held = target;
            target.Holder = this;
            SetPhase(CatCombatPhase.Holding);
            target.SetPhase(CatCombatPhase.Held);
            target._motor.SetMovementControl(true);
            target._motor.SetSuspended(true);
            Physics.IgnoreCollision(_capsule, target._capsule, true);
            Changed?.Invoke(this);
            target.Changed?.Invoke(target);
            return true;
        }

        /// <summary>Changes the current exclusive action while preserving grab links.</summary>
        public void SetPhase(CatCombatPhase phase, float duration = 0)
        {
            Phase = phase;
            _phaseRemaining = Mathf.Max(0, duration);
            Changed?.Invoke(this);
        }

        /// <summary>Releases the current target and optionally launches it.</summary>
        public void ReleaseHeld(Vector3 impulse)
        {
            if (Held == null) { return; }
            var target = Held;
            Held = null;
            target.Holder = null;
            Physics.IgnoreCollision(_capsule, target._capsule, false);
            target._motor.SetMovementControl(true);
            target._motor.SetSuspended(false);
            target.SetPhase(impulse.sqrMagnitude > .01f ? CatCombatPhase.HitStun : CatCombatPhase.Free,
                impulse.sqrMagnitude > .01f ? _settings.HitStunSeconds : 0);
            if (!target._body.isKinematic && impulse.sqrMagnitude > 0) { target._body.AddForce(impulse, ForceMode.Impulse); }
            if (Phase is CatCombatPhase.Holding or CatCombatPhase.Lifting or CatCombatPhase.Carrying or CatCombatPhase.Throwing)
            { SetPhase(CatCombatPhase.Free); }
            target.Changed?.Invoke(target);
        }

        /// <summary>Activates one of the three persistent ability statuses.</summary>
        public void ApplyBuff(CatCombatAbility ability, float duration)
        {
            switch (ability)
            {
                case CatCombatAbility.Anchor: _anchorRemaining = Mathf.Max(_anchorRemaining, duration); break;
                case CatCombatAbility.Catnip: _catnipRemaining = Mathf.Max(_catnipRemaining, duration); break;
                case CatCombatAbility.FluffUp: _fluffRemaining = Mathf.Max(_fluffRemaining, duration); break;
            }
            Changed?.Invoke(this);
        }

        /// <summary>Applies temporary traction loss from Hairball.</summary>
        public void ApplySlip(float duration) => _slipRemaining = Mathf.Max(_slipRemaining, duration);

        /// <summary>Nine Lives immediately restores an incapacitated combatant.</summary>
        public bool ReviveFromNineLives()
        {
            if (!IsKnockedOut) { return false; }
            Health = _settings.MaxHealth * .5f;
            _hitImmunity = 1.2f;
            SetPhase(CatCombatPhase.Ability);
            return true;
        }

        /// <summary>Restores combat state at the registered spawn without replacing the object.</summary>
        public void Respawn()
        {
            if (Holder != null) { Holder.ReleaseHeld(Vector3.zero); }
            ReleaseHeld(Vector3.zero);
            Health = _settings.MaxHealth;
            _hitImmunity = 1;
            _anchorRemaining = _catnipRemaining = _fluffRemaining = _slipRemaining = 0;
            _motor.SetSuspended(false);
            _motor.SetSpeedMultiplier(1);
            _motor.SetMovementControl(true);
            _motor.Warp(_spawnPosition, _spawnRotation);
            SetPhase(CatCombatPhase.Free);
            Changed?.Invoke(this);
        }
    }
}
