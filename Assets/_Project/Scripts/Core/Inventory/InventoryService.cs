using System;
using System.Collections.Generic;

namespace PawArena.Core.Inventory
{
    /// <summary>
    /// Enforces ownership-before-equip and catalog slot compatibility.
    /// </summary>
    public sealed class InventoryService : IInventoryService
    {
        private readonly IItemCatalog _catalog;
        private readonly HashSet<string> _ownedItemIds = new(StringComparer.Ordinal);
        private readonly Dictionary<EquipmentSlotId, string> _equipment = new();

        /// <summary>
        /// Creates an inventory from an optional persisted snapshot.
        /// </summary>
        public InventoryService(IItemCatalog catalog, InventorySnapshot snapshot = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            RestoreSnapshot(snapshot);
        }

        /// <inheritdoc />
        public event Action<string> ItemGranted;

        /// <inheritdoc />
        public event Action<EquipmentSlotId, string> ItemEquipped;

        /// <inheritdoc />
        public event Action<EquipmentSlotId, string> ItemUnequipped;

        /// <inheritdoc />
        public IReadOnlyCollection<string> OwnedItemIds => _ownedItemIds;

        /// <inheritdoc />
        public bool IsOwned(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId) && _ownedItemIds.Contains(itemId);
        }

        /// <inheritdoc />
        public bool IsEquipped(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            foreach (var equippedItemId in _equipment.Values)
            {
                if (string.Equals(equippedItemId, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public InventoryOperationResult TryGrant(string itemId)
        {
            if (!_catalog.TryGet(itemId, out _))
            {
                return InventoryOperationResult.UnknownItem;
            }

            if (!_ownedItemIds.Add(itemId))
            {
                return InventoryOperationResult.AlreadyOwned;
            }

            ItemGranted?.Invoke(itemId);
            return InventoryOperationResult.Success;
        }

        /// <inheritdoc />
        public InventoryOperationResult TryEquip(string itemId, EquipmentSlotId slot)
        {
            if (!_catalog.TryGet(itemId, out var item))
            {
                return InventoryOperationResult.UnknownItem;
            }

            if (!_ownedItemIds.Contains(itemId))
            {
                return InventoryOperationResult.NotOwned;
            }

            if (!item.SupportsSlot(slot))
            {
                return InventoryOperationResult.InvalidSlot;
            }

            if (_equipment.TryGetValue(slot, out var previousItemId))
            {
                if (string.Equals(previousItemId, itemId, StringComparison.Ordinal))
                {
                    return InventoryOperationResult.AlreadyEquipped;
                }

                _equipment[slot] = itemId;
                ItemUnequipped?.Invoke(slot, previousItemId);
            }
            else
            {
                _equipment.Add(slot, itemId);
            }

            ItemEquipped?.Invoke(slot, itemId);
            return InventoryOperationResult.Success;
        }

        /// <inheritdoc />
        public InventoryOperationResult TryUnequip(EquipmentSlotId slot)
        {
            if (!_equipment.TryGetValue(slot, out var itemId))
            {
                return InventoryOperationResult.NotEquipped;
            }

            _equipment.Remove(slot);
            ItemUnequipped?.Invoke(slot, itemId);
            return InventoryOperationResult.Success;
        }

        /// <inheritdoc />
        public bool TryGetEquipped(EquipmentSlotId slot, out string itemId)
        {
            return _equipment.TryGetValue(slot, out itemId);
        }

        /// <inheritdoc />
        public InventorySnapshot CaptureSnapshot()
        {
            var snapshot = new InventorySnapshot();
            foreach (var itemId in _ownedItemIds)
            {
                snapshot.OwnedItemIds.Add(itemId);
            }

            snapshot.OwnedItemIds.Sort(StringComparer.Ordinal);

            foreach (var entry in _equipment)
            {
                snapshot.Equipment.Add(new EquipmentEntryData
                {
                    SlotType = entry.Key.Type,
                    SlotIndex = entry.Key.Index,
                    ItemId = entry.Value
                });
            }

            snapshot.Equipment.Sort(CompareEquipmentEntries);
            return snapshot;
        }

        private static int CompareEquipmentEntries(EquipmentEntryData left, EquipmentEntryData right)
        {
            var typeComparison = left.SlotType.CompareTo(right.SlotType);
            return typeComparison != 0 ? typeComparison : left.SlotIndex.CompareTo(right.SlotIndex);
        }

        private void RestoreSnapshot(InventorySnapshot snapshot)
        {
            if (snapshot?.OwnedItemIds != null)
            {
                for (var i = 0; i < snapshot.OwnedItemIds.Count; i++)
                {
                    var itemId = snapshot.OwnedItemIds[i];
                    if (_catalog.TryGet(itemId, out _))
                    {
                        _ownedItemIds.Add(itemId);
                    }
                }
            }

            if (snapshot?.Equipment == null)
            {
                return;
            }

            for (var i = 0; i < snapshot.Equipment.Count; i++)
            {
                var entry = snapshot.Equipment[i];
                if (entry == null || entry.SlotIndex < 0 ||
                    !_ownedItemIds.Contains(entry.ItemId) ||
                    !_catalog.TryGet(entry.ItemId, out var item))
                {
                    continue;
                }

                var slot = new EquipmentSlotId(entry.SlotType, entry.SlotIndex);
                if (item.SupportsSlot(slot))
                {
                    _equipment[slot] = entry.ItemId;
                }
            }
        }
    }
}
