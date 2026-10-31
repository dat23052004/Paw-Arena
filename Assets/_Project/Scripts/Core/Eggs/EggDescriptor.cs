using System;
using System.Collections.Generic;
using PawArena.Core.Inventory;

namespace PawArena.Core.Eggs
{
    /// <summary>Immutable runtime definition shared by every egg UI and hatch flow.</summary>
    public sealed class EggDescriptor
    {
        private readonly EggTierDescriptor[] _tiers;

        public EggDescriptor(
            string eggId,
            string displayName,
            ItemCategory rewardCategory,
            bool isLimitedTime,
            DateTimeOffset? startsAtUtc,
            DateTimeOffset? endsAtUtc,
            int duplicateRefundMultiplierBasisPoints,
            IReadOnlyList<EggTierDescriptor> tiers)
        {
            if (string.IsNullOrWhiteSpace(eggId))
            {
                throw new ArgumentException("Egg ID is required.", nameof(eggId));
            }

            if (duplicateRefundMultiplierBasisPoints < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(duplicateRefundMultiplierBasisPoints));
            }

            if (isLimitedTime && (!endsAtUtc.HasValue || endsAtUtc <= startsAtUtc))
            {
                throw new ArgumentException("Limited eggs require a valid end time.", nameof(endsAtUtc));
            }

            if (tiers == null || tiers.Count == 0)
            {
                throw new ArgumentException("An egg needs at least one tier.", nameof(tiers));
            }

            _tiers = new EggTierDescriptor[tiers.Count];
            for (var i = 0; i < tiers.Count; i++)
            {
                _tiers[i] = tiers[i]
                    ?? throw new ArgumentException("Tiers cannot contain null.", nameof(tiers));
            }

            Array.Sort(_tiers, (left, right) => left.Order.CompareTo(right.Order));
            if (_tiers[0].Order != 1 || _tiers[0].RequiredPreviousTierHatches != 0)
            {
                throw new ArgumentException("The first tier must be order 1 and unlocked by default.", nameof(tiers));
            }

            for (var i = 1; i < _tiers.Length; i++)
            {
                if (_tiers[i].Order != _tiers[i - 1].Order + 1)
                {
                    throw new ArgumentException("Tier order must be contiguous.", nameof(tiers));
                }
            }

            EggId = eggId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? eggId : displayName;
            RewardCategory = rewardCategory;
            IsLimitedTime = isLimitedTime;
            StartsAtUtc = startsAtUtc;
            EndsAtUtc = endsAtUtc;
            DuplicateRefundMultiplierBasisPoints = duplicateRefundMultiplierBasisPoints;
        }

        public string EggId { get; }

        public string DisplayName { get; }

        public ItemCategory RewardCategory { get; }

        public bool IsLimitedTime { get; }

        public DateTimeOffset? StartsAtUtc { get; }

        public DateTimeOffset? EndsAtUtc { get; }

        public int DuplicateRefundMultiplierBasisPoints { get; }

        public IReadOnlyList<EggTierDescriptor> Tiers => _tiers;

        public bool IsAvailable(DateTimeOffset utcNow)
        {
            if (StartsAtUtc.HasValue && utcNow < StartsAtUtc.Value)
            {
                return false;
            }

            return !EndsAtUtc.HasValue || utcNow < EndsAtUtc.Value;
        }

        public bool TryGetTier(string tierId, out EggTierDescriptor tier)
        {
            for (var i = 0; i < _tiers.Length; i++)
            {
                if (string.Equals(_tiers[i].TierId, tierId, StringComparison.Ordinal))
                {
                    tier = _tiers[i];
                    return true;
                }
            }

            tier = null;
            return false;
        }
    }
}
