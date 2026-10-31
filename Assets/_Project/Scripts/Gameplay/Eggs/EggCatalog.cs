using System;
using System.Collections.Generic;
using System.Globalization;
using PawArena.Core.Economy;
using PawArena.Core.Eggs;
using PawArena.Core.Inventory;
using UnityEngine;

namespace PawArena.Gameplay.Eggs
{
    /// <summary>Designer-authored egg catalog. The sample catalog is used until this asset is assigned.</summary>
    [CreateAssetMenu(fileName = "EggCatalog", menuName = "Paw Arena/Eggs/Catalog")]
    public sealed class EggCatalog : ScriptableObject, IEggCatalog
    {
        [Serializable]
        private sealed class HatchOptionData
        {
            [SerializeField] private string _optionId;
            [SerializeField, Min(1)] private int _quantity = 1;
            [SerializeField] private CurrencyType _currency;
            [SerializeField, Min(1)] private long _price = 1L;

            public EggHatchOptionDescriptor Build()
            {
                return new EggHatchOptionDescriptor(_optionId, _quantity, _currency, _price);
            }
        }

        [Serializable]
        private sealed class RarityPoolData
        {
            [SerializeField] private ItemRarity _rarity;
            [SerializeField, Range(1, 10000)] private int _chanceBasisPoints = 10000;
            [SerializeField] private List<string> _itemIds = new();

            public EggRarityPoolDescriptor Build()
            {
                return new EggRarityPoolDescriptor(_rarity, _chanceBasisPoints, _itemIds);
            }
        }

        [Serializable]
        private sealed class TierData
        {
            [SerializeField] private string _tierId = "tier-1";
            [SerializeField] private string _displayName = "Tier 1";
            [SerializeField, Min(1)] private int _order = 1;
            [SerializeField, Min(0)] private int _requiredPreviousTierHatches;
            [SerializeField] private List<HatchOptionData> _hatchOptions = new();
            [SerializeField] private List<RarityPoolData> _rarityPools = new();

            public EggTierDescriptor Build()
            {
                var options = new List<EggHatchOptionDescriptor>(_hatchOptions.Count);
                for (var i = 0; i < _hatchOptions.Count; i++)
                {
                    options.Add(_hatchOptions[i].Build());
                }

                var pools = new List<EggRarityPoolDescriptor>(_rarityPools.Count);
                for (var i = 0; i < _rarityPools.Count; i++)
                {
                    pools.Add(_rarityPools[i].Build());
                }

                return new EggTierDescriptor(
                    _tierId,
                    _displayName,
                    _order,
                    _requiredPreviousTierHatches,
                    options,
                    pools);
            }
        }

        [Serializable]
        private sealed class EggData
        {
            [SerializeField] private string _eggId;
            [SerializeField] private string _displayName;
            [SerializeField] private ItemCategory _rewardCategory = ItemCategory.Fur;
            [SerializeField] private bool _isLimitedTime;
            [SerializeField] private string _startsAtUtcIso8601;
            [SerializeField] private string _endsAtUtcIso8601;
            [SerializeField, Min(0)] private int _duplicateRefundMultiplierBasisPoints = 10000;
            [SerializeField] private List<TierData> _tiers = new();

            public EggDescriptor Build()
            {
                var tiers = new List<EggTierDescriptor>(_tiers.Count);
                for (var i = 0; i < _tiers.Count; i++)
                {
                    tiers.Add(_tiers[i].Build());
                }

                return new EggDescriptor(
                    _eggId,
                    _displayName,
                    _rewardCategory,
                    _isLimitedTime,
                    ParseOptionalDate(_startsAtUtcIso8601),
                    ParseOptionalDate(_endsAtUtcIso8601),
                    _duplicateRefundMultiplierBasisPoints,
                    tiers);
            }

            private static DateTimeOffset? ParseOptionalDate(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                if (!DateTimeOffset.TryParse(
                        value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var parsed))
                {
                    throw new FormatException($"Invalid ISO-8601 UTC date '{value}'.");
                }

                return parsed;
            }
        }

        [SerializeField, Min(1)] private int _catalogVersion = 1;
        [SerializeField] private List<EggData> _eggs = new();

        private readonly List<EggDescriptor> _runtimeEggs = new();
        private readonly Dictionary<string, EggDescriptor> _eggById = new(StringComparer.Ordinal);

        public int CatalogVersion => _catalogVersion;

        public IReadOnlyList<EggDescriptor> Eggs
        {
            get
            {
                EnsureCache();
                return _runtimeEggs;
            }
        }

        public bool TryGet(string eggId, out EggDescriptor egg)
        {
            EnsureCache();
            egg = null;
            return !string.IsNullOrWhiteSpace(eggId) && _eggById.TryGetValue(eggId, out egg);
        }

        private void OnEnable() => RebuildCache(true);

        private void OnValidate()
        {
            _catalogVersion = Mathf.Max(1, _catalogVersion);
            RebuildCache(true);
        }

        private void EnsureCache()
        {
            if (_runtimeEggs.Count == 0 && _eggs.Count > 0)
            {
                RebuildCache(false);
            }
        }

        private void RebuildCache(bool logErrors)
        {
            _runtimeEggs.Clear();
            _eggById.Clear();

            for (var i = 0; i < _eggs.Count; i++)
            {
                try
                {
                    var egg = _eggs[i].Build();
                    if (!_eggById.TryAdd(egg.EggId, egg))
                    {
                        throw new InvalidOperationException($"Duplicate egg ID '{egg.EggId}'.");
                    }

                    _runtimeEggs.Add(egg);
                }
                catch (Exception exception)
                {
                    if (logErrors)
                    {
                        Debug.LogError($"Invalid egg definition at index {i}: {exception.Message}", this);
                    }
                }
            }
        }
    }
}
