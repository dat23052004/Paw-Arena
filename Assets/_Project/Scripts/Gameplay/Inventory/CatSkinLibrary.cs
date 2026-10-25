using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>Maps stable inventory IDs to appearance assets without loading replacement models.</summary>
    [CreateAssetMenu(fileName = "CatSkinLibrary", menuName = "Paw Arena/Inventory/Cat Skin Library")]
    public sealed class CatSkinLibrary : ScriptableObject
    {
        [SerializeField] private List<CatSkinDefinition> _skins = new();
        [SerializeField] private CatSkinDefinition _defaultSkin = null;

        /// <summary>Skin displayed when the body slot is empty.</summary>
        public CatSkinDefinition DefaultSkin => _defaultSkin;
        /// <summary>All authored appearance definitions.</summary>
        public IReadOnlyList<CatSkinDefinition> Skins => _skins;

        /// <summary>Resolves a known skin ID, returning null otherwise.</summary>
        public CatSkinDefinition Find(string itemId)
        {
            foreach (var skin in _skins)
            {
                if (skin != null && string.Equals(skin.ItemId, itemId, StringComparison.Ordinal)) { return skin; }
            }
            return null;
        }
    }
}
