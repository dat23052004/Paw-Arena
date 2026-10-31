using System;
using System.Collections.Generic;
using PawArena.Core.Economy;

namespace PawArena.Core.Eggs
{
    public enum EggHatchStatus
    {
        Success,
        EggNotFound,
        TierNotFound,
        TierLocked,
        OptionNotFound,
        NotStarted,
        Expired,
        InvalidConfiguration,
        InsufficientFunds,
        DuplicateRequest,
        CommitFailed
    }

    /// <summary>Complete outcome consumed by the reveal UI.</summary>
    public sealed class EggHatchResult
    {
        private static readonly EggRewardResult[] EmptyRewards = Array.Empty<EggRewardResult>();

        private EggHatchResult(
            EggHatchStatus status,
            IReadOnlyList<EggRewardResult> rewards,
            CurrencyType paidCurrency,
            long paidAmount,
            long totalRefundCoins,
            int tierHatchCount,
            string unlockedTierId)
        {
            Status = status;
            Rewards = rewards ?? EmptyRewards;
            PaidCurrency = paidCurrency;
            PaidAmount = paidAmount;
            TotalRefundCoins = totalRefundCoins;
            TierHatchCount = tierHatchCount;
            UnlockedTierId = unlockedTierId;
        }

        public EggHatchStatus Status { get; }

        public bool IsSuccess => Status == EggHatchStatus.Success;

        public IReadOnlyList<EggRewardResult> Rewards { get; }

        public CurrencyType PaidCurrency { get; }

        public long PaidAmount { get; }

        public long TotalRefundCoins { get; }

        public int TierHatchCount { get; }

        public string UnlockedTierId { get; }

        public static EggHatchResult Failure(EggHatchStatus status)
        {
            return new EggHatchResult(status, EmptyRewards, CurrencyType.Coins, 0L, 0L, 0, null);
        }

        public static EggHatchResult Succeeded(
            IReadOnlyList<EggRewardResult> rewards,
            CurrencyType paidCurrency,
            long paidAmount,
            long totalRefundCoins,
            int tierHatchCount,
            string unlockedTierId)
        {
            return new EggHatchResult(
                EggHatchStatus.Success,
                rewards,
                paidCurrency,
                paidAmount,
                totalRefundCoins,
                tierHatchCount,
                unlockedTierId);
        }
    }
}
