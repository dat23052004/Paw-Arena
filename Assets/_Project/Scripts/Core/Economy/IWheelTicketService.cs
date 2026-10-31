using System;

namespace PawArena.Core.Economy
{
    /// <summary>Owns paid wheel tickets independently from unique-item inventory ownership.</summary>
    public interface IWheelTicketService
    {
        /// <summary>Raised after the paid ticket balance changes.</summary>
        event Action<long> BalanceChanged;

        /// <summary>Gets the current paid spin-ticket balance.</summary>
        long Balance { get; }

        /// <summary>Applies an idempotent signed balance delta.</summary>
        WheelTicketTransactionResult TryApply(
            string transactionId,
            long delta,
            out long balance);

        /// <summary>Returns whether the Buy 1 discount is still available for an Event day.</summary>
        bool IsBuyOneDailyDealAvailable(string eventId, string resetKey);

        /// <summary>Marks the Buy 1 discount used after a successful player-profile transaction.</summary>
        void MarkBuyOneDailyDealUsed(string eventId, string resetKey);

        /// <summary>Captures serializable ticket state.</summary>
        WheelTicketSnapshot CaptureSnapshot();
    }
}
