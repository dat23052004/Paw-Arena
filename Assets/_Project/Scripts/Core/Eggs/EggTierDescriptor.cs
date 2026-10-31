using System;
using System.Collections.Generic;

namespace PawArena.Core.Eggs
{
    /// <summary>Defines one selectable tier of an egg.</summary>
    public sealed class EggTierDescriptor
    {
        private readonly EggHatchOptionDescriptor[] _hatchOptions;
        private readonly EggRarityPoolDescriptor[] _rarityPools;

        public EggTierDescriptor(
            string tierId,
            string displayName,
            int order,
            int requiredPreviousTierHatches,
            IReadOnlyList<EggHatchOptionDescriptor> hatchOptions,
            IReadOnlyList<EggRarityPoolDescriptor> rarityPools)
        {
            if (string.IsNullOrWhiteSpace(tierId))
            {
                throw new ArgumentException("Tier ID is required.", nameof(tierId));
            }

            if (order <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(order));
            }

            if (requiredPreviousTierHatches < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredPreviousTierHatches));
            }

            if (hatchOptions == null || hatchOptions.Count == 0)
            {
                throw new ArgumentException("A tier needs hatch options.", nameof(hatchOptions));
            }

            if (rarityPools == null || rarityPools.Count == 0)
            {
                throw new ArgumentException("A tier needs rarity pools.", nameof(rarityPools));
            }

            var chanceTotal = 0;
            _rarityPools = new EggRarityPoolDescriptor[rarityPools.Count];
            for (var i = 0; i < rarityPools.Count; i++)
            {
                _rarityPools[i] = rarityPools[i]
                    ?? throw new ArgumentException("Rarity pools cannot contain null.", nameof(rarityPools));
                chanceTotal += rarityPools[i].ChanceBasisPoints;
            }

            if (chanceTotal != 10000)
            {
                throw new ArgumentException("Rarity chances must total exactly 10000 basis points.", nameof(rarityPools));
            }

            _hatchOptions = new EggHatchOptionDescriptor[hatchOptions.Count];
            for (var i = 0; i < hatchOptions.Count; i++)
            {
                _hatchOptions[i] = hatchOptions[i]
                    ?? throw new ArgumentException("Hatch options cannot contain null.", nameof(hatchOptions));
            }

            TierId = tierId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"Tier {order}" : displayName;
            Order = order;
            RequiredPreviousTierHatches = requiredPreviousTierHatches;
        }

        public string TierId { get; }

        public string DisplayName { get; }

        public int Order { get; }

        public int RequiredPreviousTierHatches { get; }

        public IReadOnlyList<EggHatchOptionDescriptor> HatchOptions => _hatchOptions;

        public IReadOnlyList<EggRarityPoolDescriptor> RarityPools => _rarityPools;

        public bool TryGetHatchOption(string optionId, out EggHatchOptionDescriptor option)
        {
            for (var i = 0; i < _hatchOptions.Length; i++)
            {
                if (string.Equals(_hatchOptions[i].OptionId, optionId, StringComparison.Ordinal))
                {
                    option = _hatchOptions[i];
                    return true;
                }
            }

            option = null;
            return false;
        }
    }
}
