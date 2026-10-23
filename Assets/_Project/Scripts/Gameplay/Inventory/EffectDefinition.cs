using PawArena.Core.Inventory;
using UnityEngine;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>
    /// Extends inventory metadata with the gameplay event that triggers an effect.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectDefinition", menuName = "Paw Arena/Inventory/Effect")]
    public sealed class EffectDefinition : ItemDefinition
    {
        [SerializeField] private EffectTriggerType _triggerType = EffectTriggerType.Hit;

        /// <summary>
        /// Gets the event that should play this effect.
        /// </summary>
        public EffectTriggerType TriggerType => _triggerType;

        protected override void OnValidate()
        {
            base.OnValidate();
            SetCategory(ItemCategory.Effect);
        }
    }
}
