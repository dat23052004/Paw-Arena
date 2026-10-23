using System;
using System.Collections.Generic;

namespace PawArena.Core.Inventory
{
    /// <summary>
    /// Immutable domain representation of one catalog item.
    /// </summary>
    public sealed class ItemDescriptor
    {
        private readonly EquipmentSlotId[] _allowedSlots;

        /// <summary>
        /// Creates a validated item descriptor.
        /// </summary>
        public ItemDescriptor(
            string itemId,
            string displayName,
            ItemCategory category,
            ItemRarity rarity,
            UnlockType unlockType,
            long price,
            IReadOnlyList<EquipmentSlotId> allowedSlots)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item ID is required.", nameof(itemId));
            }

            if (price < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(price));
            }

            ItemId = itemId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? itemId : displayName;
            Category = category;
            Rarity = rarity;
            UnlockType = unlockType;
            Price = price;

            if (allowedSlots == null || allowedSlots.Count == 0)
            {
                _allowedSlots = Array.Empty<EquipmentSlotId>();
                return;
            }

            _allowedSlots = new EquipmentSlotId[allowedSlots.Count];
            for (var i = 0; i < allowedSlots.Count; i++)
            {
                _allowedSlots[i] = allowedSlots[i];
            }
        }

        /// <summary>
        /// Gets the stable item identifier.
        /// </summary>
        public string ItemId { get; }

        /// <summary>
        /// Gets the player-facing item name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the inventory category.
        /// </summary>
        public ItemCategory Category { get; }

        /// <summary>
        /// Gets the rarity tier.
        /// </summary>
        public ItemRarity Rarity { get; }

        /// <summary>
        /// Gets the acquisition route.
        /// </summary>
        public UnlockType UnlockType { get; }

        /// <summary>
        /// Gets the catalog price for coin-purchasable items.
        /// </summary>
        public long Price { get; }

        /// <summary>
        /// Gets the slots this item may occupy.
        /// </summary>
        public IReadOnlyList<EquipmentSlotId> AllowedSlots => _allowedSlots;

        /// <summary>
        /// Returns whether the item supports a specific slot.
        /// </summary>
        public bool SupportsSlot(EquipmentSlotId slot)
        {
            for (var i = 0; i < _allowedSlots.Length; i++)
            {
                if (_allowedSlots[i] == slot)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
