using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawArena.Gameplay.Inventory
{
    [CreateAssetMenu(fileName = "CatGearLibrary", menuName = "Paw Arena/Inventory/Cat Gear Library")]
    public sealed class CatGearLibrary : ScriptableObject
    {
        [SerializeField] private List<CatGearDefinition> _gear = new();
        public IReadOnlyList<CatGearDefinition> Gear => _gear;

        /// <summary>Resolves a stable inventory identifier without loading or cloning a character.</summary>
        public CatGearDefinition Find(string itemId)
        {
            foreach (var gear in _gear)
            {
                if (gear != null && string.Equals(gear.ItemId, itemId, StringComparison.Ordinal)) { return gear; }
            }
            return null;
        }
    }
}
