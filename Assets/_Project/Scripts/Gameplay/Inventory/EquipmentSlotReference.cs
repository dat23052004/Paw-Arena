using PawArena.Core.Inventory;
using UnityEngine;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>
    /// Serializes a domain equipment slot in catalog assets.
    /// </summary>
    [System.Serializable]
    public sealed class EquipmentSlotReference
    {
        [SerializeField] private EquipmentSlotType _type = EquipmentSlotType.Fur;
        [SerializeField, Min(0)] private int _index = 0;

        /// <summary>
        /// Converts the serialized values to a domain slot identifier.
        /// </summary>
        public EquipmentSlotId ToSlotId() => new(_type, _index);
    }
}
