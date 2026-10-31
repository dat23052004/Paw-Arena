using System;
using PawArena.Core.Economy;

namespace PawArena.LiveEvents
{
    /// <summary>Identifies the gameplay counter advanced by a live-event quest.</summary>
    public enum LiveEventQuestMetric
    {
        PlayMatch,
        WinMatch,
        KnockOut,
        UseAbility,
        UseEmote,
        FinishObby,
        ThrowOpponent
    }

    /// <summary>Separates frequently resetting quests from long-form event quests.</summary>
    public enum LiveEventQuestGroup
    {
        Daily,
        Hard
    }

    /// <summary>Identifies the payload delivered by an event reward.</summary>
    public enum LiveEventGrantKind
    {
        Currency,
        Item
    }

    /// <summary>Controls the visual rarity treatment of a reward card.</summary>
    public enum LiveEventRewardTier
    {
        Common,
        Rare,
        Epic,
        Legendary,
        Mythic
    }

    /// <summary>Describes where a reward sits in the sequential reward track.</summary>
    public enum LiveEventRewardState
    {
        Owned,
        Current,
        Locked
    }

    /// <summary>Describes the result of a requested live-event action.</summary>
    public enum LiveEventOperationResult
    {
        Success,
        EventInactive,
        NotFound,
        Locked,
        NotCompleted,
        AlreadyClaimed,
        AlreadyPurchased,
        InsufficientFunds,
        InvalidConfiguration,
        NoSpinAvailable,
        PersistenceFailed
    }

    /// <summary>Identifies the balance consumed by one accepted Wheel spin.</summary>
    public enum LiveEventWheelSpinSource
    {
        None,
        Free,
        PaidTicket
    }

    /// <summary>Represents the current state of one configured quest.</summary>
    public readonly struct LiveEventQuestProgress
    {
        public LiveEventQuestProgress(int current, int target, bool claimed)
        {
            Current = Math.Max(0, current);
            Target = Math.Max(1, target);
            Claimed = claimed;
        }

        public int Current { get; }
        public int Target { get; }
        public bool Claimed { get; }
        public bool IsCompleted => Current >= Target;
    }

    /// <summary>Summarizes the contiguous, unlocked rewards eligible for a bulk claim.</summary>
    public readonly struct LiveEventClaimAllQuote
    {
        public LiveEventClaimAllQuote(CurrencyType currency, int rewardCount, long totalPrice)
        {
            Currency = currency;
            RewardCount = Math.Max(0, rewardCount);
            TotalPrice = Math.Max(0L, totalPrice);
        }

        public CurrencyType Currency { get; }
        public int RewardCount { get; }
        public long TotalPrice { get; }
        public bool HasRewards => RewardCount > 0;
    }

    /// <summary>Contains the outcome of one accepted wheel spin.</summary>
    public readonly struct LiveEventSpinResult
    {
        public LiveEventSpinResult(
            LiveEventOperationResult operationResult,
            LiveEventWheelPrizeDefinition prize)
            : this(operationResult, prize, LiveEventWheelSpinSource.None)
        {
        }

        public LiveEventSpinResult(
            LiveEventOperationResult operationResult,
            LiveEventWheelPrizeDefinition prize,
            LiveEventWheelSpinSource source)
        {
            OperationResult = operationResult;
            Prize = prize;
            Source = source;
        }

        public LiveEventOperationResult OperationResult { get; }
        public LiveEventWheelPrizeDefinition Prize { get; }
        public LiveEventWheelSpinSource Source { get; }
        public bool Succeeded => OperationResult == LiveEventOperationResult.Success;
    }

    /// <summary>Describes the current Premium quote for one paid Wheel pack.</summary>
    public readonly struct LiveEventWheelPackQuote
    {
        public LiveEventWheelPackQuote(int spinCount, long premiumPrice, bool usesDailyDeal)
        {
            SpinCount = Math.Max(0, spinCount);
            PremiumPrice = Math.Max(0L, premiumPrice);
            UsesDailyDeal = usesDailyDeal;
        }

        public int SpinCount { get; }
        public long PremiumPrice { get; }
        public bool UsesDailyDeal { get; }
        public bool IsValid => SpinCount == 1 || SpinCount == 3 || SpinCount == 10;
    }
}
