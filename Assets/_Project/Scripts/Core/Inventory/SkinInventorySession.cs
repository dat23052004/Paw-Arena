using System;
using System.Collections.Generic;
using PawArena.Core.Profiles;

namespace PawArena.Core.Inventory
{
    /// <summary>Separates browsing from equipping and persists explicit local inventory changes.</summary>
    public sealed class SkinInventorySession : IDisposable
    {
        /// <summary>The one body-skin slot; head gear occupies a different slot.</summary>
        public static readonly EquipmentSlotId SkinSlot = new(EquipmentSlotType.Fur);

        private readonly IProfileService _profile;
        private readonly List<ItemDescriptor> _items = new();
        private bool _disposed;

        /// <summary>Creates a session over existing ownership and profile services.</summary>
        public SkinInventorySession(IProfileService profile, IItemCatalog catalog, bool includeGear = false)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (catalog == null) { throw new ArgumentNullException(nameof(catalog)); }
            foreach (var item in catalog.Items)
            {
                if ((item.Category == ItemCategory.Fur && item.SupportsSlot(SkinSlot)) ||
                    (includeGear && item.Category == ItemCategory.Gear && item.AllowedSlots.Count == 1 && CatGearSlots.Contains(item.AllowedSlots[0])))
                {
                    _items.Add(item);
                }
            }
            Items = _items.AsReadOnly();
            SelectedItemId = Inventory.TryGetEquipped(SkinSlot, out var equipped) ? equipped : null;
            if (Find(SelectedItemId) == null && _items.Count > 0) { SelectedItemId = _items[0].ItemId; }
            Inventory.ItemGranted += HandleGranted;
            Inventory.ItemEquipped += HandleEquipment;
            Inventory.ItemUnequipped += HandleEquipment;
        }

        /// <summary>Raised when selection, ownership, equipment or save feedback changes.</summary>
        public event Action Changed;
        /// <summary>Skins and optionally supported gear in designer-defined order.</summary>
        public IReadOnlyList<ItemDescriptor> Items { get; }
        /// <summary>The existing domain inventory, also used by the character.</summary>
        public IInventoryService Inventory => _profile.Inventory;
        /// <summary>Browsing selection; does not imply ownership or equipment.</summary>
        public string SelectedItemId { get; private set; }
        /// <summary>User-facing result of the last explicit operation.</summary>
        public string Status { get; private set; } = string.Empty;
        /// <summary>True if the latest save attempt failed and can be retried.</summary>
        public bool HasSaveError { get; private set; }

        /// <summary>Selects a catalog skin without changing character materials or saving.</summary>
        public bool Select(string itemId)
        {
            if (_disposed || Find(itemId) == null) { return false; }
            SelectedItemId = itemId;
            if (!HasSaveError) { Status = string.Empty; }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Equips only an owned, compatible skin; preserves it in memory if saving fails.</summary>
        public InventoryOperationResult EquipSelected()
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(SkinInventorySession)); }
            var selected = Find(SelectedItemId);
            var slot = selected?.Category == ItemCategory.Gear ? selected.AllowedSlots[0] : SkinSlot;
            var result = Inventory.TryEquip(SelectedItemId, slot);
            if (result == InventoryOperationResult.Success || result == InventoryOperationResult.AlreadyEquipped)
            {
                Save();
            }
            else
            {
                Status = result == InventoryOperationResult.NotOwned
                    ? "Bạn chưa sở hữu vật phẩm này." : "Không thể trang bị vật phẩm này.";
                Changed?.Invoke();
            }
            return result;
        }

        /// <summary>Removes only the selected equipped gear. Selecting another item cannot remove its slot occupant.</summary>
        public InventoryOperationResult UnequipSelected()
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(SkinInventorySession)); }
            var selected = Find(SelectedItemId);
            if (selected == null || selected.Category != ItemCategory.Gear) { return InventoryOperationResult.InvalidSlot; }
            var slot = selected.AllowedSlots[0];
            if (!Inventory.TryGetEquipped(slot, out var equipped) || equipped != selected.ItemId) { return InventoryOperationResult.NotEquipped; }
            var result = Inventory.TryUnequip(slot);
            if (result == InventoryOperationResult.Success) { Save(); }
            return result;
        }

        /// <summary>Saves ownership and equipment, returning false with visible feedback on failure.</summary>
        public bool Save()
        {
            if (_disposed) { return false; }
            try
            {
                _profile.Save();
                HasSaveError = false;
                Status = "Đã lưu trên máy.";
            }
            catch (Exception)
            {
                HasSaveError = true;
                Status = "Đã đổi trong phiên này nhưng chưa lưu được. Hãy bấm Thử lưu lại.";
            }
            Changed?.Invoke();
            return !HasSaveError;
        }

        /// <summary>Returns one skin descriptor, or null for an unknown identifier.</summary>
        public ItemDescriptor Find(string itemId)
        {
            foreach (var item in _items)
            {
                if (string.Equals(item.ItemId, itemId, StringComparison.Ordinal)) { return item; }
            }
            return null;
        }

        /// <summary>Releases all listeners without disposing shared profile services.</summary>
        public void Dispose()
        {
            if (_disposed) { return; }
            Inventory.ItemGranted -= HandleGranted;
            Inventory.ItemEquipped -= HandleEquipment;
            Inventory.ItemUnequipped -= HandleEquipment;
            Changed = null;
            _disposed = true;
        }

        private void HandleGranted(string itemId) => Changed?.Invoke();
        private void HandleEquipment(EquipmentSlotId slot, string itemId)
        {
            if (slot == SkinSlot || CatGearSlots.Contains(slot)) { Changed?.Invoke(); }
        }
    }
}
