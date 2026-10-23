using System;
using System.Collections.Generic;

namespace PawArena.Core.Inventory
{
    /// <summary>
    /// Owns player item ownership and equipment independently from UI selection.
    /// </summary>
    public interface IInventoryService
    {
        /// <summary>
        /// Raised after an item is granted for the first time.
        /// </summary>
        event Action<string> ItemGranted;

        /// <summary>
        /// Raised after an item occupies an equipment slot.
        /// </summary>
        event Action<EquipmentSlotId, string> ItemEquipped;

        /// <summary>
        /// Raised after an item leaves an equipment slot.
        /// </summary>
        event Action<EquipmentSlotId, string> ItemUnequipped;

        /// <summary>
        /// Gets an immutable view of owned item identifiers.
        /// </summary>
        IReadOnlyCollection<string> OwnedItemIds { get; }

        /// <summary>
        /// Returns whether the player owns an item.
        /// </summary>
        bool IsOwned(string itemId);

        /// <summary>
        /// Returns whether an item currently occupies any equipment slot.
        /// </summary>
        bool IsEquipped(string itemId);

        /// <summary>
        /// Attempts to grant a catalogued item exactly once.
        /// </summary>
        InventoryOperationResult TryGrant(string itemId);

        /// <summary>
        /// Attempts to equip an owned item into a compatible slot.
        /// </summary>
        InventoryOperationResult TryEquip(string itemId, EquipmentSlotId slot);

        /// <summary>
        /// Attempts to clear an equipment slot.
        /// </summary>
        InventoryOperationResult TryUnequip(EquipmentSlotId slot);

        /// <summary>
        /// Attempts to read the item occupying a slot.
        /// </summary>
        bool TryGetEquipped(EquipmentSlotId slot, out string itemId);

        /// <summary>
        /// Captures serializable inventory state.
        /// </summary>
        InventorySnapshot CaptureSnapshot();
    }
}
