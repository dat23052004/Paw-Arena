using PawArena.Core.Inventory;
using UnityEngine;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>
    /// Extends inventory metadata with active ability timing and targeting data.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityDefinition", menuName = "Paw Arena/Inventory/Ability")]
    public sealed class AbilityDefinition : ItemDefinition
    {
        [Header("Ability")]
        [SerializeField, Min(0f)] private float _cooldownSeconds = 1f;
        [SerializeField, Min(0f)] private float _durationSeconds = 0f;
        [SerializeField] private AbilityTargetingMode _targetingMode = AbilityTargetingMode.Self;
        [SerializeField, Min(1)] private int _balanceVersion = 1;

        /// <summary>
        /// Gets the server-authoritative cooldown duration.
        /// </summary>
        public float CooldownSeconds => _cooldownSeconds;

        /// <summary>
        /// Gets the configured active effect duration.
        /// </summary>
        public float DurationSeconds => _durationSeconds;

        /// <summary>
        /// Gets the targeting mode.
        /// </summary>
        public AbilityTargetingMode TargetingMode => _targetingMode;

        /// <summary>
        /// Gets the balance revision for analytics and migrations.
        /// </summary>
        public int BalanceVersion => _balanceVersion;

        protected override void OnValidate()
        {
            base.OnValidate();
            SetCategory(ItemCategory.Ability);
            _balanceVersion = Mathf.Max(1, _balanceVersion);
        }
    }
}
