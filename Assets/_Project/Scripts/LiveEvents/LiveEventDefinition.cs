using System;
using System.Collections.Generic;
using System.Globalization;
using PawArena.Core.Economy;
using UnityEngine;
using UnityEngine.Serialization;

namespace PawArena.LiveEvents
{
    /// <summary>
    /// Stores every seasonal presentation and economy choice in one replaceable asset.
    /// </summary>
    [CreateAssetMenu(fileName = "LiveEvent", menuName = "Paw Arena/Live Event Definition")]
    public sealed class LiveEventDefinition : ScriptableObject
    {
        [Header("Identity and schedule")]
        [SerializeField] private string _eventId = "halloween-2026";
        [SerializeField] private string _displayName = "Halloween Event";
        [SerializeField] private string _startsAtUtc = "2026-10-01T00:00:00Z";
        [SerializeField] private string _endsAtUtc = "2026-11-08T00:00:00Z";
        [SerializeField, Range(0, 23)] private int _dailyResetHourUtc;

        [Header("Theme")]
        [SerializeField] private Sprite _banner = null;
        [SerializeField] private Sprite _currencyIcon = null;
        [SerializeField] private Color _primaryColor = new(0.29f, 0.08f, 0.48f, 1f);
        [SerializeField] private Color _accentColor = new(1f, 0.48f, 0.08f, 1f);

        [Header("Content")]
        [SerializeField] private List<LiveEventQuestDefinition> _dailyQuests = new();
        [SerializeField] private List<LiveEventQuestDefinition> _hardQuests = new();
        [SerializeField] private List<LiveEventRewardWeekDefinition> _rewardWeeks = new();
        [SerializeField] private List<LiveEventRewardDefinition> _rewards = new();
        [SerializeField] private List<LiveEventShopPeriodDefinition> _shopPeriods = new();
        [SerializeField, HideInInspector] private List<LiveEventShopOfferDefinition> _shopOffers = new();
        [SerializeField] private List<LiveEventWheelDefinition> _wheels = new();
        [FormerlySerializedAs("_wheel")]
        [SerializeField, HideInInspector] private LiveEventWheelDefinition _legacyWheel = new();
        [SerializeField] private List<LiveEventBundleDefinition> _bundles = new();

        [NonSerialized] private List<LiveEventShopOfferDefinition> _shopOfferCache;
        [NonSerialized] private LiveEventWheelDefinition[] _legacyWheelCache;

        public string EventId => _eventId;
        public string DisplayName => _displayName;
        public int DailyResetHourUtc => _dailyResetHourUtc;
        public Sprite Banner => _banner;
        public Sprite CurrencyIcon => _currencyIcon;
        public Color PrimaryColor => _primaryColor;
        public Color AccentColor => _accentColor;
        public IReadOnlyList<LiveEventQuestDefinition> DailyQuests => _dailyQuests;
        public IReadOnlyList<LiveEventQuestDefinition> HardQuests => _hardQuests;
        public IReadOnlyList<LiveEventRewardWeekDefinition> RewardWeeks => _rewardWeeks;
        public IReadOnlyList<LiveEventRewardDefinition> Rewards => _rewards;
        public IReadOnlyList<LiveEventShopPeriodDefinition> ShopPeriods => _shopPeriods;
        public IReadOnlyList<LiveEventShopOfferDefinition> ShopOffers
        {
            get
            {
                if (_shopPeriods == null || _shopPeriods.Count == 0)
                {
                    return _shopOffers;
                }

                _shopOfferCache ??= BuildShopOfferCache();
                return _shopOfferCache;
            }
        }
        public IReadOnlyList<LiveEventWheelDefinition> Wheels =>
            _wheels != null && _wheels.Count > 0
                ? _wheels
                : LegacyWheelList;

        /// <summary>Returns the first authored Wheel for migration and editor compatibility.</summary>
        [Obsolete("Use Wheels or ILiveEventService.ActiveWheel instead.")]
        public LiveEventWheelDefinition Wheel => Wheels.Count > 0 ? Wheels[0] : null;
        public IReadOnlyList<LiveEventBundleDefinition> Bundles => _bundles;

        private IReadOnlyList<LiveEventWheelDefinition> LegacyWheelList =>
            _legacyWheelCache ??= _legacyWheel != null
                ? new[] { _legacyWheel }
                : Array.Empty<LiveEventWheelDefinition>();

        /// <summary>Parses the authoring strings as a start-inclusive, end-exclusive UTC schedule.</summary>
        public bool TryGetSchedule(out DateTimeOffset startsAt, out DateTimeOffset endsAt)
        {
            var styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
            var hasStart = DateTimeOffset.TryParse(
                _startsAtUtc,
                CultureInfo.InvariantCulture,
                styles,
                out startsAt);
            var hasEnd = DateTimeOffset.TryParse(
                _endsAtUtc,
                CultureInfo.InvariantCulture,
                styles,
                out endsAt);
            return hasStart && hasEnd &&
                   endsAt > startsAt;
        }

        private void OnEnable()
        {
            _shopOfferCache = null;
            _legacyWheelCache = null;
        }

        private void OnValidate()
        {
            _shopOfferCache = null;
            _legacyWheelCache = null;
        }

        private List<LiveEventShopOfferDefinition> BuildShopOfferCache()
        {
            var result = new List<LiveEventShopOfferDefinition>();
            for (var periodIndex = 0; periodIndex < _shopPeriods.Count; periodIndex++)
            {
                var period = _shopPeriods[periodIndex];
                if (period == null)
                {
                    continue;
                }

                for (var offerIndex = 0; offerIndex < period.Offers.Count; offerIndex++)
                {
                    var offer = period.Offers[offerIndex];
                    if (offer != null)
                    {
                        result.Add(offer);
                    }
                }
            }

            return result;
        }
    }

    /// <summary>Groups a contiguous section of rewards under one themed event week.</summary>
    [Serializable]
    public sealed class LiveEventRewardWeekDefinition
    {
        [SerializeField] private string _weekId = string.Empty;
        [SerializeField] private string _title = string.Empty;
        [SerializeField, Min(0)] private int _startHourOffset;
        [SerializeField, Min(1)] private int _endHourOffset = 168;
        [SerializeField] private Color _backgroundColor = new(0.08f, 0.28f, 0.18f, 1f);
        [SerializeField] private Color _trackColor = new(0.42f, 0.85f, 0.12f, 1f);

        public string WeekId => _weekId;
        public string Title => _title;
        public int StartHourOffset => Math.Max(0, _startHourOffset);
        public int EndHourOffset => Math.Max(StartHourOffset + 1, _endHourOffset);
        public Color BackgroundColor => _backgroundColor;
        public Color TrackColor => _trackColor;
    }

    [Serializable]
    public sealed class LiveEventQuestDefinition
    {
        [SerializeField] private string _questId = string.Empty;
        [SerializeField] private string _title = string.Empty;
        [SerializeField] private LiveEventQuestMetric _metric;
        [SerializeField, Min(1)] private int _target = 1;
        [SerializeField, Min(0)] private long _candyReward = 10L;

        public string QuestId => _questId;
        public string Title => _title;
        public LiveEventQuestMetric Metric => _metric;
        public int Target => Math.Max(1, _target);
        public long CandyReward => Math.Max(0L, _candyReward);
    }

    [Serializable]
    public sealed class LiveEventGrantDefinition
    {
        [SerializeField] private LiveEventGrantKind _kind;
        [SerializeField] private CurrencyType _currency;
        [SerializeField] private string _itemId = string.Empty;
        [SerializeField, Min(1)] private long _amount = 1L;

        public LiveEventGrantKind Kind => _kind;
        public CurrencyType Currency => _currency;
        public string ItemId => _itemId;
        public long Amount => Math.Max(1L, _amount);
    }

    [Serializable]
    public sealed class LiveEventRewardDefinition
    {
        [SerializeField] private string _rewardId = string.Empty;
        [SerializeField] private string _weekId = string.Empty;
        [SerializeField] private LiveEventRewardTier _tier;
        [SerializeField] private Color _cardColor = new(0.22f, 0.43f, 0.78f, 1f);
        [SerializeField, Min(1)] private long _candyPrice = 1L;
        [SerializeField, Min(1)] private long _premiumPrice = 1L;
        [SerializeField, Min(0)] private int _unlockHourOffset;
        [SerializeField] private string _title = string.Empty;
        [SerializeField] private string _subtitle = string.Empty;
        [SerializeField] private Sprite _icon = null;
        [SerializeField] private LiveEventGrantDefinition _grant = new();

        public string RewardId => _rewardId;
        public string WeekId => _weekId;
        public LiveEventRewardTier Tier => _tier;
        public Color CardColor => _cardColor;
        public long CandyPrice => Math.Max(1L, _candyPrice);
        public long PremiumPrice => Math.Max(1L, _premiumPrice);
        public int UnlockHourOffset => Math.Max(0, _unlockHourOffset);
        public string Title => _title;
        public string Subtitle => _subtitle;
        public Sprite Icon => _icon;
        public LiveEventGrantDefinition Grant => _grant;
    }

    [Serializable]
    public sealed class LiveEventShopPeriodDefinition
    {
        [SerializeField] private string _periodId = string.Empty;
        [SerializeField, Min(1)] private int _endHourOffset = 24;
        [SerializeField] private List<LiveEventShopOfferDefinition> _offers = new();

        public string PeriodId => _periodId;
        public int EndHourOffset => Math.Max(1, _endHourOffset);
        public IReadOnlyList<LiveEventShopOfferDefinition> Offers => _offers;
    }

    [Serializable]
    public sealed class LiveEventShopOfferDefinition
    {
        [SerializeField] private string _offerId = string.Empty;
        [SerializeField] private string _title = string.Empty;
        [SerializeField] private string _rarity = string.Empty;
        [SerializeField] private Sprite _icon = null;
        [SerializeField] private string _itemId = string.Empty;
        [SerializeField] private CurrencyType _priceCurrency = CurrencyType.Coins;
        [SerializeField, Min(0)] private long _price;
        [SerializeField, Min(0)] private int _startHourOffset;
        [SerializeField, Min(1)] private int _endHourOffset = 24;

        public string OfferId => _offerId;
        public string Title => _title;
        public string Rarity => _rarity;
        public Sprite Icon => _icon;
        public string ItemId => _itemId;
        public CurrencyType PriceCurrency => _priceCurrency;
        public long Price => Math.Max(0L, _price);
        public int StartHourOffset => Math.Max(0, _startHourOffset);
        public int EndHourOffset => Math.Max(StartHourOffset + 1, _endHourOffset);
    }

    [Serializable]
    public sealed class LiveEventWheelPrizeDefinition
    {
        [SerializeField] private string _prizeId = string.Empty;
        [SerializeField] private string _title = string.Empty;
        [SerializeField] private string _typeLabel = string.Empty;
        [SerializeField] private Sprite _icon = null;
        [SerializeField, Min(1)] private int _weight = 1;
        [SerializeField, Min(0)] private int _displayOrder;
        [SerializeField] private LiveEventGrantDefinition _grant = new();

        public string PrizeId => _prizeId;
        public string Title => _title;
        public string TypeLabel => _typeLabel;
        public Sprite Icon => _icon;
        public int Weight => Math.Max(1, _weight);
        public int DisplayOrder => Math.Max(0, _displayOrder);
        public LiveEventGrantDefinition Grant => _grant;
    }

    [Serializable]
    public sealed class LiveEventWheelDefinition
    {
        [Header("Identity and schedule")]
        [SerializeField] private string _wheelId = string.Empty;
        [SerializeField] private string _displayName = "Event Wheel";
        [SerializeField] private string _navigationLabel = string.Empty;
        [SerializeField, Min(0)] private int _startHourOffset;
        [SerializeField, Min(1)] private int _endHourOffset = 24;
        [SerializeField] private LiveEventWheelPresentation _presentation = null;

        [Header("Economy")]
        [FormerlySerializedAs("_dailyFreeSpins")]
        [SerializeField, Range(1, 3)] private int _maxFreeSpins = 3;
        [SerializeField, Min(1)] private int _freeSpinRefillMinutes = 15;
        [SerializeField, Min(1)] private int _pityThreshold = 100;
        [FormerlySerializedAs("_spinCurrency")]
        [SerializeField] private CurrencyType _packCurrency = CurrencyType.Premium;
        [SerializeField, Min(0)] private long _buyOneOriginalPrice = 10L;
        [SerializeField, Min(0)] private long _buyOnePrice = 5L;
        [SerializeField, Min(0)] private long _buyThreePrice = 19L;
        [SerializeField, Min(0)] private long _buyTenPrice = 59L;
        [SerializeField] private bool _dailyDealEnabled = true;
        [SerializeField] private bool _bestValueEnabled = true;
        [SerializeField] private string _featuredPrizeId = string.Empty;
        [SerializeField] private List<LiveEventWheelPrizeDefinition> _prizes = new();

        public string WheelId => _wheelId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? "Event Wheel" : _displayName;
        public string NavigationLabel => string.IsNullOrWhiteSpace(_navigationLabel)
            ? DisplayName
            : _navigationLabel;
        public int StartHourOffset => Math.Max(0, _startHourOffset);
        public int EndHourOffset => Math.Max(StartHourOffset + 1, _endHourOffset);
        public LiveEventWheelPresentation Presentation => _presentation;
        public int MaxFreeSpins => Math.Max(1, Math.Min(3, _maxFreeSpins));
        public int FreeSpinRefillMinutes => Math.Max(1, _freeSpinRefillMinutes);
        public int PityThreshold => Math.Max(1, _pityThreshold);
        public CurrencyType PackCurrency => _packCurrency;
        public long BuyOneOriginalPrice => Math.Max(0L, _buyOneOriginalPrice);
        public long BuyOnePrice => Math.Max(0L, _buyOnePrice);
        public long BuyThreePrice => Math.Max(0L, _buyThreePrice);
        public long BuyTenPrice => Math.Max(0L, _buyTenPrice);
        public bool DailyDealEnabled => _dailyDealEnabled;
        public bool BestValueEnabled => _bestValueEnabled;
        public string FeaturedPrizeId => _featuredPrizeId;
        public IReadOnlyList<LiveEventWheelPrizeDefinition> Prizes => _prizes;
    }

    [Serializable]
    public sealed class LiveEventBundleDefinition
    {
        [SerializeField] private string _bundleId = string.Empty;
        [SerializeField] private string _title = string.Empty;
        [SerializeField] private string _description = string.Empty;
        [SerializeField] private Sprite _banner = null;
        [SerializeField] private LiveEventBundlePresentation _presentation = null;
        [SerializeField] private CurrencyType _priceCurrency = CurrencyType.Premium;
        [SerializeField, Min(0)] private long _originalPrice;
        [SerializeField, Min(0)] private long _salePrice;
        [SerializeField, Min(0)] private int _startHourOffset;
        [SerializeField, Min(1)] private int _endHourOffset = 24;
        [SerializeField] private List<LiveEventGrantDefinition> _grants = new();

        public string BundleId => _bundleId;
        public string Title => _title;
        public string Description => _description;
        public Sprite Banner => _banner;
        public LiveEventBundlePresentation Presentation => _presentation;
        public CurrencyType PriceCurrency => _priceCurrency;
        public long OriginalPrice => Math.Max(0L, _originalPrice);
        public long SalePrice => Math.Max(0L, _salePrice);
        public int StartHourOffset => Math.Max(0, _startHourOffset);
        public int EndHourOffset => Math.Max(StartHourOffset + 1, _endHourOffset);
        public IReadOnlyList<LiveEventGrantDefinition> Grants => _grants;
    }
}
