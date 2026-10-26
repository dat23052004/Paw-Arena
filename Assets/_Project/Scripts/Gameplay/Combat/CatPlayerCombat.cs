using System;
using PawArena.Gameplay.Characters;
using UnityEngine;

namespace PawArena.Gameplay.Combat
{
    /// <summary>Player combat state machine: punch, grab/lift/throw and all retained abilities.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(90)]
    [RequireComponent(typeof(CatCombatant), typeof(CatCombatEffects))]
    public sealed class CatPlayerCombat : MonoBehaviour
    {
        private const int TargetCapacity = 16;
        private const string GrabIdleClip = "Cat_Grab_Hold";
        private const string GrabMoveClip = "Cat_Carry_Move";

        [SerializeField] private CatCombatSettings _settings;
        [SerializeField] private CatCombatant _actor;
        [SerializeField] private CatAnimationController _animation;
        [SerializeField] private CatCombatEffects _effects;
        [SerializeField] private CatCombatAbility _selectedAbility = CatCombatAbility.Dash;

        private readonly float[] _cooldowns = new float[11];
        private readonly EntityId[] _hitIds = new EntityId[TargetCapacity];
        private float _actionElapsed;
        private float _holdElapsed;
        private float _anchorVisualRemaining;
        private float _stepDelta;
        private int _hitCount;
        private int _punchCount;
        private bool _attackRequested;
        private bool _liftRequested;
        private bool _abilityRequested;
        private bool _grabHeld;
        private bool _queuedPunch;
        private bool _actionTriggered;
        private bool _secondTrigger;
        private bool _initialized;
        private CatCombatAbility _castingAbility;
        private CatCombatant _abilityTarget;

        public CatCombatAbility SelectedAbility => _selectedAbility;
        public float SelectedCooldown => _cooldowns[(int)_selectedAbility];
        public bool CanMove => _actor != null && _actor.CanMove;
        public bool CanTurn => _actor != null && _actor.CanTurn;
        public bool IsLeftPunch => (_punchCount & 1) != 0;
        public float ActionNormalizedTime
        {
            get
            {
                if (_actor == null || _settings == null) { return 0; }
                var duration = _actor.Phase switch
                {
                    CatCombatPhase.Punch => _settings.PunchSeconds,
                    CatCombatPhase.Grab => _settings.GrabWindupSeconds,
                    CatCombatPhase.Lifting => _settings.LiftSeconds,
                    CatCombatPhase.Throwing => .45f,
                    CatCombatPhase.Ability => _settings.Ability(_castingAbility).CastSeconds,
                    _ => 1
                };
                return Mathf.Clamp01(_actionElapsed / Mathf.Max(.01f, duration));
            }
        }
        public event Action<CatCombatAbility> AbilityChanged;
        public event Action<CatCombatAbility> AbilityUsed;

        private void Awake() => Initialize();
        private void Update() => Step(Time.deltaTime);
        private void OnDisable()
        {
            ClearInput();
            _animation?.ClearStance();
            _actor?.Motor?.SetMovementControl(true);
        }

        /// <summary>Idempotent setup for scene play and deterministic tests.</summary>
        public void Initialize()
        {
            if (_initialized) { return; }
            if (_settings == null || _actor == null || _animation == null || _effects == null)
            {
                Debug.LogError($"{nameof(CatPlayerCombat)} has missing references.", this);
                enabled = false;
                return;
            }
            _actor.Initialize();
            _effects.Initialize();
            _initialized = true;
        }

        /// <summary>Queues one primary attack edge. Carrying converts it into a throw.</summary>
        public void RequestAttack() => _attackRequested = true;

        /// <summary>Supplies the continuous grab intent from the held right mouse button.</summary>
        public void SetGrabHeld(bool held) => _grabHeld = held;

        /// <summary>Queues a lift edge from Left Shift for the currently held target.</summary>
        public void RequestLift() => _liftRequested = true;

        /// <summary>Queues the selected ability without spending cooldown until validation succeeds.</summary>
        public void RequestAbility() => _abilityRequested = true;

        /// <summary>Cycles the retained ability roster in either direction.</summary>
        public void CycleAbility(int direction)
        {
            var count = Enum.GetValues(typeof(CatCombatAbility)).Length;
            var index = ((int)_selectedAbility + (direction < 0 ? -1 : 1) + count) % count;
            _selectedAbility = (CatCombatAbility)index;
            AbilityChanged?.Invoke(_selectedAbility);
        }

        /// <summary>Clears device intent and safely drops a held target.</summary>
        public void ClearInput()
        {
            _attackRequested = _liftRequested = _abilityRequested = _grabHeld = _queuedPunch = false;
        }

        /// <summary>Advances action windows and resolves validated contacts.</summary>
        public void Step(float delta)
        {
            Initialize();
            if (!_initialized || !float.IsFinite(delta) || delta <= 0) { return; }
            _stepDelta = delta;
            for (var i = 0; i < _cooldowns.Length; i++) { _cooldowns[i] = Mathf.Max(0, _cooldowns[i] - delta); }
            UpdateAnchorVisual(delta);

            if (_actor.Phase == CatCombatPhase.Ability)
            {
                _actionElapsed += delta;
                TickAbility();
                UpdateCombatStance();
                ConsumeEdges();
                return;
            }

            if (_actor.IsKnockedOut)
            {
                if (_abilityRequested && _selectedAbility == CatCombatAbility.NineLives) { TryActivateAbility(); }
                UpdateCombatStance();
                ConsumeEdges();
                return;
            }
            if (_actor.Holder != null)
            {
                if (_abilityRequested && _selectedAbility == CatCombatAbility.Hiss) { TryActivateAbility(); }
                UpdateCombatStance();
                ConsumeEdges();
                return;
            }

            _actionElapsed += delta;
            switch (_actor.Phase)
            {
                case CatCombatPhase.Free: TickFree(); break;
                case CatCombatPhase.Punch: TickPunch(); break;
                case CatCombatPhase.Grab: TickGrab(); break;
                case CatCombatPhase.Holding: TickHolding(); break;
                case CatCombatPhase.Lifting: TickLifting(); break;
                case CatCombatPhase.Carrying: TickCarrying(); break;
                case CatCombatPhase.Throwing: TickThrowing(); break;
            }
            UpdateCombatStance();
            ConsumeEdges();
        }

        private void TickFree()
        {
            if (_abilityRequested && TryActivateAbility()) { return; }
            if (_attackRequested) { BeginPunch(); return; }
            if (_grabHeld)
            {
                BeginAction(CatCombatPhase.Grab);
            }
        }

        private void TickPunch()
        {
            if (_attackRequested && _actionElapsed >= _settings.PunchSeconds - _settings.ComboBufferSeconds) { _queuedPunch = true; }
            // Resolve when the simulation crosses the contact marker; a long frame must not skip the hit entirely.
            if (!_actionTriggered && _actionElapsed >= _settings.PunchContactStart)
            {
                _actionTriggered = true;
                HitCone(_settings.PunchRange, _settings.PunchAngle, _settings.PunchDamage * _actor.DamageMultiplier,
                    _settings.PunchImpulse * _actor.DamageMultiplier);
            }
            if (_actionElapsed < _settings.PunchSeconds) { return; }
            if (_queuedPunch) { BeginPunch(); }
            else { _actor.SetPhase(CatCombatPhase.Free); }
        }

        private void TickGrab()
        {
            if (!_grabHeld) { _actor.SetPhase(CatCombatPhase.Free); return; }
            if (_actionElapsed < _settings.GrabWindupSeconds) { return; }
            var target = FindTarget(_settings.GrabRange, 110);
            if (target != null && _actor.TryHold(target)) { _holdElapsed = 0; }
            // Keep the grab stance active while RMB remains held. Returning to Free here made
            // movement lock/unlock every frame and repeatedly blended Idle/Run under the pose.
        }

        private void TickHolding()
        {
            _holdElapsed += _stepDelta;
            if (!_grabHeld || _holdElapsed >= _settings.HoldSeconds) { _actor.ReleaseHeld(Vector3.zero); return; }
            if (_liftRequested && _actor.Held != null)
            {
                BeginAction(CatCombatPhase.Lifting);
                _actor.Held.SetPhase(CatCombatPhase.Carried);
            }
        }

        private void TickLifting()
        {
            if (!_grabHeld || _actor.Held == null) { _actor.ReleaseHeld(Vector3.zero); return; }
            if (_actionElapsed >= _settings.LiftSeconds) { _actor.SetPhase(CatCombatPhase.Carrying); }
        }

        private void TickCarrying()
        {
            _holdElapsed += _stepDelta;
            if (!_grabHeld || _holdElapsed >= _settings.HoldSeconds) { _actor.ReleaseHeld(Vector3.zero); return; }
            if (_attackRequested) { BeginAction(CatCombatPhase.Throwing); }
        }

        private void TickThrowing()
        {
            if (!_actionTriggered && _actionElapsed >= .2f)
            {
                _actionTriggered = true;
                var target = _actor.Held;
                if (target != null)
                {
                    _actor.ReleaseHeld(Vector3.zero);
                    target.ReceiveHit(_actor, _settings.PunchDamage,
                        transform.forward * _settings.ThrowImpulse + Vector3.up * 2.5f);
                }
            }
            if (_actionElapsed >= .45f) { _actor.SetPhase(CatCombatPhase.Free); }
        }

        private void BeginPunch()
        {
            _punchCount++;
            _queuedPunch = false;
            BeginAction(CatCombatPhase.Punch);
        }

        private bool TryActivateAbility()
        {
            var ability = _selectedAbility;
            if (_cooldowns[(int)ability] > 0) { return false; }
            var target = default(CatCombatant);
            if (ability == CatCombatAbility.NineLives)
            {
                if (!_actor.ReviveFromNineLives()) { return false; }
            }
            else if (ability == CatCombatAbility.Hiss)
            {
                if (_actor.Holder == null) { return false; }
                _actor.SetPhase(CatCombatPhase.Ability);
            }
            else
            {
                if (!_actor.CanAct) { return false; }
                if (ability is CatCombatAbility.Pounce)
                {
                    target = FindTarget(_settings.Ability(ability).Range, 130);
                    if (target == null || !_actor.Motor.Grounded) { return false; }
                }
                if (ability == CatCombatAbility.Stomp && !_actor.Motor.Grounded) { return false; }
                _actor.SetPhase(CatCombatPhase.Ability);
            }
            _castingAbility = ability;
            _abilityTarget = target;
            _cooldowns[(int)ability] = _settings.Ability(ability).Cooldown;
            _actionElapsed = 0;
            _actionTriggered = _secondTrigger = false;
            _hitCount = 0;
            _animation.PlayAction(AbilityClip(ability), _settings.Ability(ability).CastSeconds, false);
            AbilityUsed?.Invoke(ability);
            return true;
        }

        private void TickAbility()
        {
            var tuning = _settings.Ability(_castingAbility);
            switch (_castingAbility)
            {
                case CatCombatAbility.Dash:
                    TriggerOnce(.13f, () =>
                    {
                        _actor.Motor.SetMovementControl(false);
                        _actor.Body.linearVelocity = transform.forward * tuning.Power + Vector3.up * _actor.Body.linearVelocity.y;
                        HitCone(tuning.Range, 80, 18, tuning.Power * .5f);
                    });
                    break;
                case CatCombatAbility.NineLives:
                    break;
                case CatCombatAbility.Hiss:
                    TriggerOnce(.23f, () =>
                    {
                        var holder = _actor.Holder;
                        if (holder == null) { return; }
                        holder.ReleaseHeld(Vector3.zero);
                        _actor.SetPhase(CatCombatPhase.Ability);
                        var away = Vector3.ProjectOnPlane(holder.transform.position - transform.position, Vector3.up).normalized;
                        holder.ReceiveHit(_actor, 8, away * tuning.Power + Vector3.up);
                    });
                    break;
                case CatCombatAbility.Pounce:
                    TriggerOnce(.3f, () =>
                    {
                        if (_abilityTarget == null) { return; }
                        var direction = Vector3.ProjectOnPlane(_abilityTarget.transform.position - transform.position, Vector3.up).normalized;
                        _actor.Body.rotation = Quaternion.LookRotation(direction);
                        _actor.Body.linearVelocity = direction * tuning.Power + Vector3.up * 6;
                        _animation.PlayAction("Cat_Ability_Pounce_Air", Mathf.Max(.1f, tuning.CastSeconds - .3f), true);
                    });
                    if (_actionElapsed >= .34f && _abilityTarget != null && !WasHit(_abilityTarget) &&
                        Vector3.Distance(transform.position, _abilityTarget.transform.position) <= 2)
                    {
                        MarkHit(_abilityTarget);
                        _abilityTarget.ReceiveHit(_actor, 32, transform.forward * 10 + Vector3.up * 2);
                    }
                    break;
                case CatCombatAbility.Anchor:
                case CatCombatAbility.Catnip:
                case CatCombatAbility.FluffUp:
                    TriggerOnce(_castingAbility == CatCombatAbility.Anchor ? .4f : .37f,
                        () => _actor.ApplyBuff(_castingAbility, tuning.Duration));
                    break;
                case CatCombatAbility.Stomp:
                    TriggerOnce(.43f, () => HitArea(tuning.Range, 28, tuning.Power));
                    break;
                case CatCombatAbility.Hairball:
                    TriggerOnce(.33f, () => _effects.SpawnPuddle(_actor,
                        transform.position + transform.forward * 1.6f, 1.4f, tuning.Duration));
                    break;
                case CatCombatAbility.SharkAttack:
                    TriggerOnce(.53f, () => _effects.SpawnProjectile(_actor, transform.position + Vector3.up * .8f,
                        transform.forward * tuning.Range, .65f, 1.2f, 34, tuning.Power));
                    break;
                case CatCombatAbility.WindShurikens:
                    TriggerOnce(.3f, () => SpawnFan(-8, tuning));
                    if (!_secondTrigger && _actionElapsed >= .67f) { _secondTrigger = true; SpawnFan(8, tuning); }
                    break;
            }
            if (_actionElapsed < tuning.CastSeconds) { return; }
            if (_castingAbility == CatCombatAbility.Dash) { _actor.Motor.SetMovementControl(true); }
            _actor.SetPhase(CatCombatPhase.Free);
            if (_castingAbility == CatCombatAbility.Pounce && _actor.Motor.Grounded)
            { _animation.PlayAction("Cat_Ability_Pounce_Land", .5f, false); }
            if (_castingAbility == CatCombatAbility.Anchor)
            {
                _anchorVisualRemaining = tuning.Duration;
                _animation.PlayAction("Cat_Ability_Anchor_Hold", tuning.Duration, true);
            }
        }

        private void TriggerOnce(float time, Action action)
        {
            if (_actionTriggered || _actionElapsed < time) { return; }
            _actionTriggered = true;
            action();
        }

        private void HitCone(float range, float angle, float damage, float impulse)
        {
            var actors = CatCombatant.Active;
            for (var i = 0; i < actors.Count; i++)
            {
                var target = actors[i];
                if (!IsTargetInCone(target, range, angle) || WasHit(target)) { continue; }
                MarkHit(target);
                var direction = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).normalized;
                target.ReceiveHit(_actor, damage, direction * impulse + Vector3.up * 1.2f);
            }
        }

        private void HitArea(float range, float damage, float impulse)
        {
            var actors = CatCombatant.Active;
            for (var i = 0; i < actors.Count; i++)
            {
                var target = actors[i];
                if (target == null || target == _actor || WasHit(target)) { continue; }
                var offset = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
                if (offset.sqrMagnitude > range * range) { continue; }
                MarkHit(target);
                target.ReceiveHit(_actor, damage, offset.normalized * impulse + Vector3.up * 4);
            }
        }

        private CatCombatant FindTarget(float range, float angle)
        {
            var actors = CatCombatant.Active;
            CatCombatant best = null;
            var bestDistance = range * range;
            for (var i = 0; i < actors.Count; i++)
            {
                var target = actors[i];
                if (!IsTargetInCone(target, range, angle)) { continue; }
                var distance = (target.transform.position - transform.position).sqrMagnitude;
                if (distance >= bestDistance) { continue; }
                best = target;
                bestDistance = distance;
            }
            return best;
        }

        private bool IsTargetInCone(CatCombatant target, float range, float angle)
        {
            if (target == null || target == _actor || !target.isActiveAndEnabled || target.IsKnockedOut) { return false; }
            var offset = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
            return offset.sqrMagnitude <= range * range && offset.sqrMagnitude > .001f &&
                Vector3.Angle(transform.forward, offset) <= angle * .5f;
        }

        private void SpawnFan(float offset, CatAbilityTuning tuning)
        {
            for (var i = -1; i <= 1; i++)
            {
                var direction = Quaternion.AngleAxis(i * 18 + offset, Vector3.up) * transform.forward;
                _effects.SpawnProjectile(_actor, transform.position + Vector3.up, direction * tuning.Range,
                    .24f, 1.2f, 18, tuning.Power);
            }
        }

        private void BeginAction(CatCombatPhase phase)
        {
            _actor.SetPhase(phase);
            _actionElapsed = 0;
            _actionTriggered = false;
            _hitCount = 0;
        }

        private void UpdateCombatStance()
        {
            var grabPhase = _actor.Phase is CatCombatPhase.Grab or CatCombatPhase.Holding or
                CatCombatPhase.Lifting or CatCombatPhase.Carrying or CatCombatPhase.Throwing;
            if (!grabPhase || !_actor.Motor.Grounded)
            {
                _animation.ClearStance();
                return;
            }
            _animation.SetStance(_actor.Motor.PlanarSpeed > .15f ? GrabMoveClip : GrabIdleClip);
        }

        private void UpdateAnchorVisual(float delta)
        {
            if (_anchorVisualRemaining <= 0) { return; }
            _anchorVisualRemaining = Mathf.Max(0, _anchorVisualRemaining - delta);
            if (_anchorVisualRemaining <= 0) { _animation.PlayAction("Cat_Ability_Anchor_End", .6f, false); }
        }

        private bool WasHit(CatCombatant target)
        {
            var id = target.GetEntityId();
            for (var i = 0; i < _hitCount; i++) { if (_hitIds[i] == id) { return true; } }
            return false;
        }

        private void MarkHit(CatCombatant target)
        {
            if (_hitCount < _hitIds.Length) { _hitIds[_hitCount++] = target.GetEntityId(); }
        }

        private void ConsumeEdges() => _attackRequested = _liftRequested = _abilityRequested = false;

        private static string AbilityClip(CatCombatAbility ability) => ability switch
        {
            CatCombatAbility.Dash => "Cat_Ability_Dash",
            CatCombatAbility.NineLives => "Cat_Ability_NineLives_FaceUp",
            CatCombatAbility.Hiss => "Cat_Ability_Hiss",
            CatCombatAbility.Pounce => "Cat_Ability_Pounce_Start",
            CatCombatAbility.Anchor => "Cat_Ability_Anchor_Start",
            CatCombatAbility.Stomp => "Cat_Ability_Stomp",
            CatCombatAbility.Catnip => "Cat_Ability_Catnip",
            CatCombatAbility.FluffUp => "Cat_Ability_FluffUp",
            CatCombatAbility.Hairball => "Cat_Ability_Hairball",
            CatCombatAbility.SharkAttack => "Cat_Ability_SharkAttack",
            CatCombatAbility.WindShurikens => "Cat_Ability_WindShurikens",
            _ => "Cat_Idle"
        };
    }
}
