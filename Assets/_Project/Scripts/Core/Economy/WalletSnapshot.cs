using System;
using System.Collections.Generic;

namespace PawArena.Core.Economy
{
    /// <summary>
    /// Serializable wallet state used by profile persistence.
    /// </summary>
    [Serializable]
    public sealed class WalletSnapshot
    {
        public List<WalletBalanceEntry> Balances = new();
        public List<WalletTransactionEntry> Ledger = new();
    }
}
