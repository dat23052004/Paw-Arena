namespace PawArena.Core.Economy
{
    /// <summary>
    /// Coordinates validated wallet deductions with inventory grants.
    /// </summary>
    public interface IInventoryPurchaseService
    {
        /// <summary>
        /// Attempts to purchase one coin-priced inventory item exactly once.
        /// </summary>
        InventoryPurchaseResult TryPurchase(string transactionId, string itemId);
    }
}
