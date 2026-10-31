namespace PawArena.Core.Economy
{
    /// <summary>Describes an idempotent paid wheel-ticket balance operation.</summary>
    public enum WheelTicketTransactionResult
    {
        Success,
        DuplicateTransaction,
        InsufficientTickets,
        InvalidTransaction
    }
}
