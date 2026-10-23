namespace PawArena.Core.Economy
{
    /// <summary>
    /// Describes the outcome of an idempotent wallet operation.
    /// </summary>
    public enum WalletTransactionResult
    {
        Success,
        DuplicateTransaction,
        InvalidTransaction,
        InsufficientFunds
    }
}
