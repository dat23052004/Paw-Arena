using System;

namespace PawArena.Core.Economy
{
    /// <summary>
    /// Owns currency balances and idempotent ledger operations.
    /// </summary>
    public interface IWalletService
    {
        /// <summary>
        /// Raised after a currency balance changes.
        /// </summary>
        event Action<CurrencyType, long> BalanceChanged;

        /// <summary>
        /// Raised after a transaction is accepted.
        /// </summary>
        event Action<WalletTransaction> TransactionApplied;

        /// <summary>
        /// Gets the current balance for a currency.
        /// </summary>
        long GetBalance(CurrencyType currency);

        /// <summary>
        /// Applies a signed transaction exactly once.
        /// </summary>
        WalletTransactionResult TryApply(
            string transactionId,
            CurrencyType currency,
            long delta,
            string reason,
            string sourceId,
            out WalletTransaction transaction);

        /// <summary>
        /// Captures serializable wallet state.
        /// </summary>
        WalletSnapshot CaptureSnapshot();
    }
}
