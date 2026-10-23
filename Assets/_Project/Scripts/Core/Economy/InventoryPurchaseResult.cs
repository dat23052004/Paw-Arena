namespace PawArena.Core.Economy
{
    /// <summary>
    /// Describes a coin purchase attempt for an inventory item.
    /// </summary>
    public enum InventoryPurchaseResult
    {
        Success,
        UnknownItem,
        AlreadyOwned,
        NotCoinPurchasable,
        InsufficientFunds,
        DuplicateTransaction,
        InvalidTransaction
    }
}
