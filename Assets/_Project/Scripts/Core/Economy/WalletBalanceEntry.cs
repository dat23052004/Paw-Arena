using System;

namespace PawArena.Core.Economy
{
    /// <summary>
    /// Serializable currency balance entry.
    /// </summary>
    [Serializable]
    public sealed class WalletBalanceEntry
    {
        public CurrencyType Currency;
        public long Balance;
    }
}
