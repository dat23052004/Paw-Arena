using System;
using System.Collections.Generic;
using PawArena.Core.Economy;
using PawArena.Core.Inventory;
using PawArena.Core.Profiles;
using PawArena.Core.Time;

namespace PawArena.Core.Eggs
{
    /// <summary>
    /// Resolves rarity first, then an item inside that configured rarity group.
    /// Ownership is checked at hatch time, including earlier rewards in the same batch.
    /// </summary>
    public sealed class EggHatchService : IEggHatchService
    {
        private readonly IEggCatalog _eggCatalog;
        private readonly IItemCatalog _itemCatalog;
        private readonly IInventoryService _inventory;
        private readonly IWalletService _wallet;
        private readonly IEggProgressService _progress;
        private readonly IProfileService _profile;
        private readonly IClock _clock;
        private readonly IRandomSource _random;
        private readonly IEggRefundPolicy _refundPolicy;

        public EggHatchService(
            IEggCatalog eggCatalog,
            IItemCatalog itemCatalog,
            IInventoryService inventory,
            IWalletService wallet,
            IEggProgressService progress,
            IProfileService profile,
            IClock clock,
            IRandomSource random,
            IEggRefundPolicy refundPolicy)
        {
            _eggCatalog = eggCatalog ?? throw new ArgumentNullException(nameof(eggCatalog));
            _itemCatalog = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _refundPolicy = refundPolicy ?? throw new ArgumentNullException(nameof(refundPolicy));
        }

        public EggHatchResult TryHatch(EggHatchRequest request)
        {
            if (!_eggCatalog.TryGet(request.EggId, out var egg))
            {
                return EggHatchResult.Failure(EggHatchStatus.EggNotFound);
            }

            var utcNow = _clock.UtcNow;
            if (egg.StartsAtUtc.HasValue && utcNow < egg.StartsAtUtc.Value)
            {
                return EggHatchResult.Failure(EggHatchStatus.NotStarted);
            }

            if (egg.EndsAtUtc.HasValue && utcNow >= egg.EndsAtUtc.Value)
            {
                return EggHatchResult.Failure(EggHatchStatus.Expired);
            }

            if (!egg.TryGetTier(request.TierId, out var tier))
            {
                return EggHatchResult.Failure(EggHatchStatus.TierNotFound);
            }

            if (!_progress.IsTierUnlocked(egg, tier))
            {
                return EggHatchResult.Failure(EggHatchStatus.TierLocked);
            }

            if (!tier.TryGetHatchOption(request.HatchOptionId, out var option))
            {
                return EggHatchResult.Failure(EggHatchStatus.OptionNotFound);
            }

            if (!TryPlanRewards(egg, tier, option.Quantity, out var rewards, out var refundCoins))
            {
                return EggHatchResult.Failure(EggHatchStatus.InvalidConfiguration);
            }

            var chargeResult = _wallet.TryApply(
                $"{request.RequestId}:charge",
                option.Currency,
                -option.Price,
                "Egg hatch",
                egg.EggId,
                out _);

            if (chargeResult == WalletTransactionResult.InsufficientFunds)
            {
                return EggHatchResult.Failure(EggHatchStatus.InsufficientFunds);
            }

            if (chargeResult == WalletTransactionResult.DuplicateTransaction)
            {
                return EggHatchResult.Failure(EggHatchStatus.DuplicateRequest);
            }

            if (chargeResult != WalletTransactionResult.Success)
            {
                return EggHatchResult.Failure(EggHatchStatus.CommitFailed);
            }

            for (var i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].WasAlreadyOwned)
                {
                    continue;
                }

                var grantResult = _inventory.TryGrant(rewards[i].ItemId);
                if (grantResult != InventoryOperationResult.Success
                    && grantResult != InventoryOperationResult.AlreadyOwned)
                {
                    _wallet.TryApply(
                        $"{request.RequestId}:rollback",
                        option.Currency,
                        option.Price,
                        "Egg hatch rollback",
                        egg.EggId,
                        out _);
                    return EggHatchResult.Failure(EggHatchStatus.CommitFailed);
                }
            }

            if (refundCoins > 0L)
            {
                var refundResult = _wallet.TryApply(
                    $"{request.RequestId}:duplicates",
                    CurrencyType.Coins,
                    refundCoins,
                    "Duplicate egg rewards",
                    egg.EggId,
                    out _);

                if (refundResult != WalletTransactionResult.Success)
                {
                    return EggHatchResult.Failure(EggHatchStatus.CommitFailed);
                }
            }

            var unlockedTierId = _progress.RecordSuccessfulHatches(egg, tier, option.Quantity);
            var tierHatchCount = _progress.GetHatchCount(egg.EggId, tier.TierId);
            _profile.Save();
            return EggHatchResult.Succeeded(
                rewards,
                option.Currency,
                option.Price,
                refundCoins,
                tierHatchCount,
                unlockedTierId);
        }

        private bool TryPlanRewards(
            EggDescriptor egg,
            EggTierDescriptor tier,
            int quantity,
            out List<EggRewardResult> rewards,
            out long refundCoins)
        {
            rewards = new List<EggRewardResult>(quantity);
            refundCoins = 0L;
            var workingOwnership = new HashSet<string>(_inventory.OwnedItemIds, StringComparer.Ordinal);

            for (var i = 0; i < quantity; i++)
            {
                var rarityRoll = _random.NextInt(10000);
                var cumulative = 0;
                EggRarityPoolDescriptor selectedPool = null;
                for (var poolIndex = 0; poolIndex < tier.RarityPools.Count; poolIndex++)
                {
                    var pool = tier.RarityPools[poolIndex];
                    cumulative += pool.ChanceBasisPoints;
                    if (rarityRoll < cumulative)
                    {
                        selectedPool = pool;
                        break;
                    }
                }

                if (selectedPool == null)
                {
                    return false;
                }

                var itemId = selectedPool.ItemIds[_random.NextInt(selectedPool.ItemIds.Count)];
                if (!_itemCatalog.TryGet(itemId, out var item)
                    || item.Category != egg.RewardCategory
                    || item.Rarity != selectedPool.Rarity)
                {
                    return false;
                }

                var wasAlreadyOwned = !workingOwnership.Add(itemId);
                var itemRefund = wasAlreadyOwned
                    ? _refundPolicy.CalculateCoins(item.Rarity, egg.DuplicateRefundMultiplierBasisPoints)
                    : 0L;
                refundCoins = checked(refundCoins + itemRefund);
                rewards.Add(new EggRewardResult(itemId, item.Rarity, wasAlreadyOwned, itemRefund));
            }

            return true;
        }
    }
}
