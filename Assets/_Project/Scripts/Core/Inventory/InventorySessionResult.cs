namespace PawArena.Core.Inventory
{
    /// <summary>Describes the result of the primary action for a selected inventory item.</summary>
    public enum InventorySessionResult
    {
        Purchased,
        PurchasedAndEquipped,
        Equipped,
        Unequipped,
        InvalidSelection,
        NotPurchasable,
        InsufficientFunds,
        PurchaseRejected,
        InventoryRejected,
        SaveFailed
    }
}
