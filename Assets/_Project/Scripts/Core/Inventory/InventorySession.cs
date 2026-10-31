using System;
using System.Collections.Generic;
using PawArena.Core.Economy;
using PawArena.Core.Profiles;

namespace PawArena.Core.Inventory
{
    /// <summary>
    /// Coordinates browsing, purchasing, equipping and persistence for the complete item catalog.
    /// </summary>
    public sealed class InventorySession : IDisposable
    {
        private readonly IProfileService _profile;
        private readonly IItemCatalog _catalog;
        private readonly List<ItemDescriptor> _items = new();
        private bool _disposed;

        /// <summary>Creates a UI-facing session over the active local profile.</summary>
        public InventorySession(IProfileService profile, IItemCatalog catalog)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

            foreach (var item in catalog.Items)
            {
                _items.Add(item);
            }

            Items = _items.AsReadOnly();
            SelectedItemId = FindInitialSelection();
            Inventory.ItemGranted += HandleInventoryChanged;
            Inventory.ItemEquipped += HandleEquipmentChanged;
            Inventory.ItemUnequipped += HandleEquipmentChanged;
            Wallet.BalanceChanged += HandleBalanceChanged;
        }

        /// <summary>Raised after selection, ownership, equipment, balance or save feedback changes.</summary>
        public event Action Changed;

        /// <summary>All catalog items in designer-authored order.</summary>
        public IReadOnlyList<ItemDescriptor> Items { get; }

        /// <summary>The active profile inventory.</summary>
        public IInventoryService Inventory => _profile.Inventory;

        /// <summary>The active profile wallet.</summary>
        public IWalletService Wallet => _profile.Wallet;

        /// <summary>The selected catalog item, independent from its equipped state.</summary>
        public string SelectedItemId { get; private set; }

        /// <summary>User-facing feedback from the latest explicit operation.</summary>
        public string Status { get; private set; } = string.Empty;

        /// <summary>True when the in-memory change could not be persisted.</summary>
        public bool HasSaveError { get; private set; }

        /// <summary>Selects a known catalog item without mutating ownership or equipment.</summary>
        public bool Select(string itemId)
        {
            ThrowIfDisposed();
            if (!_catalog.TryGet(itemId, out _))
            {
                return false;
            }

            SelectedItemId = itemId;
            if (!HasSaveError)
            {
                Status = string.Empty;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Buys a coin item, equips an owned item, or unequips the selected equipped item.
        /// </summary>
        public InventorySessionResult ActivateSelected()
        {
            ThrowIfDisposed();
            if (!_catalog.TryGet(SelectedItemId, out var selected))
            {
                return Fail(InventorySessionResult.InvalidSelection, "Không tìm thấy vật phẩm đã chọn.");
            }

            if (!Inventory.IsOwned(selected.ItemId))
            {
                return Purchase(selected);
            }

            if (TryGetEquippedSlot(selected, out var equippedSlot))
            {
                var unequip = Inventory.TryUnequip(equippedSlot);
                if (unequip != InventoryOperationResult.Success)
                {
                    return Fail(InventorySessionResult.InventoryRejected, "Không thể tháo vật phẩm này.");
                }

                return SaveResult(InventorySessionResult.Unequipped, "Đã tháo vật phẩm.");
            }

            if (!TryGetPreferredSlot(selected, out var slot))
            {
                return Fail(InventorySessionResult.InventoryRejected, "Vật phẩm này không thể trang bị.");
            }

            var equip = Inventory.TryEquip(selected.ItemId, slot);
            if (equip != InventoryOperationResult.Success && equip != InventoryOperationResult.AlreadyEquipped)
            {
                return Fail(InventorySessionResult.InventoryRejected, "Không thể trang bị vật phẩm này.");
            }

            return SaveResult(InventorySessionResult.Equipped, "Đã trang bị vật phẩm.");
        }

        /// <summary>Retries persistence after a recoverable local save failure.</summary>
        public bool Save()
        {
            if (_disposed)
            {
                return false;
            }

            try
            {
                _profile.Save();
                HasSaveError = false;
                Status = "Đã lưu trên máy.";
            }
            catch (Exception)
            {
                HasSaveError = true;
                Status = "Thay đổi vẫn còn trong phiên này nhưng chưa lưu được. Hãy thử lại.";
            }

            Changed?.Invoke();
            return !HasSaveError;
        }

        /// <summary>Returns one descriptor, or null when the identifier is unknown.</summary>
        public ItemDescriptor Find(string itemId) =>
            _catalog.TryGet(itemId, out var item) ? item : null;

        /// <summary>Returns the selected item's occupied slot, when equipped.</summary>
        public bool TryGetSelectedEquippedSlot(out EquipmentSlotId slot)
        {
            var selected = Find(SelectedItemId);
            return TryGetEquippedSlot(selected, out slot);
        }

        /// <summary>Releases event subscriptions without disposing shared profile services.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Inventory.ItemGranted -= HandleInventoryChanged;
            Inventory.ItemEquipped -= HandleEquipmentChanged;
            Inventory.ItemUnequipped -= HandleEquipmentChanged;
            Wallet.BalanceChanged -= HandleBalanceChanged;
            Changed = null;
            _disposed = true;
        }

        private InventorySessionResult Purchase(ItemDescriptor item)
        {
            if (item.UnlockType != UnlockType.Coins || item.Price <= 0L)
            {
                return Fail(
                    InventorySessionResult.NotPurchasable,
                    "Vật phẩm này được mở khóa qua phần thưởng, gói hoặc sự kiện.");
            }

            var transactionId = $"inventory:{_profile.PlayerId}:{Guid.NewGuid():N}";
            var result = _profile.Purchases.TryPurchase(transactionId, item.ItemId);
            if (result == InventoryPurchaseResult.InsufficientFunds)
            {
                return Fail(InventorySessionResult.InsufficientFunds, "Bạn không đủ xu để mở khóa vật phẩm này.");
            }

            if (result != InventoryPurchaseResult.Success && result != InventoryPurchaseResult.AlreadyOwned)
            {
                return Fail(InventorySessionResult.PurchaseRejected, "Không thể mở khóa vật phẩm này.");
            }

            var finalResult = InventorySessionResult.Purchased;
            var message = "Đã mở khóa vật phẩm.";
            if (TryGetPreferredSlot(item, out var slot))
            {
                var equip = Inventory.TryEquip(item.ItemId, slot);
                if (equip == InventoryOperationResult.Success || equip == InventoryOperationResult.AlreadyEquipped)
                {
                    finalResult = InventorySessionResult.PurchasedAndEquipped;
                    message = "Đã mở khóa và trang bị vật phẩm.";
                }
            }

            return SaveResult(finalResult, message);
        }

        private InventorySessionResult SaveResult(InventorySessionResult successResult, string successMessage)
        {
            Status = successMessage;
            try
            {
                _profile.Save();
                HasSaveError = false;
                Changed?.Invoke();
                return successResult;
            }
            catch (Exception)
            {
                HasSaveError = true;
                Status = "Thay đổi vẫn còn trong phiên này nhưng chưa lưu được. Hãy thử lại.";
                Changed?.Invoke();
                return InventorySessionResult.SaveFailed;
            }
        }

        private InventorySessionResult Fail(InventorySessionResult result, string message)
        {
            Status = message;
            Changed?.Invoke();
            return result;
        }

        private string FindInitialSelection()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (Inventory.IsEquipped(_items[i].ItemId))
                {
                    return _items[i].ItemId;
                }
            }

            return _items.Count > 0 ? _items[0].ItemId : null;
        }

        private bool TryGetEquippedSlot(ItemDescriptor item, out EquipmentSlotId slot)
        {
            if (item != null)
            {
                for (var i = 0; i < item.AllowedSlots.Count; i++)
                {
                    var candidate = item.AllowedSlots[i];
                    if (Inventory.TryGetEquipped(candidate, out var equippedId) &&
                        string.Equals(equippedId, item.ItemId, StringComparison.Ordinal))
                    {
                        slot = candidate;
                        return true;
                    }
                }
            }

            slot = default;
            return false;
        }

        private static bool TryGetPreferredSlot(ItemDescriptor item, out EquipmentSlotId slot)
        {
            if (item != null && item.AllowedSlots.Count > 0)
            {
                slot = item.AllowedSlots[0];
                return true;
            }

            slot = default;
            return false;
        }

        private void HandleInventoryChanged(string itemId) => Changed?.Invoke();
        private void HandleEquipmentChanged(EquipmentSlotId slot, string itemId) => Changed?.Invoke();
        private void HandleBalanceChanged(CurrencyType currency, long balance)
        {
            if (currency == CurrencyType.Coins)
            {
                Changed?.Invoke();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InventorySession));
            }
        }
    }
}
