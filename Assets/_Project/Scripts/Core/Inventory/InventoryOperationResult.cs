namespace PawArena.Core.Inventory
{
    /// <summary>
    /// Describes the outcome of an inventory mutation.
    /// </summary>
    public enum InventoryOperationResult
    {
        Success,
        UnknownItem,
        AlreadyOwned,
        NotOwned,
        InvalidSlot,
        AlreadyEquipped,
        NotEquipped
    }
}
