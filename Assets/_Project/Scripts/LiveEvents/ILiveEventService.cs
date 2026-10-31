using System;
using System.Collections.Generic;
using PawArena.Core.Economy;

namespace PawArena.LiveEvents
{
    /// <summary>Owns seasonal progress, claims, purchases, rotations and wheel outcomes.</summary>
    public interface ILiveEventService : IDisposable
    {
        event Action Changed;

        LiveEventDefinition Definition { get; }
        LiveEventWheelDefinition ActiveWheel { get; }
        int FreeSpinsRemaining { get; }
        long PaidSpinsRemaining { get; }
        int WheelPity { get; }
        bool IsActive { get; }
        bool IsHardQuestUnlocked { get; }
        int CurrentRewardIndex { get; }
        TimeSpan EventTimeRemaining { get; }
        TimeSpan DailyResetTimeRemaining { get; }
        TimeSpan NextFreeSpinTimeRemaining { get; }
        IReadOnlyList<LiveEventShopOfferDefinition> ActiveShopOffers { get; }
        TimeSpan ShopRotationTimeRemaining { get; }

        LiveEventQuestProgress GetQuestProgress(LiveEventQuestDefinition quest);
        bool IsRewardClaimed(string rewardId);
        LiveEventRewardState GetRewardState(string rewardId);
        TimeSpan GetRewardUnlockTimeRemaining(string rewardId);
        LiveEventClaimAllQuote GetClaimAllQuote(CurrencyType paymentCurrency);
        LiveEventWheelPackQuote GetWheelPackQuote(int spinCount);
        bool IsShopOfferPurchased(string offerId);
        bool IsBundlePurchased(string bundleId);
        bool IsAvailable(int startHourOffset, int endHourOffset);
        TimeSpan GetAvailabilityTimeRemaining(int endHourOffset);
        void ReportProgress(LiveEventQuestMetric metric, int amount = 1);
        LiveEventOperationResult TryClaimQuest(string questId);
        LiveEventOperationResult TryClaimReward(string rewardId);
        LiveEventOperationResult TryClaimReward(string rewardId, CurrencyType paymentCurrency);
        LiveEventOperationResult TryClaimAllRewards(CurrencyType paymentCurrency);
        LiveEventOperationResult TryPurchaseShopOffer(string offerId);
        LiveEventOperationResult TryPurchaseWheelPack(int spinCount);
        LiveEventSpinResult TrySpin();
        LiveEventSpinResult TrySpin(bool useFreeSpin);
        LiveEventOperationResult TryPurchaseBundle(string bundleId);
        void Save();
    }
}
