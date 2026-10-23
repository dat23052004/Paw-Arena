using System;
using PawArena.Core.Inventory;

namespace PawArena.Core.Economy
{
    /// <summary>
    /// Performs the offline purchase flow through domain services.
    /// </summary>
    public sealed class InventoryPurchaseService : IInventoryPurchaseService
    {
        private const string PurchaseReason = "item_purchase";

        private readonly IItemCatalog _catalog;
        private readonly IInventoryService _inventory;
        private readonly IWalletService _wallet;

        /// <summary>
        /// Creates the purchase coordinator.
        /// </summary>
        public InventoryPurchaseService(
            IItemCatalog catalog,
            IInventoryService inventory,
            IWalletService wallet)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        /// <inheritdoc />
        public InventoryPurchaseResult TryPurchase(string transactionId, string itemId)
        {
            if (!_catalog.TryGet(itemId, out var item))
            {
                return InventoryPurchaseResult.UnknownItem;
            }

            if (_inventory.IsOwned(itemId))
            {
                return InventoryPurchaseResult.AlreadyOwned;
            }

            if (item.UnlockType != UnlockType.Coins || item.Price <= 0L)
            {
                return InventoryPurchaseResult.NotCoinPurchasable;
            }

            var walletResult = _wallet.TryApply(
                transactionId,
                CurrencyType.Coins,
                -item.Price,
                PurchaseReason,
                itemId,
                out _);

            switch (walletResult)
            {
                case WalletTransactionResult.InsufficientFunds:
                    return InventoryPurchaseResult.InsufficientFunds;
                case WalletTransactionResult.DuplicateTransaction:
                    return InventoryPurchaseResult.DuplicateTransaction;
                case WalletTransactionResult.InvalidTransaction:
                    return InventoryPurchaseResult.InvalidTransaction;
                case WalletTransactionResult.Success:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var grantResult = _inventory.TryGrant(itemId);
            if (grantResult != InventoryOperationResult.Success)
            {
                throw new InvalidOperationException(
                    $"Wallet transaction succeeded but inventory grant failed with {grantResult}.");
            }

            return InventoryPurchaseResult.Success;
        }
    }
}
