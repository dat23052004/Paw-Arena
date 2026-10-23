using System;

namespace PawArena.Core.Economy
{
    /// <summary>
    /// Serializable idempotent wallet ledger entry.
    /// </summary>
    [Serializable]
    public sealed class WalletTransactionEntry
    {
        public string TransactionId;
        public CurrencyType Currency;
        public long Delta;
        public string Reason;
        public string SourceId;
        public long UnixTimestamp;
    }
}
