using System;

namespace PawArena.Core.Economy
{
    /// <summary>
    /// Represents one accepted wallet ledger entry.
    /// </summary>
    public sealed class WalletTransaction
    {
        /// <summary>
        /// Creates an immutable ledger entry.
        /// </summary>
        public WalletTransaction(
            string transactionId,
            CurrencyType currency,
            long delta,
            string reason,
            string sourceId,
            DateTimeOffset timestamp)
        {
            TransactionId = transactionId;
            Currency = currency;
            Delta = delta;
            Reason = reason;
            SourceId = sourceId;
            Timestamp = timestamp;
        }

        /// <summary>
        /// Gets the idempotency key supplied by the caller.
        /// </summary>
        public string TransactionId { get; }

        /// <summary>
        /// Gets the affected currency.
        /// </summary>
        public CurrencyType Currency { get; }

        /// <summary>
        /// Gets the signed balance change.
        /// </summary>
        public long Delta { get; }

        /// <summary>
        /// Gets the machine-readable transaction reason.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets the source content or operation identifier.
        /// </summary>
        public string SourceId { get; }

        /// <summary>
        /// Gets the UTC timestamp assigned by the wallet authority.
        /// </summary>
        public DateTimeOffset Timestamp { get; }
    }
}
