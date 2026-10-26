using System;
using UnityEngine;

namespace PawArena.Gameplay.Combat
{
    /// <summary>Designer-owned timing and strength for one combat ability.</summary>
    [Serializable]
    public sealed class CatAbilityTuning
    {
        [SerializeField] private CatCombatAbility _ability;
        [SerializeField, Min(.05f)] private float _cooldown;
        [SerializeField, Min(.05f)] private float _castSeconds;
        [SerializeField, Min(0)] private float _duration;
        [SerializeField, Min(0)] private float _power;
        [SerializeField, Min(.1f)] private float _range;

        public CatAbilityTuning(CatCombatAbility ability, float cooldown, float castSeconds, float duration, float power, float range)
        {
            _ability = ability;
            _cooldown = cooldown;
            _castSeconds = castSeconds;
            _duration = duration;
            _power = power;
            _range = range;
        }

        public CatCombatAbility Ability => _ability;
        public float Cooldown => _cooldown;
        public float CastSeconds => _castSeconds;
        public float Duration => _duration;
        public float Power => _power;
        public float Range => _range;
    }
}
