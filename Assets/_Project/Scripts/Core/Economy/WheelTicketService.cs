using System;
using System.Collections.Generic;

namespace PawArena.Core.Economy
{
    /// <summary>Maintains a non-expiring, player-scoped paid spin-ticket balance.</summary>
    public sealed class WheelTicketService : IWheelTicketService
    {
        private readonly HashSet<string> _appliedTransactionIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, WheelDailyDealState> _dailyDeals =
            new(StringComparer.Ordinal);
        private long _balance;

        /// <summary>Creates a ticket service from an optional persisted snapshot.</summary>
        public WheelTicketService(WheelTicketSnapshot snapshot = null)
        {
            RestoreSnapshot(snapshot);
        }

        /// <inheritdoc />
        public event Action<long> BalanceChanged;

        /// <inheritdoc />
        public long Balance => _balance;

        /// <inheritdoc />
        public WheelTicketTransactionResult TryApply(
            string transactionId,
            long delta,
            out long balance)
        {
            balance = _balance;
            if (string.IsNullOrWhiteSpace(transactionId) || delta == 0L)
            {
                return WheelTicketTransactionResult.InvalidTransaction;
            }

            if (_appliedTransactionIds.Contains(transactionId))
            {
                return WheelTicketTransactionResult.DuplicateTransaction;
            }

            long nextBalance;
            try
            {
                nextBalance = checked(_balance + delta);
            }
            catch (OverflowException)
            {
                return WheelTicketTransactionResult.InvalidTransaction;
            }

            if (nextBalance < 0L)
            {
                return WheelTicketTransactionResult.InsufficientTickets;
            }

            _balance = nextBalance;
            _appliedTransactionIds.Add(transactionId);
            balance = _balance;
            BalanceChanged?.Invoke(_balance);
            return WheelTicketTransactionResult.Success;
        }

        /// <inheritdoc />
        public bool IsBuyOneDailyDealAvailable(string eventId, string resetKey)
        {
            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(resetKey))
            {
                return false;
            }

            return !_dailyDeals.TryGetValue(eventId, out var state) ||
                   !string.Equals(state.ResetKey, resetKey, StringComparison.Ordinal) ||
                   !state.Used;
        }

        /// <inheritdoc />
        public void MarkBuyOneDailyDealUsed(string eventId, string resetKey)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new ArgumentException("Event ID is required.", nameof(eventId));
            }

            if (string.IsNullOrWhiteSpace(resetKey))
            {
                throw new ArgumentException("Reset key is required.", nameof(resetKey));
            }

            _dailyDeals[eventId] = new WheelDailyDealState
            {
                EventId = eventId,
                ResetKey = resetKey,
                Used = true
            };
        }

        /// <inheritdoc />
        public WheelTicketSnapshot CaptureSnapshot()
        {
            var snapshot = new WheelTicketSnapshot
            {
                PaidSpinTickets = _balance
            };
            foreach (var transactionId in _appliedTransactionIds)
            {
                snapshot.AppliedTransactionIds.Add(transactionId);
            }

            snapshot.AppliedTransactionIds.Sort(StringComparer.Ordinal);
            foreach (var state in _dailyDeals.Values)
            {
                snapshot.DailyDeals.Add(new WheelDailyDealState
                {
                    EventId = state.EventId,
                    ResetKey = state.ResetKey,
                    Used = state.Used
                });
            }

            snapshot.DailyDeals.Sort(
                (left, right) => string.CompareOrdinal(left.EventId, right.EventId));
            return snapshot;
        }

        private void RestoreSnapshot(WheelTicketSnapshot snapshot)
        {
            _balance = Math.Max(0L, snapshot?.PaidSpinTickets ?? 0L);
            if (snapshot?.AppliedTransactionIds != null)
            {
                for (var i = 0; i < snapshot.AppliedTransactionIds.Count; i++)
                {
                    var transactionId = snapshot.AppliedTransactionIds[i];
                    if (!string.IsNullOrWhiteSpace(transactionId))
                    {
                        _appliedTransactionIds.Add(transactionId);
                    }
                }
            }

            if (snapshot?.DailyDeals == null)
            {
                return;
            }

            for (var i = 0; i < snapshot.DailyDeals.Count; i++)
            {
                var state = snapshot.DailyDeals[i];
                if (state == null || string.IsNullOrWhiteSpace(state.EventId) ||
                    string.IsNullOrWhiteSpace(state.ResetKey))
                {
                    continue;
                }

                _dailyDeals[state.EventId] = new WheelDailyDealState
                {
                    EventId = state.EventId,
                    ResetKey = state.ResetKey,
                    Used = state.Used
                };
            }
        }
    }
}
