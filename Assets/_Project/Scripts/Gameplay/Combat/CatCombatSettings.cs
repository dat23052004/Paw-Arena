using UnityEngine;

namespace PawArena.Gameplay.Combat
{
    /// <summary>Shared combat balance. Runtime components copy state and never mutate this asset.</summary>
    [CreateAssetMenu(fileName = "CatCombat", menuName = "Paw Arena/Combat/Cat Combat Settings")]
    public sealed class CatCombatSettings : ScriptableObject
    {
        [Header("Health / recovery")]
        [SerializeField, Min(1)] private float _maxHealth = 100;
        [SerializeField, Min(.1f)] private float _hitStunSeconds = .32f;
        [SerializeField, Min(.1f)] private float _knockoutSeconds = 2.5f;
        [SerializeField, Min(0)] private float _hitImmunitySeconds = .15f;
        [SerializeField] private float _ringOutY = -3;

        [Header("Punch")]
        [SerializeField, Min(.1f)] private float _punchSeconds = .48f;
        [SerializeField, Min(0)] private float _punchContactStart = .14f;
        [SerializeField, Min(0)] private float _punchContactEnd = .24f;
        [SerializeField, Min(.1f)] private float _punchRange = 1.55f;
        [SerializeField, Range(10, 180)] private float _punchAngle = 105;
        [SerializeField, Min(0)] private float _punchDamage = 24;
        [SerializeField, Min(0)] private float _punchImpulse = 7.5f;
        [SerializeField, Min(0)] private float _comboBufferSeconds = .14f;

        [Header("Grab / throw")]
        [SerializeField, Min(.1f)] private float _grabRange = 1.7f;
        [SerializeField, Min(.1f)] private float _grabWindupSeconds = .32f;
        [SerializeField, Min(.1f)] private float _holdSeconds = 4;
        [SerializeField, Min(.1f)] private float _liftSeconds = .65f;
        [SerializeField, Min(.1f)] private float _gripDistance = 1;
        [SerializeField, Min(.1f)] private float _carryHeight = .75f;
        [SerializeField, Min(.1f)] private float _throwImpulse = 12;

        [Header("Abilities")]
        [SerializeField] private CatAbilityTuning _dash = new(CatCombatAbility.Dash, 8, .35f, .35f, 17, 2.2f);
        [SerializeField] private CatAbilityTuning _nineLives = new(CatCombatAbility.NineLives, 25, 1.2f, 0, 0, 1);
        [SerializeField] private CatAbilityTuning _hiss = new(CatCombatAbility.Hiss, 14, .6f, 0, 7, 2.4f);
        [SerializeField] private CatAbilityTuning _pounce = new(CatCombatAbility.Pounce, 14, 1.1f, 0, 10, 10);
        [SerializeField] private CatAbilityTuning _anchor = new(CatCombatAbility.Anchor, 15, .6f, 3, 0, 1);
        [SerializeField] private CatAbilityTuning _stomp = new(CatCombatAbility.Stomp, 16, .8f, 0, 10, 4);
        [SerializeField] private CatAbilityTuning _catnip = new(CatCombatAbility.Catnip, 16, .8f, 3, 0, 1);
        [SerializeField] private CatAbilityTuning _fluffUp = new(CatCombatAbility.FluffUp, 16, .9f, 4, 0, 1);
        [SerializeField] private CatAbilityTuning _hairball = new(CatCombatAbility.Hairball, 20, .8f, 6, 0, 4);
        [SerializeField] private CatAbilityTuning _sharkAttack = new(CatCombatAbility.SharkAttack, 20, 1.1f, 0, 13, 14);
        [SerializeField] private CatAbilityTuning _windShurikens = new(CatCombatAbility.WindShurikens, 25, 1.1f, 0, 7, 16);

        public float MaxHealth => _maxHealth;
        public float HitStunSeconds => _hitStunSeconds;
        public float KnockoutSeconds => _knockoutSeconds;
        public float HitImmunitySeconds => _hitImmunitySeconds;
        public float RingOutY => _ringOutY;
        public float PunchSeconds => _punchSeconds;
        public float PunchContactStart => _punchContactStart;
        public float PunchContactEnd => _punchContactEnd;
        public float PunchRange => _punchRange;
        public float PunchAngle => _punchAngle;
        public float PunchDamage => _punchDamage;
        public float PunchImpulse => _punchImpulse;
        public float ComboBufferSeconds => _comboBufferSeconds;
        public float GrabRange => _grabRange;
        public float GrabWindupSeconds => _grabWindupSeconds;
        public float HoldSeconds => _holdSeconds;
        public float LiftSeconds => _liftSeconds;
        public float GripDistance => _gripDistance;
        public float CarryHeight => _carryHeight;
        public float ThrowImpulse => _throwImpulse;

        public CatAbilityTuning Ability(CatCombatAbility ability) => ability switch
        {
            CatCombatAbility.Dash => _dash,
            CatCombatAbility.NineLives => _nineLives,
            CatCombatAbility.Hiss => _hiss,
            CatCombatAbility.Pounce => _pounce,
            CatCombatAbility.Anchor => _anchor,
            CatCombatAbility.Stomp => _stomp,
            CatCombatAbility.Catnip => _catnip,
            CatCombatAbility.FluffUp => _fluffUp,
            CatCombatAbility.Hairball => _hairball,
            CatCombatAbility.SharkAttack => _sharkAttack,
            CatCombatAbility.WindShurikens => _windShurikens,
            _ => _dash
        };

        private void OnValidate()
        {
            _punchContactStart = Mathf.Clamp(_punchContactStart, 0, _punchSeconds - .02f);
            _punchContactEnd = Mathf.Clamp(_punchContactEnd, _punchContactStart + .01f, _punchSeconds);
        }
    }
}
