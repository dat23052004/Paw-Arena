using System;
using System.Collections.Generic;
using PawArena.Core.Economy;
using UnityEngine;

namespace PawArena.UI.Shop
{
    /// <summary>Stores presentation and pricing data consumed by the main Shop screen.</summary>
    [CreateAssetMenu(fileName = "ShopCatalog", menuName = "Paw Arena/UI/Shop Catalog")]
    public sealed class ShopCatalog : ScriptableObject
    {
        [SerializeField] private List<EggOffer> _eggs = new();
        [SerializeField] private List<BundleOffer> _bundles = new();
        [SerializeField] private List<CoinPackOffer> _coinPacks = new();

        /// <summary>Gets egg offers in display order.</summary>
        public IReadOnlyList<EggOffer> Eggs => _eggs;

        /// <summary>Gets bundle offers in display order.</summary>
        public IReadOnlyList<BundleOffer> Bundles => _bundles;

        /// <summary>Gets coin packs in display order.</summary>
        public IReadOnlyList<CoinPackOffer> CoinPacks => _coinPacks;

        /// <summary>Describes one reward preview shown inside an offer card.</summary>
        [Serializable]
        public sealed class RewardDefinition
        {
            [SerializeField] private string _title = string.Empty;
            [SerializeField] private string _subtitle = string.Empty;
            [SerializeField] private Sprite _icon = null;
            [SerializeField, Range(0f, 100f)] private float _chancePercent;
            [SerializeField] private bool _locked;

            public string Title => _title;
            public string Subtitle => _subtitle;
            public Sprite Icon => _icon;
            public float ChancePercent => _chancePercent;
            public bool Locked => _locked;
        }

        /// <summary>Describes an egg and its supported purchase packs.</summary>
        [Serializable]
        public sealed class EggOffer
        {
            [SerializeField] private string _id = string.Empty;
            [SerializeField] private string _title = string.Empty;
            [SerializeField] private string _subtitle = string.Empty;
            [SerializeField] private Sprite _artwork = null;
            [SerializeField] private bool _limitedTime;
            [Tooltip("ISO-8601 UTC timestamp. Leave empty when the offer has no countdown.")]
            [SerializeField] private string _endsAtUtc = string.Empty;
            [SerializeField] private CurrencyType _hatchCurrency = CurrencyType.Coins;
            [SerializeField, Min(0)] private long _hatchPrice;
            [SerializeField] private CurrencyType _packCurrency = CurrencyType.Premium;
            [SerializeField, Min(0)] private long _buyOnePrice;
            [SerializeField, Min(0)] private long _buyThreePrice;
            [SerializeField, Min(0)] private long _buyTenPrice;
            [SerializeField] private bool _showTiers;
            [SerializeField] private List<RewardDefinition> _rewards = new();

            public string Id => _id;
            public string Title => _title;
            public string Subtitle => _subtitle;
            public Sprite Artwork => _artwork;
            public bool LimitedTime => _limitedTime;
            public string EndsAtUtc => _endsAtUtc;
            public CurrencyType HatchCurrency => _hatchCurrency;
            public long HatchPrice => _hatchPrice;
            public CurrencyType PackCurrency => _packCurrency;
            public long BuyOnePrice => _buyOnePrice;
            public long BuyThreePrice => _buyThreePrice;
            public long BuyTenPrice => _buyTenPrice;
            public bool ShowTiers => _showTiers;
            public IReadOnlyList<RewardDefinition> Rewards => _rewards;
        }

        /// <summary>Describes a standard or special bundle.</summary>
        [Serializable]
        public sealed class BundleOffer
        {
            [SerializeField] private string _id = string.Empty;
            [SerializeField] private string _title = string.Empty;
            [SerializeField] private string _subtitle = string.Empty;
            [SerializeField] private Sprite _artwork = null;
            [SerializeField] private bool _soldOut;
            [SerializeField] private bool _specialPack;
            [Tooltip("ISO-8601 UTC timestamp. Leave empty when the offer has no countdown.")]
            [SerializeField] private string _endsAtUtc = string.Empty;
            [SerializeField] private CurrencyType _currency = CurrencyType.Premium;
            [SerializeField, Min(0)] private long _originalPrice;
            [SerializeField, Min(0)] private long _price;
            [SerializeField, Range(0, 100)] private int _discountPercent;
            [SerializeField] private List<RewardDefinition> _rewards = new();

            public string Id => _id;
            public string Title => _title;
            public string Subtitle => _subtitle;
            public Sprite Artwork => _artwork;
            public bool SoldOut => _soldOut;
            public bool SpecialPack => _specialPack;
            public string EndsAtUtc => _endsAtUtc;
            public CurrencyType Currency => _currency;
            public long OriginalPrice => _originalPrice;
            public long Price => _price;
            public int DiscountPercent => _discountPercent;
            public IReadOnlyList<RewardDefinition> Rewards => _rewards;
        }

        /// <summary>Describes a premium-currency coin pack.</summary>
        [Serializable]
        public sealed class CoinPackOffer
        {
            [SerializeField] private string _id = string.Empty;
            [SerializeField] private string _title = string.Empty;
            [SerializeField] private Sprite _artwork = null;
            [SerializeField, Min(0)] private long _coinAmount;
            [SerializeField, Min(0)] private long _premiumPrice;
            [SerializeField, Range(0, 100)] private int _bonusPercent;

            public string Id => _id;
            public string Title => _title;
            public Sprite Artwork => _artwork;
            public long CoinAmount => _coinAmount;
            public long PremiumPrice => _premiumPrice;
            public int BonusPercent => _bonusPercent;
        }
    }
}
