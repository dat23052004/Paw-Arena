using System.Collections.Generic;
using PawArena.Core.Inventory;
using UnityEngine;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>
    /// Stores designer-authored catalog metadata for an inventory item.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Paw Arena/Inventory/Item")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _itemId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField, TextArea(2, 5)] private string _description = string.Empty;
        [SerializeField] private ItemCategory _category = ItemCategory.Fur;
        [SerializeField] private ItemRarity _rarity = ItemRarity.Common;

        [Header("Acquisition")]
        [SerializeField] private UnlockType _unlockType = UnlockType.Coins;
        [SerializeField, Min(0)] private long _price = 0L;

        [Header("Presentation")]
        [SerializeField] private Sprite _icon = null;
        [SerializeField] private GameObject _previewPrefab = null;

        [Header("Equipment")]
        [SerializeField] private List<EquipmentSlotReference> _allowedSlots = new();

        /// <summary>
        /// Gets the stable catalog identifier.
        /// </summary>
        public string ItemId => _itemId;

        /// <summary>
        /// Gets the player-facing name.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets the player-facing description.
        /// </summary>
        public string Description => _description;

        /// <summary>
        /// Gets the inventory category.
        /// </summary>
        public ItemCategory Category => _category;

        /// <summary>
        /// Gets the rarity tier.
        /// </summary>
        public ItemRarity Rarity => _rarity;

        /// <summary>
        /// Gets the acquisition route.
        /// </summary>
        public UnlockType UnlockType => _unlockType;

        /// <summary>
        /// Gets the coin price where applicable.
        /// </summary>
        public long Price => _price;

        /// <summary>
        /// Gets the inventory icon.
        /// </summary>
        public Sprite Icon => _icon;

        /// <summary>
        /// Gets the optional preview prefab.
        /// </summary>
        public GameObject PreviewPrefab => _previewPrefab;

        /// <summary>
        /// Builds the Unity-independent descriptor consumed by domain services.
        /// </summary>
        public ItemDescriptor BuildDescriptor()
        {
            var slots = new List<EquipmentSlotId>(_allowedSlots.Count);
            for (var i = 0; i < _allowedSlots.Count; i++)
            {
                if (_allowedSlots[i] != null)
                {
                    slots.Add(_allowedSlots[i].ToSlotId());
                }
            }

            return new ItemDescriptor(
                _itemId,
                _displayName,
                _category,
                _rarity,
                _unlockType,
                _price,
                slots);
        }

        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_itemId))
            {
                _itemId = name.Trim().ToLowerInvariant().Replace(' ', '_');
            }

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                _displayName = name;
            }

            _price = System.Math.Max(0L, _price);
        }

        protected void SetCategory(ItemCategory category) => _category = category;
    }
}
