using System;
using System.Collections.Generic;
using PawArena.Core.Time;

namespace PawArena.Core.Economy
{
    /// <summary>
    /// Provides an in-memory wallet with persisted idempotency history.
    /// </summary>
    public sealed class WalletService : IWalletService
    {
        private readonly Dictionary<CurrencyType, long> _balances = new();
        private readonly HashSet<string> _processedTransactionIds = new(StringComparer.Ordinal);
        private readonly List<WalletTransaction> _ledger = new();
        private readonly IClock _clock;

        /// <summary>
        /// Creates a wallet from an optional persisted snapshot.
        /// </summary>
        public WalletService(IClock clock, WalletSnapshot snapshot = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            foreach (CurrencyType currency in Enum.GetValues(typeof(CurrencyType)))
            {
                _balances[currency] = 0L;
            }

            RestoreSnapshot(snapshot);
        }

        /// <inheritdoc />
        public event Action<CurrencyType, long> BalanceChanged;

        /// <inheritdoc />
        public event Action<WalletTransaction> TransactionApplied;

        /// <inheritdoc />
        public long GetBalance(CurrencyType currency) => _balances[currency];

        /// <inheritdoc />
        public WalletTransactionResult TryApply(
            string transactionId,
            CurrencyType currency,
            long delta,
            string reason,
            string sourceId,
            out WalletTransaction transaction)
        {
            transaction = null;
            if (string.IsNullOrWhiteSpace(transactionId) ||
                string.IsNullOrWhiteSpace(reason) ||
                delta == 0L)
            {
                return WalletTransactionResult.InvalidTransaction;
            }

            if (_processedTransactionIds.Contains(transactionId))
            {
                return WalletTransactionResult.DuplicateTransaction;
            }

            long nextBalance;
            try
            {
                nextBalance = checked(_balances[currency] + delta);
            }
            catch (OverflowException)
            {
                return WalletTransactionResult.InvalidTransaction;
            }

            if (nextBalance < 0L)
            {
                return WalletTransactionResult.InsufficientFunds;
            }

            transaction = new WalletTransaction(
                transactionId,
                currency,
                delta,
                reason,
                sourceId ?? string.Empty,
                _clock.UtcNow);

            _processedTransactionIds.Add(transactionId);
            _ledger.Add(transaction);
            _balances[currency] = nextBalance;
            BalanceChanged?.Invoke(currency, nextBalance);
            TransactionApplied?.Invoke(transaction);
            return WalletTransactionResult.Success;
        }

        /// <inheritdoc />
        public WalletSnapshot CaptureSnapshot()
        {
            var snapshot = new WalletSnapshot();
            foreach (CurrencyType currency in Enum.GetValues(typeof(CurrencyType)))
            {
                snapshot.Balances.Add(new WalletBalanceEntry
                {
                    Currency = currency,
                    Balance = _balances[currency]
                });
            }

            for (var i = 0; i < _ledger.Count; i++)
            {
                var transaction = _ledger[i];
                snapshot.Ledger.Add(new WalletTransactionEntry
                {
                    TransactionId = transaction.TransactionId,
                    Currency = transaction.Currency,
                    Delta = transaction.Delta,
                    Reason = transaction.Reason,
                    SourceId = transaction.SourceId,
                    UnixTimestamp = transaction.Timestamp.ToUnixTimeSeconds()
                });
            }

            return snapshot;
        }

        private void RestoreSnapshot(WalletSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (snapshot.Balances != null)
            {
                for (var i = 0; i < snapshot.Balances.Count; i++)
                {
                    var entry = snapshot.Balances[i];
                    if (entry != null && entry.Balance >= 0L)
                    {
                        _balances[entry.Currency] = entry.Balance;
                    }
                }
            }

            if (snapshot.Ledger == null)
            {
                return;
            }

            for (var i = 0; i < snapshot.Ledger.Count; i++)
            {
                var entry = snapshot.Ledger[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.TransactionId) ||
                    !_processedTransactionIds.Add(entry.TransactionId))
                {
                    continue;
                }

                _ledger.Add(new WalletTransaction(
                    entry.TransactionId,
                    entry.Currency,
                    entry.Delta,
                    entry.Reason ?? string.Empty,
                    entry.SourceId ?? string.Empty,
                    DateTimeOffset.FromUnixTimeSeconds(entry.UnixTimestamp)));
            }
        }
    }
}
