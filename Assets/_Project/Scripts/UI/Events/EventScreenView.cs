using System;
using System.Collections.Generic;
using PawArena.Core.Economy;
using PawArena.Core.Inventory;
using PawArena.LiveEvents;
using PawArena.UI.Motion;
using PawArena.UI.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PawArena.UI.Events
{
    /// <summary>Renders every module of the currently bound live-event definition.</summary>
    [DisallowMultipleComponent]
    public sealed class EventScreenView : MonoBehaviour
    {
        private const int TabCount = 5;
        private const int FullyVisibleRewardCards = 3;
        private const float RewardCardWidth = 188f;
        private const float RewardCardHeight = 400f;
        private const float RewardCardSpacing = 12f;
        private const float RewardWeekHorizontalPadding = 32f;
        private const float RewardWeekWidth = 620f;
        private const float RewardWeekHeight = 430f;
        private const float RewardEndCardWidth = 210f;
        private const float QuestRowHeight = 72f;
        private const float QuestRewardButtonWidth = 250f;
        private const float QuestSectionHeaderHeight = 54f;
        private const int MaximumVisibleShopItems = 4;
        private const int MaximumRareWheelRows = 2;
        private const int MaximumBundlePreviewRewards = 4;
        private const int MaximumBundlePageDots = 4;
        private const float WheelRewardRadius = 218f;
        private const string RewardLayoutResourcePath = "UI/EventRewardsRuntimeLayout";

        private static readonly Color QuestDefaultTextColor = Color.white;
        private static readonly Color QuestProgressTextColor = new(0.82f, 0.79f, 1f, 1f);
        private static readonly Color QuestClaimedTextColor = new(0.35f, 0.92f, 0.08f, 1f);
        private static readonly Color QuestLockedTextColor = new(0.72f, 0.66f, 0.9f, 1f);

        [Header("Header")]
        [SerializeField] private TMP_Text _eventTitle = null;
        [SerializeField] private TMP_Text _eventCountdown = null;
        [SerializeField] private TMP_Text _eventCurrency = null;
        [SerializeField] private Image _eventCurrencyIcon = null;
        [SerializeField] private Image _eventBanner = null;

        [Header("Tabs")]
        [SerializeField] private Button[] _tabButtons = new Button[TabCount];
        [SerializeField] private Image[] _tabBackgrounds = new Image[TabCount];
        [SerializeField] private GameObject[] _tabPanels = new GameObject[TabCount];
        [SerializeField] private Sprite _activeTabSprite = null;
        [SerializeField] private Sprite _inactiveTabSprite = null;
        [SerializeField] private TMP_Text _wheelTabLabel = null;

        [Header("Quests")]
        [SerializeField] private TMP_Text _dailyResetCountdown = null;
        [SerializeField] private TMP_Text _hardQuestLock = null;
        [SerializeField] private Image _hardQuestLockIcon = null;
        [SerializeField] private ScrollRect _questScroll = null;
        [SerializeField] private RectTransform _questContent = null;
        [SerializeField] private RectTransform _dailyQuestContent = null;
        [SerializeField] private RectTransform _hardQuestContent = null;
        [SerializeField] private GameObject _questTemplate = null;

        [Header("Rewards")]
        [SerializeField] private TMP_Text _rewardProgress = null;
        [SerializeField] private ScrollRect _rewardScroll = null;
        [SerializeField] private RectTransform _rewardContent = null;
        [SerializeField] private GameObject _rewardWeekTemplate = null;
        [SerializeField] private GameObject _rewardTemplate = null;
        [SerializeField] private Button _jumpToLastReward = null;
        [SerializeField] private TMP_Text _jumpToLastRewardLabel = null;
        [SerializeField] private TMP_Text _rewardEndCountdown = null;
        [SerializeField] private Button _claimAllRewards = null;
        [SerializeField] private TMP_Text _claimAllRewardsLabel = null;
        [SerializeField] private Image _grandPrizeIcon = null;
        [SerializeField] private TMP_Text _grandPrizeTitle = null;
        [SerializeField] private TMP_Text _grandPrizeDescription = null;
        [SerializeField] private TMP_Text _grandPrizeStatus = null;
        [SerializeField] private Image _grandPrizePriceIcon = null;
        [SerializeField] private TMP_Text _grandPrizePrice = null;
        [SerializeField] private Button _grandPrizeAction = null;
        [SerializeField] private TMP_Text _grandPrizeActionLabel = null;
        [SerializeField] private Button _grandPrizePremiumAction = null;
        [SerializeField] private TMP_Text _grandPrizePremiumActionLabel = null;

        [Header("Shop")]
        [SerializeField] private TMP_Text _shopCountdown = null;
        [SerializeField] private TMP_Text _shopEmptyState = null;
        [SerializeField] private RectTransform _shopContent = null;
        [SerializeField] private GameObject _shopTemplate = null;
        [SerializeField] private Sprite _shopCommonPreviewSprite = null;
        [SerializeField] private Sprite _shopRarePreviewSprite = null;
        [SerializeField] private Sprite _shopEpicPreviewSprite = null;
        [SerializeField] private Sprite _shopLegendaryPreviewSprite = null;
        [SerializeField] private Sprite _shopMythicPreviewSprite = null;
        [SerializeField] private Sprite _shopCoinIcon = null;
        [SerializeField] private Sprite _shopPremiumIcon = null;

        [Header("Wheel")]
        [SerializeField] private RectTransform _wheelContent = null;
        [SerializeField] private EventWheelAnimator _wheelAnimator = null;
        [SerializeField] private TMP_Text _wheelTitle = null;
        [SerializeField] private Image _wheelBackground = null;
        [SerializeField] private Image _wheelDecoration = null;
        [SerializeField] private GameObject _wheelPrizeTemplate = null;
        [SerializeField] private Image _wheelPreviewImage = null;
        [SerializeField] private TMP_Text _wheelRewardName = null;
        [SerializeField] private TMP_Text _wheelRewardType = null;
        [SerializeField] private Slider _wheelPitySlider = null;
        [SerializeField] private TMP_Text _wheelPityText = null;
        [SerializeField] private TMP_Text[] _wheelRareRewardNames = new TMP_Text[MaximumRareWheelRows];
        [SerializeField] private TMP_Text[] _wheelRareRewardOdds = new TMP_Text[MaximumRareWheelRows];
        [SerializeField] private TMP_Text _wheelResultText = null;
        [SerializeField] private TMP_Text _wheelFreeSpinCountdown = null;
        [SerializeField] private Button _freeSpinButton = null;
        [SerializeField] private TMP_Text _freeSpinLabel = null;
        [SerializeField] private Button _paidSpinButton = null;
        [SerializeField] private TMP_Text _paidSpinLabel = null;
        [SerializeField] private Button _wheelPreviewButton = null;
        [SerializeField] private Button _wheelOddsButton = null;
        [SerializeField] private Toggle _fastSpinToggle = null;
        [SerializeField] private Image _fastSpinTrack = null;
        [SerializeField] private RectTransform _fastSpinKnob = null;
        [SerializeField] private TMP_Text _fastSpinStateText = null;
        [SerializeField] private Sprite _fastSpinOffSprite = null;
        [SerializeField] private Sprite _fastSpinOnSprite = null;
        [SerializeField] private Button _buyOneSpinButton = null;
        [SerializeField] private TMP_Text _buyOneSpinPrice = null;
        [SerializeField] private GameObject _buyOneDailyDealBadge = null;
        [SerializeField] private TMP_Text _buyOneOriginalPrice = null;
        [SerializeField] private Button _buyThreeSpinsButton = null;
        [SerializeField] private TMP_Text _buyThreeSpinsPrice = null;
        [SerializeField] private Button _buyTenSpinsButton = null;
        [SerializeField] private TMP_Text _buyTenSpinsPrice = null;
        [SerializeField] private GameObject _buyTenBestValueBadge = null;

        [Header("Bundles")]
        [SerializeField] private Sprite _bundleAbilityIcon = null;
        [SerializeField] private Sprite _bundleEffectIcon = null;
        [SerializeField] private Sprite _bundleFireworksIcon = null;
        [SerializeField] private Sprite _bundleEmoteIcon = null;
        [SerializeField] private RectTransform _bundleContent = null;
        [SerializeField] private GameObject _bundleTemplate = null;
        [SerializeField] private Image _bundleBanner = null;
        [SerializeField] private TMP_Text _bundleTitle = null;
        [SerializeField] private TMP_Text _bundleDescription = null;
        [SerializeField] private TMP_Text _bundleContents = null;
        [SerializeField] private TMP_Text _bundleCountdown = null;
        [SerializeField] private TMP_Text[] _bundleCountdownCells = new TMP_Text[4];
        [SerializeField] private TMP_Text _bundleOriginalPrice = null;
        [SerializeField] private TMP_Text _bundleDiscount = null;
        [SerializeField] private Image _bundlePriceIcon = null;
        [SerializeField] private Button _bundlePurchaseButton = null;
        [SerializeField] private TMP_Text _bundlePurchaseLabel = null;
        [SerializeField] private Image _bundleFeaturedBanner = null;
        [SerializeField] private TMP_Text _bundleFeaturedTitle = null;
        [SerializeField] private TMP_Text _bundleFeaturedStatus = null;
        [SerializeField] private Image[] _bundleFeaturedRewardIcons = new Image[MaximumBundlePreviewRewards];
        [SerializeField] private TMP_Text[] _bundleFeaturedRewardLabels = new TMP_Text[MaximumBundlePreviewRewards];
        [SerializeField] private Button _bundlePreviousButton = null;
        [SerializeField] private Button _bundleNextButton = null;
        [SerializeField] private Button _bundleViewButton = null;
        [SerializeField] private TMP_Text _bundleViewLabel = null;
        [SerializeField] private Image[] _bundlePageDots = new Image[MaximumBundlePageDots];
        [SerializeField] private Image _bundlePanelBackground = null;
        [SerializeField] private Image _bundleDetailsBackground = null;
        [SerializeField] private Image _bundleFeaturedBackground = null;

        private readonly List<GameObject> _runtimeObjects = new();
        private readonly List<ButtonListener> _runtimeListeners = new();
        private readonly List<QuestBinding> _questBindings = new();
        private readonly List<RewardBinding> _rewardBindings = new();
        private readonly List<RewardWeekBinding> _rewardWeekBindings = new();
        private readonly List<ShopBinding> _shopBindings = new();
        private readonly List<BundleBinding> _bundleBindings = new();
        private readonly List<LiveEventBundleDefinition> _featuredBundles = new();
        private readonly List<LiveEventWheelPrizeDefinition> _wheelDisplayPrizes = new();
        private readonly List<GameObject> _wheelRuntimeObjects = new();
        private ILiveEventService _service;
        private IWalletService _wallet;
        private IItemCatalog _bundleCatalog;
        private LiveEventRewardDefinition _grandPrize;
        private LiveEventBundleDefinition _selectedBundle;
        private int _featuredBundleIndex;
        private int _activeTab;
        private int _regularRewardCount;
        private float _nextCountdownRefresh;
        private ulong _availabilityFingerprint;
        private bool _hasAvailabilityFingerprint;
        private UIScreen _visibilityScreen;
        private GameObject _visibilityRoot;
        private CanvasGroup _visibilityCanvasGroup;
        private bool _wasWindowVisible;
        private bool _refreshPending;
        private LiveEventWheelPrizeDefinition _selectedWheelPrize;
        private UnityAction<bool> _fastSpinListener;
        private Image _eventWindowBackground;
        private GameObject _eventBannerChrome;
        private GameObject _eventSidebarChrome;
        private GameObject _eventCloseChrome;
        private bool _wheelInteractionLocked;
        private bool _fastSpinEnabled;

        /// <summary>Raised when the selected reward preview is requested.</summary>
        public event Action<string> WheelPreviewRequested;

        /// <summary>Raised when the player requests the complete wheel odds view.</summary>
        public event Action WheelOddsRequested;

        /// <summary>Raised when a configured spin pack purchase is requested.</summary>
        public event Action<int> WheelPurchaseRequested;

        /// <summary>Raised when the featured carousel bundle preview is requested.</summary>
        public event Action<string> BundlePreviewRequested;

        /// <summary>Connects seasonal state and wallet balances to the event presentation.</summary>
        public void Bind(ILiveEventService service, IWalletService wallet, IItemCatalog bundleCatalog = null)
        {
            Unbind();
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _bundleCatalog = bundleCatalog;
            EnsureRewardLayout();
            EnsureQuestLayout();
            ResolveOptionalPresentationReferences();
            BindVisibilityScreen();
            IsolateDynamicPresentation();
            _service.Changed += HandleServiceChanged;
            _wallet.BalanceChanged += HandleBalanceChanged;
            if (_rewardScroll != null)
            {
                _rewardScroll.onValueChanged.AddListener(HandleRewardScrollChanged);
            }

            BindStaticButtons();
            RebuildDynamicContent();
            SwitchTab(0);
            Refresh();
            _nextCountdownRefresh = Time.unscaledTime + 1f;
            _wasWindowVisible = IsEventWindowVisible();
            _refreshPending = false;
            enabled = _visibilityScreen == null || _wasWindowVisible;
        }

        /// <summary>Removes listeners and generated rows without disposing shared services.</summary>
        public void Unbind()
        {
            CancelWheelPresentation();
            UnbindVisibilityScreen();
            if (_service != null)
            {
                _service.Changed -= HandleServiceChanged;
            }

            if (_wallet != null)
            {
                _wallet.BalanceChanged -= HandleBalanceChanged;
            }

            if (_rewardScroll != null)
            {
                _rewardScroll.onValueChanged.RemoveListener(HandleRewardScrollChanged);
            }

            if (_fastSpinToggle != null && _fastSpinListener != null)
            {
                _fastSpinToggle.onValueChanged.RemoveListener(_fastSpinListener);
            }

            for (var i = 0; i < _runtimeListeners.Count; i++)
            {
                if (_runtimeListeners[i].Button != null)
                {
                    _runtimeListeners[i].Button.onClick.RemoveListener(_runtimeListeners[i].Action);
                }
            }

            _runtimeListeners.Clear();
            DestroyRuntimeObjects();
            _service = null;
            _wallet = null;
            _bundleCatalog = null;
            _grandPrize = null;
            _selectedBundle = null;
            _featuredBundles.Clear();
            _featuredBundleIndex = 0;
            _selectedWheelPrize = null;
            _wheelDisplayPrizes.Clear();
            _regularRewardCount = 0;
            _availabilityFingerprint = 0UL;
            _hasAvailabilityFingerprint = false;
            _wasWindowVisible = false;
            _refreshPending = false;
            _fastSpinListener = null;
        }

        private void OnDestroy() => Unbind();

        private void OnDisable() => CancelWheelPresentation();

        private void Update()
        {
            if (_service == null)
            {
                return;
            }

            if (!IsEventWindowVisible())
            {
                _wasWindowVisible = false;
                if (_visibilityScreen != null)
                {
                    enabled = false;
                }

                return;
            }

            if (!_wasWindowVisible || _refreshPending)
            {
                RefreshAfterBecomingVisible();
                return;
            }

            if (Time.unscaledTime < _nextCountdownRefresh)
            {
                return;
            }

            _nextCountdownRefresh = Time.unscaledTime + 1f;
            var fingerprint = BuildAvailabilityFingerprint();
            if (!_hasAvailabilityFingerprint || fingerprint != _availabilityFingerprint)
            {
                RebuildTimedContent();
                _availabilityFingerprint = fingerprint;
                _hasAvailabilityFingerprint = true;
            }

            RefreshCountdowns();
            if (_activeTab == 0)
            {
                RefreshRewards();
            }
        }

        private void BindVisibilityScreen()
        {
            UnbindVisibilityScreen();
            _visibilityScreen = FindVisibilityScreen();
            if (_visibilityScreen == null)
            {
                return;
            }

            _visibilityRoot = _visibilityScreen.gameObject;
            _visibilityCanvasGroup = _visibilityRoot.GetComponent<CanvasGroup>();
            _visibilityScreen.Shown += HandleScreenShown;
            _visibilityScreen.Hidden += HandleScreenHidden;
        }

        private void ResolveOptionalPresentationReferences()
        {
            _wheelTabLabel ??= FindText(transform, "EventTab_WheelLabel");
            _wheelTitle ??= FindText(transform, "EventWheelTitle");
            _wheelBackground ??= FindImage(transform, "EventWheelBackgroundOverlay");
            _wheelDecoration ??= FindImage(transform, "EventWheelDecoration");
            _wheelAnimator ??= Find(transform, "EventWheelArea")?.GetComponent<EventWheelAnimator>();
            _bundlePanelBackground ??= FindImage(transform, "EventBundleReadabilityShade");
            _bundleDetailsBackground ??= _bundlePanelBackground;
            _bundleFeaturedBackground ??= FindImage(transform, "EventBundleFeaturedCard");
        }

        private void UnbindVisibilityScreen()
        {
            if (_visibilityScreen != null)
            {
                _visibilityScreen.Shown -= HandleScreenShown;
                _visibilityScreen.Hidden -= HandleScreenHidden;
            }

            _visibilityScreen = null;
            _visibilityRoot = null;
            _visibilityCanvasGroup = null;
        }

        private UIScreen FindVisibilityScreen()
        {
            var screen = _eventTitle != null
                ? _eventTitle.GetComponentInParent<UIScreen>(true)
                : null;
            if (screen != null)
            {
                return screen;
            }

            for (var i = 0; i < _tabPanels.Length; i++)
            {
                if (_tabPanels[i] == null)
                {
                    continue;
                }

                screen = _tabPanels[i].GetComponentInParent<UIScreen>(true);
                if (screen != null)
                {
                    return screen;
                }
            }

            return null;
        }

        private bool IsEventWindowVisible()
        {
            if (_visibilityRoot == null)
            {
                return isActiveAndEnabled;
            }

            return _visibilityRoot.activeInHierarchy &&
                   (_visibilityCanvasGroup == null || _visibilityCanvasGroup.alpha > 0.001f);
        }

        private void HandleScreenShown()
        {
            enabled = true;
            RefreshAfterBecomingVisible();
        }

        private void HandleScreenHidden()
        {
            CancelWheelPresentation();
            if (_activeTab == 3 || _activeTab == 4)
            {
                SwitchTab(0);
            }

            _wasWindowVisible = false;
            enabled = false;
        }

        private void RefreshAfterBecomingVisible()
        {
            if (_service == null || !IsEventWindowVisible())
            {
                return;
            }

            var fingerprint = BuildAvailabilityFingerprint();
            if (!_hasAvailabilityFingerprint || fingerprint != _availabilityFingerprint)
            {
                RebuildTimedContent();
                _availabilityFingerprint = fingerprint;
                _hasAvailabilityFingerprint = true;
            }

            Refresh();
            _wasWindowVisible = true;
            _refreshPending = false;
            _nextCountdownRefresh = Time.unscaledTime + 1f;
        }

        private void IsolateDynamicPresentation()
        {
            RuntimeUiState.EnsureNonInteractiveNestedCanvas(_eventCountdown?.gameObject);
            RuntimeUiState.EnsureNonInteractiveNestedCanvas(_rewardEndCountdown?.gameObject);
            RuntimeUiState.EnsureNonInteractiveNestedCanvas(_dailyResetCountdown?.gameObject);
            RuntimeUiState.EnsureNonInteractiveNestedCanvas(_shopCountdown?.gameObject);
            RuntimeUiState.EnsureNonInteractiveNestedCanvas(_bundleCountdown?.gameObject);
        }

        private void BindStaticButtons()
        {
            for (var i = 0; i < _tabButtons.Length; i++)
            {
                var tabIndex = i;
                BindButton(_tabButtons[i], () => SwitchTab(tabIndex));
            }

            BindButton(_jumpToLastReward, JumpToLastReward);
            BindButton(_claimAllRewards, ClaimAllRewards);
            BindButton(_freeSpinButton, RequestPrimaryWheelSpin);
            BindButton(_paidSpinButton, () => Spin(false));
            BindButton(_wheelPreviewButton, RequestWheelPreview);
            BindButton(_wheelOddsButton, RequestWheelOdds);
            BindButton(_buyOneSpinButton, () => RequestWheelPurchase(1));
            BindButton(_buyThreeSpinsButton, () => RequestWheelPurchase(3));
            BindButton(_buyTenSpinsButton, () => RequestWheelPurchase(10));
            BindButton(_bundlePurchaseButton, PurchaseSelectedBundle);
            BindButton(_bundlePreviousButton, () => MoveFeaturedBundle(-1));
            BindButton(_bundleNextButton, () => MoveFeaturedBundle(1));
            BindButton(_bundleViewButton, RequestFeaturedBundlePreview);

            if (_fastSpinToggle != null)
            {
                _fastSpinListener = HandleFastSpinChanged;
                _fastSpinToggle.onValueChanged.AddListener(_fastSpinListener);
                _fastSpinEnabled = _fastSpinToggle.isOn;
                RefreshFastSpinVisual();
            }
        }

        private void EnsureRewardLayout()
        {
            if (_rewardWeekTemplate != null &&
                _rewardEndCountdown != null &&
                _claimAllRewards != null &&
                _grandPrizeAction != null)
            {
                ConfigureRewardLayoutSizing();
                ConfigureGrandPrizeCandyAction();
                return;
            }

            var rewardsPanel = _tabPanels != null && _tabPanels.Length > 0
                ? _tabPanels[0]
                : null;
            if (rewardsPanel == null)
            {
                Debug.LogError("The event Rewards panel is not assigned.", this);
                return;
            }

            var layout = Find(rewardsPanel.transform, "EventRewardsRuntimeLayout")?.gameObject;
            if (layout == null)
            {
                var layoutPrefab = Resources.Load<GameObject>(RewardLayoutResourcePath);
                if (layoutPrefab == null)
                {
                    Debug.LogError(
                        $"Rewards layout resource '{RewardLayoutResourcePath}' could not be loaded.",
                        this);
                    return;
                }

                layout = Instantiate(layoutPrefab, rewardsPanel.transform, false);
                layout.name = "EventRewardsRuntimeLayout";
            }

            for (var childIndex = 0; childIndex < rewardsPanel.transform.childCount; childIndex++)
            {
                var child = rewardsPanel.transform.GetChild(childIndex);
                SetActive(child.gameObject, child.gameObject == layout);
            }

            if (layout.transform is RectTransform layoutRect)
            {
                layoutRect.anchorMin = Vector2.zero;
                layoutRect.anchorMax = Vector2.one;
                layoutRect.offsetMin = Vector2.zero;
                layoutRect.offsetMax = Vector2.zero;
            }

            var root = layout.transform;
            _rewardProgress = FindText(root, "EventRewardProgress");
            _rewardScroll = Find(root, "EventRewardScroll")?.GetComponent<ScrollRect>();
            _rewardContent = Find(root, "EventRewardContent") as RectTransform;
            _rewardWeekTemplate = Find(root, "EventRewardWeekTemplate")?.gameObject;
            _rewardTemplate = Find(root, "EventRewardTemplate")?.gameObject;
            _jumpToLastReward = FindButton(root, "EventRewardJumpEnd");
            _jumpToLastRewardLabel = FindText(root, "EventRewardJumpEndLabel");
            _rewardEndCountdown = FindText(root, "EventRewardEndCountdown");
            _claimAllRewards = FindButton(root, "EventRewardClaimAll");
            _claimAllRewardsLabel = FindText(root, "EventRewardClaimAllLabel");
            _grandPrizeIcon = FindImage(root, "EventGrandPrizeIcon");
            _grandPrizeTitle = FindText(root, "EventGrandPrizeTitle");
            _grandPrizeDescription = FindText(root, "EventGrandPrizeDescription");
            _grandPrizeStatus = FindText(root, "EventGrandPrizeStatus");
            _grandPrizePriceIcon = FindImage(root, "EventGrandPrizePriceIcon");
            _grandPrizePrice = FindText(root, "EventGrandPrizePrice");
            _grandPrizeAction = FindButton(root, "EventGrandPrizeAction");
            _grandPrizeActionLabel = FindText(root, "EventGrandPrizeActionLabel");
            _grandPrizePremiumAction = FindButton(root, "EventGrandPrizePremiumAction");
            _grandPrizePremiumActionLabel = FindText(root, "EventGrandPrizePremiumActionLabel");
            ConfigureRewardLayoutSizing();
            ConfigureGrandPrizeCandyAction();
        }

        private void ConfigureGrandPrizeCandyAction()
        {
            if (_grandPrizeAction == null)
            {
                return;
            }

            var actionRect = _grandPrizeAction.transform as RectTransform;
            var pricePanel = _grandPrizePrice != null
                ? _grandPrizePrice.transform.parent as RectTransform
                : null;
            if (actionRect != null && pricePanel != null && pricePanel != actionRect)
            {
                actionRect.anchorMin = pricePanel.anchorMin;
                actionRect.anchorMax = pricePanel.anchorMax;
                actionRect.pivot = pricePanel.pivot;
                actionRect.anchoredPosition = pricePanel.anchoredPosition;
                actionRect.sizeDelta = pricePanel.sizeDelta;

                if (_grandPrizePriceIcon != null)
                {
                    _grandPrizePriceIcon.transform.SetParent(actionRect, false);
                    SetRect(
                        _grandPrizePriceIcon.rectTransform,
                        new Vector2(0f, 0.5f),
                        new Vector2(0f, 0.5f),
                        new Vector2(52f, 0f),
                        new Vector2(34f, 34f));
                }

                _grandPrizePrice.transform.SetParent(actionRect, false);
                SetRect(
                    _grandPrizePrice.rectTransform,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(20f, 0f),
                    new Vector2(-20f, 0f));
                _grandPrizePrice.alignment = TextAlignmentOptions.Center;
                SetActive(pricePanel.gameObject, false);
            }

            if (_grandPrizeActionLabel != null && _grandPrizeActionLabel != _grandPrizePrice)
            {
                SetActive(_grandPrizeActionLabel.gameObject, false);
            }

            _grandPrizeActionLabel = _grandPrizePrice ?? _grandPrizeActionLabel;
            SetActive(_grandPrizePremiumAction, false);
        }

        private void EnsureQuestLayout()
        {
            var questsPanel = _tabPanels != null && _tabPanels.Length > 1
                ? _tabPanels[1]
                : null;
            if (questsPanel == null)
            {
                Debug.LogError("The event Quests panel is not assigned.", this);
                return;
            }

            var panel = questsPanel.transform;
            var scrollTransform = Find(panel, "EventQuestScroll") as RectTransform
                ?? Find(panel, "EventDailyQuestScroll") as RectTransform;
            if (scrollTransform == null)
            {
                Debug.LogError("The event Quest scroll area could not be found.", this);
                return;
            }

            scrollTransform.name = "EventQuestScroll";
            StretchRect(scrollTransform, 18f, 18f, 18f, 18f);
            _questScroll = GetOrAddComponent<ScrollRect>(scrollTransform.gameObject);
            _questScroll.horizontal = false;
            _questScroll.vertical = true;
            _questScroll.movementType = ScrollRect.MovementType.Clamped;
            _questScroll.inertia = true;
            _questScroll.scrollSensitivity = 25f;
            _questScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            var legacyDailyContent = _dailyQuestContent != null
                ? _dailyQuestContent
                : Find(scrollTransform, "EventDailyQuestContent") as RectTransform;
            var unifiedContent = _questContent != null
                ? _questContent
                : Find(scrollTransform, "EventQuestUnifiedContent") as RectTransform;
            unifiedContent ??= legacyDailyContent;
            if (unifiedContent == null)
            {
                Debug.LogError("The event Quest content could not be found.", this);
                return;
            }

            unifiedContent.name = "EventQuestUnifiedContent";
            unifiedContent.SetParent(scrollTransform, false);
            ConfigureQuestUnifiedContent(unifiedContent);
            _questContent = unifiedContent;
            _questScroll.content = unifiedContent;
            _questScroll.viewport ??= scrollTransform;

            var scrollbar = _questScroll.verticalScrollbar;
            if (scrollbar == null)
            {
                scrollbar = Find(scrollTransform, "EventDailyQuestScrollScrollbar")?.GetComponent<Scrollbar>()
                    ?? Find(scrollTransform, "EventQuestScrollbar")?.GetComponent<Scrollbar>();
                _questScroll.verticalScrollbar = scrollbar;
            }

            if (scrollbar != null && scrollbar.transform is RectTransform scrollbarRect)
            {
                scrollbarRect.name = "EventQuestScrollbar";
                scrollbarRect.anchorMin = new Vector2(1f, 0f);
                scrollbarRect.anchorMax = Vector2.one;
                scrollbarRect.pivot = new Vector2(1f, 0.5f);
                scrollbarRect.anchoredPosition = new Vector2(-4f, 0f);
                scrollbarRect.sizeDelta = new Vector2(12f, -12f);
            }

            var dailyHeader = Find(unifiedContent, "EventDailyQuestHeader") as RectTransform
                ?? CreateRect("EventDailyQuestHeader", unifiedContent);
            var dailyRows = (_dailyQuestContent != null && _dailyQuestContent != unifiedContent
                    ? _dailyQuestContent
                    : null)
                ?? Find(unifiedContent, "EventDailyQuestContent") as RectTransform
                ?? CreateRect("EventDailyQuestContent", unifiedContent);
            var hardHeader = Find(unifiedContent, "EventHardQuestHeader") as RectTransform
                ?? CreateRect("EventHardQuestHeader", unifiedContent);

            var legacyHardContent = _hardQuestContent != null
                ? _hardQuestContent
                : Find(panel, "EventHardQuestContent") as RectTransform;
            var hardRows = Find(unifiedContent, "EventHardQuestContent") as RectTransform
                ?? legacyHardContent
                ?? CreateRect("EventHardQuestContent", unifiedContent);
            dailyRows.name = "EventDailyQuestContent";
            hardRows.name = "EventHardQuestContent";

            dailyHeader.SetParent(unifiedContent, false);
            dailyRows.SetParent(unifiedContent, false);
            hardHeader.SetParent(unifiedContent, false);
            hardRows.SetParent(unifiedContent, false);
            dailyHeader.SetSiblingIndex(0);
            dailyRows.SetSiblingIndex(1);
            hardHeader.SetSiblingIndex(2);
            hardRows.SetSiblingIndex(3);

            _dailyQuestContent = dailyRows;
            _hardQuestContent = hardRows;
            ConfigureQuestRows(dailyRows);
            ConfigureQuestRows(hardRows);

            var dailyTitle = FindText(panel, "EventDailyQuestTitle");
            var hardTitle = FindText(panel, "EventHardQuestTitle");
            _dailyResetCountdown ??= FindText(panel, "EventDailyResetCountdown");
            _hardQuestLock ??= FindText(panel, "EventHardQuestLock");
            ConfigureDailyQuestHeader(dailyHeader, dailyTitle, _dailyResetCountdown);
            ConfigureHardQuestHeader(hardHeader, hardTitle, _hardQuestLock);

            var legacyHardScroll = Find(panel, "EventHardQuestScroll");
            if (legacyHardScroll != null && legacyHardScroll != scrollTransform)
            {
                SetActive(legacyHardScroll.gameObject, false);
            }

            _questTemplate ??= Find(panel, "EventQuestTemplate")?.gameObject;
            ConfigureQuestTemplate();
        }

        private static void ConfigureQuestUnifiedContent(RectTransform content)
        {
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(-28f, 0f);

            var layout = GetOrAddComponent<VerticalLayoutGroup>(content.gameObject);
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = GetOrAddComponent<ContentSizeFitter>(content.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void ConfigureQuestRows(RectTransform rows)
        {
            var layout = GetOrAddComponent<VerticalLayoutGroup>(rows.gameObject);
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = GetOrAddComponent<ContentSizeFitter>(rows.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void ConfigureDailyQuestHeader(
            RectTransform header,
            TMP_Text title,
            TMP_Text countdown)
        {
            ConfigureQuestHeaderRoot(header);
            if (title != null)
            {
                title.transform.SetParent(header, false);
                SetRect(
                    title.rectTransform,
                    new Vector2(0f, 0f),
                    new Vector2(0.58f, 1f),
                    new Vector2(12f, 0f),
                    new Vector2(-6f, 0f));
                title.alignment = TextAlignmentOptions.MidlineLeft;
                title.fontSize = 30f;
                title.color = QuestDefaultTextColor;
                title.text = "Daily quests";
            }

            if (countdown != null)
            {
                countdown.transform.SetParent(header, false);
                SetRect(
                    countdown.rectTransform,
                    new Vector2(0.52f, 0f),
                    Vector2.one,
                    new Vector2(6f, 0f),
                    new Vector2(-12f, 0f));
                countdown.alignment = TextAlignmentOptions.MidlineRight;
                countdown.fontSize = 23f;
                countdown.color = new Color(1f, 0.48f, 0.35f, 1f);
            }
        }

        private void ConfigureHardQuestHeader(
            RectTransform header,
            TMP_Text title,
            TMP_Text lockMessage)
        {
            ConfigureQuestHeaderRoot(header);
            _hardQuestLockIcon ??= FindImage(header, "EventHardQuestLockIcon");
            if (_hardQuestLockIcon == null)
            {
                var iconRect = CreateRect("EventHardQuestLockIcon", header);
                _hardQuestLockIcon = GetOrAddComponent<Image>(iconRect.gameObject);
                _hardQuestLockIcon.raycastTarget = false;
                _hardQuestLockIcon.preserveAspect = true;
            }

            var lockSprite = _hardQuestLockIcon.sprite
                ?? (_rewardTemplate != null
                    ? FindImage(_rewardTemplate.transform, "RewardLockIcon")?.sprite
                    : null);
            SetImage(_hardQuestLockIcon, lockSprite);
            SetRect(
                _hardQuestLockIcon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(30f, 0f),
                new Vector2(34f, 34f));

            if (title != null)
            {
                title.transform.SetParent(header, false);
                SetRect(
                    title.rectTransform,
                    new Vector2(0f, 0f),
                    new Vector2(0.52f, 1f),
                    new Vector2(58f, 0f),
                    new Vector2(-6f, 0f));
                title.alignment = TextAlignmentOptions.MidlineLeft;
                title.fontSize = 30f;
                title.color = QuestLockedTextColor;
                title.text = "Hard quests";
            }

            if (lockMessage != null)
            {
                lockMessage.transform.SetParent(header, false);
                SetRect(
                    lockMessage.rectTransform,
                    new Vector2(0.48f, 0f),
                    Vector2.one,
                    new Vector2(6f, 0f),
                    new Vector2(-12f, 0f));
                lockMessage.alignment = TextAlignmentOptions.MidlineRight;
                lockMessage.fontSize = 22f;
                lockMessage.color = new Color(1f, 0.48f, 0.35f, 1f);
            }
        }

        private static void ConfigureQuestHeaderRoot(RectTransform header)
        {
            var element = GetOrAddComponent<LayoutElement>(header.gameObject);
            element.minHeight = QuestSectionHeaderHeight;
            element.preferredHeight = QuestSectionHeaderHeight;
            element.flexibleHeight = 0f;
        }

        private void ConfigureQuestTemplate()
        {
            if (_questTemplate == null)
            {
                return;
            }

            var root = _questTemplate.transform as RectTransform;
            if (root == null)
            {
                return;
            }

            var rootImage = root.GetComponent<Image>();
            var rowSprite = rootImage != null ? rootImage.sprite : null;
            if (rootImage != null)
            {
                rootImage.enabled = false;
                rootImage.raycastTarget = false;
            }

            var element = GetOrAddComponent<LayoutElement>(_questTemplate);
            element.minHeight = QuestRowHeight;
            element.preferredHeight = QuestRowHeight;
            element.flexibleHeight = 0f;

            var horizontal = GetOrAddComponent<HorizontalLayoutGroup>(_questTemplate);
            horizontal.padding = new RectOffset(0, 0, 0, 0);
            horizontal.spacing = 14f;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;

            var info = Find(root, "QuestInfo") as RectTransform
                ?? CreateRect("QuestInfo", root);
            var infoImage = GetOrAddComponent<Image>(info.gameObject);
            infoImage.sprite = rowSprite ?? infoImage.sprite;
            infoImage.type = Image.Type.Sliced;
            infoImage.raycastTarget = false;
            var infoElement = GetOrAddComponent<LayoutElement>(info.gameObject);
            infoElement.minHeight = QuestRowHeight;
            infoElement.preferredHeight = QuestRowHeight;
            infoElement.flexibleWidth = 1f;
            infoElement.flexibleHeight = 0f;

            var title = FindText(root, "QuestTitle");
            if (title != null)
            {
                title.transform.SetParent(info, false);
                SetRect(
                    title.rectTransform,
                    Vector2.zero,
                    new Vector2(0.78f, 1f),
                    new Vector2(22f, 0f),
                    new Vector2(-8f, 0f));
                title.alignment = TextAlignmentOptions.MidlineLeft;
                title.fontSize = 25f;
            }

            var progress = FindText(root, "QuestProgress");
            if (progress != null)
            {
                progress.transform.SetParent(info, false);
                SetRect(
                    progress.rectTransform,
                    new Vector2(0.76f, 0f),
                    Vector2.one,
                    new Vector2(8f, 0f),
                    new Vector2(-22f, 0f));
                progress.alignment = TextAlignmentOptions.MidlineRight;
                progress.fontSize = 25f;
            }

            var action = FindButton(root, "QuestAction");
            if (action == null)
            {
                return;
            }

            action.transform.SetParent(root, false);
            info.SetSiblingIndex(0);
            action.transform.SetSiblingIndex(1);
            action.transition = Selectable.Transition.None;
            var actionElement = GetOrAddComponent<LayoutElement>(action.gameObject);
            actionElement.minWidth = QuestRewardButtonWidth;
            actionElement.preferredWidth = QuestRewardButtonWidth;
            actionElement.flexibleWidth = 0f;
            actionElement.minHeight = QuestRowHeight;
            actionElement.preferredHeight = QuestRowHeight;
            actionElement.flexibleHeight = 0f;

            var rewardIcon = FindImage(action.transform, "QuestRewardIcon");
            if (rewardIcon == null)
            {
                var iconRect = CreateRect("QuestRewardIcon", action.transform);
                rewardIcon = GetOrAddComponent<Image>(iconRect.gameObject);
            }

            rewardIcon.raycastTarget = false;
            rewardIcon.preserveAspect = true;
            SetImage(rewardIcon, _service?.Definition.CurrencyIcon ?? _eventCurrencyIcon?.sprite);
            SetRect(
                rewardIcon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(58f, 0f),
                new Vector2(34f, 34f));

            var label = FindText(action.transform, "QuestRewardAmount")
                ?? FindText(action.transform, "QuestActionLabel");
            if (label != null)
            {
                label.gameObject.name = "QuestRewardAmount";
                SetRect(
                    label.rectTransform,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(54f, 0f),
                    new Vector2(-16f, 0f));
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 27f;
            }
        }

        private void ConfigureRewardLayoutSizing()
        {
            if (_rewardContent != null)
            {
                ConfigureHorizontalLayoutGroup(_rewardContent.GetComponent<HorizontalLayoutGroup>());
                var contentFitter = _rewardContent.GetComponent<ContentSizeFitter>();
                if (contentFitter != null)
                {
                    contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    contentFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                }
            }

            ConfigureFixedWidthLayoutElement(
                _rewardWeekTemplate,
                RewardWeekWidth,
                RewardWeekHeight);
            ConfigureFixedWidthLayoutElement(
                _rewardTemplate,
                RewardCardWidth,
                RewardCardHeight);

            var weekItems = _rewardWeekTemplate != null
                ? Find(_rewardWeekTemplate.transform, "RewardWeekItems")
                : null;
            if (weekItems != null)
            {
                ConfigureHorizontalLayoutGroup(weekItems.GetComponent<HorizontalLayoutGroup>());
            }

            var endCard = _rewardEndCountdown != null
                ? _rewardEndCountdown.transform.parent?.gameObject
                : null;
            ConfigureFixedWidthLayoutElement(endCard, RewardEndCardWidth, RewardWeekHeight);
        }

        private static void ConfigureHorizontalLayoutGroup(HorizontalLayoutGroup layout)
        {
            if (layout == null)
            {
                return;
            }

            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;
        }

        private static void ConfigureFixedWidthLayoutElement(
            GameObject target,
            float width,
            float height)
        {
            if (target == null)
            {
                return;
            }

            var layout = target.GetComponent<LayoutElement>();
            if (layout == null)
            {
                return;
            }

            layout.ignoreLayout = false;
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
        }

        private void RebuildDynamicContent()
        {
            DestroyRuntimeObjects();
            BuildQuests();
            BuildRewards();
            RebuildTimedContent();
            _availabilityFingerprint = BuildAvailabilityFingerprint();
            _hasAvailabilityFingerprint = true;
        }

        private void RebuildTimedContent()
        {
            DestroyBindings(_shopBindings);
            DestroyBindings(_bundleBindings);
            DestroyWheelRuntimeObjects();
            BuildShop();
            BuildWheel();
            BuildBundles();
        }

        private void BuildQuests()
        {
            BuildQuestGroup(_service.Definition.DailyQuests, _dailyQuestContent, false);
            BuildQuestGroup(_service.Definition.HardQuests, _hardQuestContent, true);

            Canvas.ForceUpdateCanvases();
            if (_dailyQuestContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_dailyQuestContent);
            }

            if (_hardQuestContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_hardQuestContent);
            }

            if (_questContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_questContent);
            }

            if (_questScroll != null)
            {
                _questScroll.StopMovement();
                _questScroll.verticalNormalizedPosition = 1f;
            }
        }

        private void BuildQuestGroup(
            IReadOnlyList<LiveEventQuestDefinition> quests,
            RectTransform content,
            bool hard)
        {
            if (content == null || _questTemplate == null)
            {
                return;
            }

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null)
                {
                    continue;
                }

                var instance = CreateRuntimeObject(_questTemplate, content, "Quest_" + quest.QuestId);
                var button = FindButton(instance.transform, "QuestAction");
                var background = button != null
                    ? button.targetGraphic as Image ?? button.GetComponent<Image>()
                    : null;
                var binding = new QuestBinding(
                    quest,
                    hard,
                    FindText(instance.transform, "QuestTitle"),
                    FindText(instance.transform, "QuestProgress"),
                    button,
                    background,
                    background != null ? background.sprite : null,
                    button != null ? button.spriteState.disabledSprite : null,
                    FindImage(instance.transform, "QuestRewardIcon"),
                    FindText(instance.transform, "QuestRewardAmount")
                        ?? FindText(instance.transform, "QuestActionLabel"));
                _questBindings.Add(binding);
                BindButton(binding.Button, () => ClaimQuest(quest.QuestId));
            }
        }

        private void BuildRewards()
        {
            if (_rewardContent == null || _rewardWeekTemplate == null || _rewardTemplate == null)
            {
                return;
            }

            var rewards = _service.Definition.Rewards;
            _regularRewardCount = Math.Max(0, rewards.Count - 1);
            var assignedRewards = new bool[_regularRewardCount];
            var weeks = _service.Definition.RewardWeeks;
            for (var weekIndex = 0; weekIndex < weeks.Count; weekIndex++)
            {
                var week = weeks[weekIndex];
                if (week == null)
                {
                    continue;
                }

                var rewardIndexes = new List<int>();
                for (var rewardIndex = 0; rewardIndex < _regularRewardCount; rewardIndex++)
                {
                    var reward = rewards[rewardIndex];
                    if (reward == null || !string.Equals(reward.WeekId, week.WeekId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    assignedRewards[rewardIndex] = true;
                    rewardIndexes.Add(rewardIndex);
                }

                if (rewardIndexes.Count > 0)
                {
                    BuildRewardWeek(week, rewardIndexes);
                }
            }

            var unassigned = new List<int>();
            for (var rewardIndex = 0; rewardIndex < assignedRewards.Length; rewardIndex++)
            {
                if (!assignedRewards[rewardIndex] && rewards[rewardIndex] != null)
                {
                    unassigned.Add(rewardIndex);
                }
            }

            if (unassigned.Count > 0)
            {
                BuildRewardWeek(null, unassigned);
            }

            var endCard = _rewardEndCountdown != null ? _rewardEndCountdown.transform.parent : null;
            if (endCard != null)
            {
                endCard.SetAsLastSibling();
            }

            if (rewards.Count > 0)
            {
                _grandPrize = rewards[rewards.Count - 1];
                SetImage(_grandPrizeIcon, _grandPrize.Icon);
                SetText(_grandPrizeTitle, _grandPrize.Title);
                SetText(_grandPrizeDescription, _grandPrize.Subtitle);
                BindButton(_grandPrizeAction, () => ClaimReward(_grandPrize.RewardId, CurrencyType.Candy));
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rewardContent);
            Canvas.ForceUpdateCanvases();
            FocusCurrentReward();
            UpdateJumpEndState();
        }

        private void BuildRewardWeek(
            LiveEventRewardWeekDefinition week,
            IReadOnlyList<int> rewardIndexes)
        {
            var weekId = week != null ? week.WeekId : "unassigned";
            var instance = CreateRuntimeObject(
                _rewardWeekTemplate,
                _rewardContent,
                "RewardWeek_" + weekId);
            var layout = instance.GetComponent<LayoutElement>();
            if (layout != null)
            {
                var width = RewardWeekHorizontalPadding +
                            rewardIndexes.Count * RewardCardWidth +
                            Math.Max(0, rewardIndexes.Count - 1) * RewardCardSpacing;
                layout.minWidth = width;
                layout.preferredWidth = width;
                layout.flexibleWidth = 0f;
                layout.preferredHeight = RewardWeekHeight;
                layout.flexibleHeight = 0f;
            }

            var background = instance.GetComponent<Image>();
            var track = FindImage(instance.transform, "RewardWeekTrack");
            var header = FindText(instance.transform, "RewardWeekHeader");
            var count = FindText(instance.transform, "RewardWeekCount");
            var countdown = FindText(instance.transform, "RewardWeekCountdown");
            var items = Find(instance.transform, "RewardWeekItems") as RectTransform;
            if (items == null)
            {
                return;
            }

            if (background != null)
            {
                background.color = week != null ? week.BackgroundColor : _service.Definition.PrimaryColor;
            }

            if (track != null)
            {
                track.color = week != null ? week.TrackColor : _service.Definition.AccentColor;
            }

            SetText(header, week != null ? week.Title : "PHẦN THƯỞNG SỰ KIỆN");
            var rewardIds = new List<string>(rewardIndexes.Count);
            for (var i = 0; i < rewardIndexes.Count; i++)
            {
                var rewardIndex = rewardIndexes[i];
                var reward = _service.Definition.Rewards[rewardIndex];
                rewardIds.Add(reward.RewardId);
                BuildRewardCard(items, reward, rewardIndex);
            }

            _rewardWeekBindings.Add(
                new RewardWeekBinding(
                    week,
                    instance.GetComponent<CanvasGroup>(),
                    header,
                    count,
                    countdown,
                    rewardIds));
        }

        private void BuildRewardCard(
            RectTransform parent,
            LiveEventRewardDefinition reward,
            int rewardIndex)
        {
            var instance = Instantiate(_rewardTemplate, parent, false);
            instance.name = "Reward_" + reward.RewardId;
            SetActive(instance, true);
            ConfigureFixedWidthLayoutElement(instance, RewardCardWidth, RewardCardHeight);
            var binding = new RewardBinding(
                reward,
                rewardIndex,
                instance,
                instance.GetComponent<CanvasGroup>(),
                FindImage(instance.transform, "RewardCard"),
                FindImage(instance.transform, "RewardIcon"),
                FindText(instance.transform, "RewardLevel"),
                FindText(instance.transform, "RewardTitle"),
                FindText(instance.transform, "RewardStatus"),
                Find(instance.transform, "RewardLockOverlay")?.gameObject,
                FindText(instance.transform, "RewardLockLabel"),
                Find(instance.transform, "RewardOwnedPanel")?.gameObject,
                FindText(instance.transform, "RewardOwnedLabel"),
                Find(instance.transform, "RewardCurrentHighlight")?.gameObject,
                FindImage(instance.transform, "RewardCandyIcon"),
                FindButton(instance.transform, "RewardCandyAction"),
                FindText(instance.transform, "RewardCandyActionLabel"),
                FindImage(instance.transform, "RewardPremiumIcon"),
                FindButton(instance.transform, "RewardPremiumAction"),
                FindText(instance.transform, "RewardPremiumActionLabel"));
            _rewardBindings.Add(binding);
            BindButton(
                binding.CandyButton,
                () => ClaimReward(reward.RewardId, CurrencyType.Candy));
            BindButton(
                binding.PremiumButton,
                () => ClaimReward(reward.RewardId, CurrencyType.Premium));
        }

        private void BuildShop()
        {
            if (_shopContent == null || _shopTemplate == null)
            {
                return;
            }

            var visibleItemCount = 0;
            var activeOffers = _service.ActiveShopOffers;
            for (var i = 0; i < activeOffers.Count; i++)
            {
                var offer = activeOffers[i];
                if (offer == null)
                {
                    continue;
                }

                if (visibleItemCount >= MaximumVisibleShopItems)
                {
                    break;
                }

                var instance = CreateRuntimeObject(_shopTemplate, _shopContent, "Shop_" + offer.OfferId);
                var binding = new ShopBinding(
                    offer,
                    instance,
                    FindImage(instance.transform, "ShopPreviewBackground"),
                    FindImage(instance.transform, "ShopIcon"),
                    FindText(instance.transform, "ShopTitle"),
                    FindText(instance.transform, "ShopRarity"),
                    FindImage(instance.transform, "ShopCurrencyIcon"),
                    FindButton(instance.transform, "ShopBuy"),
                    FindText(instance.transform, "ShopBuyLabel"));
                _shopBindings.Add(binding);
                BindButton(binding.Button, () => PurchaseShopOffer(offer.OfferId));
                visibleItemCount++;
            }

            if (_shopEmptyState != null)
            {
                SetActive(_shopEmptyState.gameObject, _shopBindings.Count == 0);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_shopContent);
        }

        private void BuildWheel()
        {
            var wheel = _service.ActiveWheel;
            if (_wheelContent == null || _wheelPrizeTemplate == null || wheel == null)
            {
                return;
            }

            SetText(_wheelTitle, wheel.DisplayName);
            SetText(_wheelTabLabel, wheel.NavigationLabel);
            ApplyWheelPresentation(wheel);
            var prizes = wheel.Prizes;
            _wheelDisplayPrizes.Clear();
            var totalWeight = 0;
            for (var i = 0; i < prizes.Count; i++)
            {
                totalWeight += prizes[i].Weight;
                InsertWheelPrizeByDisplayOrder(prizes[i]);
            }

            for (var i = 0; i < _wheelDisplayPrizes.Count; i++)
            {
                var prize = _wheelDisplayPrizes[i];
                var instance = CreateRuntimeObject(_wheelPrizeTemplate, _wheelContent, "WheelPrize_" + prize.PrizeId);
                _wheelRuntimeObjects.Add(instance);
                SetImage(FindImage(instance.transform, "WheelPrizeIcon"), prize.Icon);
                SetText(FindText(instance.transform, "WheelPrizeTitle"), prize.Title);
                var odds = totalWeight > 0 ? prize.Weight * 100f / totalWeight : 0f;
                SetText(FindText(instance.transform, "WheelPrizeOdds"), odds.ToString("0.#") + "%");

                var rewardRect = instance.GetComponent<RectTransform>();
                var sectorAngle = 360f / Mathf.Max(1, _wheelDisplayPrizes.Count);
                var angle = 90f - i * sectorAngle;
                var radians = angle * Mathf.Deg2Rad;
                rewardRect.anchorMin = new Vector2(0.5f, 0.5f);
                rewardRect.anchorMax = new Vector2(0.5f, 0.5f);
                rewardRect.pivot = new Vector2(0.5f, 0.5f);
                rewardRect.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * WheelRewardRadius;
                rewardRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);

                var capturedPrize = prize;
                BindButton(instance.GetComponent<Button>(), () => SelectWheelPrize(capturedPrize));
            }

            _selectedWheelPrize = FindWheelPrize(wheel.FeaturedPrizeId) ??
                                  (_wheelDisplayPrizes.Count > 0 ? _wheelDisplayPrizes[0] : null);
            RefreshWheelRareOdds(totalWeight);
            RefreshSelectedWheelPrize();
        }

        private void InsertWheelPrizeByDisplayOrder(LiveEventWheelPrizeDefinition prize)
        {
            if (prize == null)
            {
                return;
            }

            var insertIndex = _wheelDisplayPrizes.Count;
            for (var i = 0; i < _wheelDisplayPrizes.Count; i++)
            {
                if (prize.DisplayOrder < _wheelDisplayPrizes[i].DisplayOrder)
                {
                    insertIndex = i;
                    break;
                }
            }

            _wheelDisplayPrizes.Insert(insertIndex, prize);
        }

        private LiveEventWheelPrizeDefinition FindWheelPrize(string prizeId)
        {
            for (var i = 0; i < _wheelDisplayPrizes.Count; i++)
            {
                var prize = _wheelDisplayPrizes[i];
                if (string.Equals(prize.PrizeId, prizeId, StringComparison.Ordinal))
                {
                    return prize;
                }
            }

            return null;
        }

        private int GetWheelDisplayIndex(string prizeId)
        {
            for (var i = 0; i < _wheelDisplayPrizes.Count; i++)
            {
                if (string.Equals(_wheelDisplayPrizes[i].PrizeId, prizeId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        private void RefreshWheelRareOdds(int totalWeight)
        {
            var rarePrizes = new List<LiveEventWheelPrizeDefinition>(MaximumRareWheelRows);
            for (var i = 0; i < _wheelDisplayPrizes.Count; i++)
            {
                var prize = _wheelDisplayPrizes[i];
                if (prize.Weight > 3)
                {
                    continue;
                }

                var insertIndex = rarePrizes.Count;
                for (var rareIndex = 0; rareIndex < rarePrizes.Count; rareIndex++)
                {
                    if (prize.Weight > rarePrizes[rareIndex].Weight)
                    {
                        insertIndex = rareIndex;
                        break;
                    }
                }

                rarePrizes.Insert(insertIndex, prize);
            }

            for (var rowIndex = 0; rowIndex < MaximumRareWheelRows; rowIndex++)
            {
                var hasPrize = rowIndex < rarePrizes.Count;
                var prize = hasPrize ? rarePrizes[rowIndex] : null;
                var odds = hasPrize && totalWeight > 0
                    ? prize.Weight * 100f / totalWeight
                    : 0f;
                if (_wheelRareRewardNames != null && rowIndex < _wheelRareRewardNames.Length)
                {
                    SetText(_wheelRareRewardNames[rowIndex], hasPrize ? prize.Title : string.Empty);
                }

                if (_wheelRareRewardOdds != null && rowIndex < _wheelRareRewardOdds.Length)
                {
                    SetText(_wheelRareRewardOdds[rowIndex], hasPrize ? odds.ToString("0.#") + "%" : string.Empty);
                }
            }
        }

        private void RefreshSelectedWheelPrize()
        {
            if (_selectedWheelPrize == null)
            {
                SetImage(_wheelPreviewImage, null);
                SetText(_wheelRewardName, string.Empty);
                SetText(_wheelRewardType, string.Empty);
                return;
            }

            SetImage(_wheelPreviewImage, _selectedWheelPrize.Icon);
            SetText(_wheelRewardName, _selectedWheelPrize.Title);
            SetText(_wheelRewardType, _selectedWheelPrize.TypeLabel);
        }

        private void RefreshWheelCountdown()
        {
            if (_service == null)
            {
                return;
            }

            var remaining = _service.NextFreeSpinTimeRemaining;
            SetText(
                _wheelFreeSpinCountdown,
                remaining <= TimeSpan.Zero
                    ? "Free spins full"
                    : "Next free spin in " + FormatCompactEnglishDuration(remaining));
        }

        private void RefreshFastSpinVisual()
        {
            if (_fastSpinTrack != null)
            {
                _fastSpinTrack.sprite = _fastSpinEnabled ? _fastSpinOnSprite : _fastSpinOffSprite;
                _fastSpinTrack.color = _fastSpinEnabled
                    ? new Color32(67, 188, 88, 255)
                    : new Color32(27, 64, 111, 255);
            }

            if (_fastSpinKnob != null)
            {
                _fastSpinKnob.anchoredPosition = new Vector2(_fastSpinEnabled ? 24f : -24f, 0f);
            }

            SetText(_fastSpinStateText, _fastSpinEnabled ? "On" : "Off");
        }

        private void BuildBundles()
        {
            if (_bundleContent == null || _bundleTemplate == null)
            {
                return;
            }

            _selectedBundle = null;
            _featuredBundles.Clear();
            for (var i = 0; i < _service.Definition.Bundles.Count; i++)
            {
                var bundle = _service.Definition.Bundles[i];
                if (bundle == null || !_service.IsAvailable(bundle.StartHourOffset, bundle.EndHourOffset))
                {
                    continue;
                }

                _featuredBundles.Add(bundle);
                _selectedBundle ??= bundle;
            }

            if (_selectedBundle != null)
            {
                for (var i = 0; i < _selectedBundle.Grants.Count; i++)
                {
                    var grant = _selectedBundle.Grants[i];
                    var instance = CreateRuntimeObject(_bundleTemplate, _bundleContent, "BundleItem_" + i);
                    var presentation = ResolveBundleGrant(grant, _selectedBundle, i);
                    var binding = new BundleBinding(
                        grant,
                        instance,
                        FindImage(instance.transform, "BundleItemIcon"),
                        FindText(instance.transform, "BundleItemTitle"),
                        FindText(instance.transform, "BundleItemType"),
                        FindText(instance.transform, "BundleItemValue"));
                    SetImage(binding.Icon, presentation.Icon);
                    SetText(binding.Title, presentation.Title);
                    SetText(binding.Type, presentation.Type);
                    SetText(binding.Value, presentation.Value);
                    _bundleBindings.Add(binding);
                }
            }

            _featuredBundleIndex = _featuredBundles.Count > 1 ? 1 : 0;
            RefreshBundleDetails();
            RefreshFeaturedBundle();
        }

        private BundleGrantPresentation ResolveBundleGrant(
            LiveEventGrantDefinition grant,
            LiveEventBundleDefinition bundle,
            int grantIndex)
        {
            if (grant == null)
            {
                return new BundleGrantPresentation(null, "Unknown", "ITEM", string.Empty);
            }

            if (grant.Kind == LiveEventGrantKind.Currency)
            {
                var icon = grant.Currency switch
                {
                    CurrencyType.Coins => _shopCoinIcon,
                    CurrencyType.Premium => _shopPremiumIcon,
                    CurrencyType.Candy => _service?.Definition.CurrencyIcon,
                    _ => null
                };
                return new BundleGrantPresentation(
                    icon,
                    grant.Amount.ToString("N0"),
                    FormatCurrency(grant.Currency).ToUpperInvariant(),
                    "INCLUDED");
            }

            if (_bundleCatalog != null && _bundleCatalog.TryGet(grant.ItemId, out var definition))
            {
                var icon = string.Equals(grant.ItemId, "effect_fireworks", StringComparison.Ordinal)
                    ? _bundleFireworksIcon
                    : definition.Category switch
                    {
                        ItemCategory.Fur => bundle?.Banner,
                        ItemCategory.Ability => _bundleAbilityIcon,
                        ItemCategory.Effect => _bundleEffectIcon,
                        ItemCategory.Emote => _bundleEmoteIcon,
                        _ => null
                    };
                if (icon == null && grantIndex == 0)
                {
                    icon = bundle?.Banner;
                }

                return new BundleGrantPresentation(
                    icon,
                    definition.DisplayName,
                    FormatItemCategory(definition.Category),
                    definition.Price > 0L ? definition.Price.ToString("N0") : "EXCLUSIVE");
            }

            return new BundleGrantPresentation(
                grantIndex == 0 ? bundle?.Banner : null,
                HumanizeIdentifier(grant.ItemId),
                "ITEM",
                "EXCLUSIVE");
        }

        private static string FormatItemCategory(ItemCategory category) => category switch
        {
            ItemCategory.Fur => "FUR",
            ItemCategory.Ability => "ABILITY",
            ItemCategory.Effect => "EFFECT",
            ItemCategory.Emote => "EMOTE",
            _ => category.ToString().ToUpperInvariant()
        };

        private static string HumanizeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return "Item";
            }

            var words = identifier.Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..];
                }
            }

            return string.Join(" ", words);
        }

        private void Refresh()
        {
            if (_service == null)
            {
                return;
            }

            SetText(_eventTitle, _service.Definition.DisplayName);
            SetImage(_eventCurrencyIcon, _service.Definition.CurrencyIcon);
            if (_eventBanner != null)
            {
                SetImage(_eventBanner, _service.Definition.Banner);
                _eventBanner.color = _service.Definition.PrimaryColor;
            }

            SetText(_eventCurrency, _wallet.GetBalance(CurrencyType.Candy).ToString("N0"));
            RefreshQuests();
            RefreshRewards();
            RefreshShop();
            RefreshWheel();
            RefreshBundleDetails();
            RefreshCountdowns();
        }

        private void RefreshQuests()
        {
            for (var i = 0; i < _questBindings.Count; i++)
            {
                var binding = _questBindings[i];
                var progress = _service.GetQuestProgress(binding.Quest);
                var hardLocked = binding.Hard && !_service.IsHardQuestUnlocked;
                SetText(binding.Title, binding.Quest.Title);
                SetText(binding.Progress, $"{progress.Current}/{progress.Target}");
                SetImage(binding.RewardIcon, _service.Definition.CurrencyIcon);

                if (binding.Title != null)
                {
                    binding.Title.color = progress.Claimed
                        ? QuestClaimedTextColor
                        : hardLocked
                            ? QuestLockedTextColor
                            : QuestDefaultTextColor;
                }

                if (binding.Progress != null)
                {
                    binding.Progress.color = hardLocked
                        ? QuestLockedTextColor
                        : QuestProgressTextColor;
                }

                var showReward = !progress.Claimed;
                SetActive(binding.RewardIcon, showReward);
                SetButtonState(
                    binding.Button,
                    binding.ButtonLabel,
                    showReward && !hardLocked && progress.IsCompleted && _service.IsActive,
                    progress.Claimed
                        ? "Claimed"
                        : binding.Quest.CandyReward.ToString("N0"));

                if (binding.Background != null)
                {
                    var disabled = progress.Claimed || hardLocked;
                    binding.Background.sprite = disabled
                        ? binding.DisabledSprite ?? binding.NormalSprite
                        : binding.NormalSprite;
                    binding.Background.color = disabled && binding.DisabledSprite == null
                        ? new Color(0.48f, 0.43f, 0.58f, 1f)
                        : Color.white;
                }
            }

            var hardQuestLocked = !_service.IsHardQuestUnlocked;
            SetText(_hardQuestLock, hardQuestLocked ? "Finish the daily quests first" : string.Empty);
            SetActive(_hardQuestLock, hardQuestLocked);
            SetActive(_hardQuestLockIcon, hardQuestLocked);
        }

        private void RefreshRewards()
        {
            var candyBalance = _wallet.GetBalance(CurrencyType.Candy);
            var premiumBalance = _wallet.GetBalance(CurrencyType.Premium);
            for (var i = 0; i < _rewardBindings.Count; i++)
            {
                var binding = _rewardBindings[i];
                var reward = binding.Reward;
                var state = _service.GetRewardState(reward.RewardId);
                var unlockRemaining = _service.GetRewardUnlockTimeRemaining(reward.RewardId);
                var owned = state == LiveEventRewardState.Owned;
                var current = state == LiveEventRewardState.Current && unlockRemaining <= TimeSpan.Zero;
                var locked = !owned && !current;

                SetImage(binding.Icon, reward.Icon);
                SetImage(binding.CandyIcon, _service.Definition.CurrencyIcon);
                SetText(binding.Level, (binding.RewardIndex + 1).ToString());
                SetText(binding.Title, reward.Title);
                SetText(binding.CandyLabel, reward.CandyPrice.ToString("N0"));
                SetText(binding.PremiumLabel, reward.PremiumPrice.ToString("N0"));
                SetText(
                    binding.Status,
                    owned
                        ? "OWNED"
                        : current
                            ? "CHỌN CÁCH NHẬN"
                            : unlockRemaining > TimeSpan.Zero
                                ? "MỞ SAU " + FormatDuration(unlockRemaining)
                                : "CHƯA ĐẾN LƯỢT");

                SetActive(binding.LockOverlay, locked);
                SetText(
                    binding.LockLabel,
                    unlockRemaining > TimeSpan.Zero
                        ? FormatDuration(unlockRemaining)
                        : "CHƯA ĐẾN LƯỢT");
                SetActive(binding.OwnedPanel, owned);
                SetText(binding.OwnedLabel, "OWNED");
                SetActive(binding.CurrentHighlight, current);
                SetActive(binding.CandyButton, !owned);
                SetActive(binding.PremiumButton, current);
                SetButtonState(
                    binding.CandyButton,
                    binding.CandyLabel,
                    current && _service.IsActive && candyBalance >= reward.CandyPrice,
                    reward.CandyPrice.ToString("N0"));
                SetButtonState(
                    binding.PremiumButton,
                    binding.PremiumLabel,
                    current && _service.IsActive && premiumBalance >= reward.PremiumPrice,
                    reward.PremiumPrice.ToString("N0"));

                if (binding.Background != null)
                {
                    binding.Background.color = reward.CardColor;
                }

                if (binding.CanvasGroup != null)
                {
                    binding.CanvasGroup.alpha = locked ? 0.62f : 1f;
                }

                var candyRect = binding.CandyButton != null
                    ? binding.CandyButton.transform as RectTransform
                    : null;
                if (candyRect != null)
                {
                    candyRect.anchoredPosition = new Vector2(0f, current ? 82f : 31f);
                }
            }

            RefreshRewardWeeks();
            RefreshGrandPrize(candyBalance);
            RefreshClaimAll(premiumBalance);
            var claimed = CountClaimedRewards();
            SetText(
                _rewardProgress,
                $"{claimed}/{_service.Definition.Rewards.Count} • {GetCurrentWeekTitle()}");
            UpdateJumpEndState();
        }

        private void RefreshRewardWeeks()
        {
            for (var i = 0; i < _rewardWeekBindings.Count; i++)
            {
                var binding = _rewardWeekBindings[i];
                var claimed = 0;
                for (var rewardIndex = 0; rewardIndex < binding.RewardIds.Count; rewardIndex++)
                {
                    if (_service.IsRewardClaimed(binding.RewardIds[rewardIndex]))
                    {
                        claimed++;
                    }
                }

                var startsIn = binding.Week != null
                    ? _service.GetAvailabilityTimeRemaining(binding.Week.StartHourOffset)
                    : TimeSpan.Zero;
                SetText(
                    binding.Header,
                    binding.Week != null ? binding.Week.Title : "PHẦN THƯỞNG SỰ KIỆN");
                SetText(binding.Count, $"{claimed}/{binding.RewardIds.Count} BẬC");
                SetText(
                    binding.Countdown,
                    startsIn > TimeSpan.Zero ? "MỞ SAU " + FormatDuration(startsIn) : string.Empty);
                if (binding.CanvasGroup != null)
                {
                    binding.CanvasGroup.alpha = startsIn > TimeSpan.Zero ? 0.72f : 1f;
                }
            }
        }

        private void RefreshGrandPrize(long candyBalance)
        {
            if (_grandPrize == null)
            {
                return;
            }

            var state = _service.GetRewardState(_grandPrize.RewardId);
            var remaining = _service.GetRewardUnlockTimeRemaining(_grandPrize.RewardId);
            var owned = state == LiveEventRewardState.Owned;
            var current = state == LiveEventRewardState.Current && remaining <= TimeSpan.Zero;
            SetImage(_grandPrizePriceIcon, _service.Definition.CurrencyIcon);
            SetText(
                _grandPrizeStatus,
                owned
                    ? "OWNED"
                    : current
                        ? "SẴN SÀNG"
                        : remaining > TimeSpan.Zero
                            ? "MỞ SAU " + FormatDuration(remaining)
                            : "CHƯA ĐẾN LƯỢT");
            SetActive(_grandPrizeAction, true);
            SetActive(_grandPrizePriceIcon, !owned);
            SetButtonState(
                _grandPrizeAction,
                _grandPrizeActionLabel,
                current && _service.IsActive && candyBalance >= _grandPrize.CandyPrice,
                owned ? "OWNED" : _grandPrize.CandyPrice.ToString("N0"));
            SetActive(_grandPrizePremiumAction, false);
        }

        private void RefreshClaimAll(long premiumBalance)
        {
            var quote = _service.GetClaimAllQuote(CurrencyType.Premium);
            SetButtonState(
                _claimAllRewards,
                _claimAllRewardsLabel,
                _service.IsActive && quote.HasRewards && premiumBalance >= quote.TotalPrice,
                quote.HasRewards
                    ? $"NHẬN TẤT CẢ ({quote.RewardCount}) • {quote.TotalPrice:N0} XU NẠP"
                    : "ĐÃ NHẬN TẤT CẢ QUÀ ĐANG MỞ");
        }

        private int CountClaimedRewards()
        {
            var claimed = 0;
            var rewards = _service.Definition.Rewards;
            for (var i = 0; i < rewards.Count; i++)
            {
                if (rewards[i] != null && _service.IsRewardClaimed(rewards[i].RewardId))
                {
                    claimed++;
                }
            }

            return claimed;
        }

        private string GetCurrentWeekTitle()
        {
            var currentIndex = _service.CurrentRewardIndex;
            if (currentIndex < 0 || currentIndex >= _service.Definition.Rewards.Count)
            {
                return "HOÀN THÀNH";
            }

            var weekId = _service.Definition.Rewards[currentIndex].WeekId;
            var weeks = _service.Definition.RewardWeeks;
            for (var i = 0; i < weeks.Count; i++)
            {
                if (weeks[i] != null && string.Equals(weeks[i].WeekId, weekId, StringComparison.Ordinal))
                {
                    return weeks[i].Title;
                }
            }

            return "PHẦN THƯỞNG SỰ KIỆN";
        }

        private void RefreshShop()
        {
            for (var i = 0; i < _shopBindings.Count; i++)
            {
                var binding = _shopBindings[i];
                var purchased = _service.IsShopOfferPurchased(binding.Offer.OfferId);
                SetShopPreview(binding.PreviewBackground, binding.Offer.Rarity);
                SetImage(binding.Icon, binding.Offer.Icon);
                SetText(binding.Title, binding.Offer.Title);
                SetText(binding.Rarity, binding.Offer.Rarity);
                if (binding.Rarity != null)
                {
                    binding.Rarity.color = GetShopRarityColor(binding.Offer.Rarity);
                }

                SetImage(binding.CurrencyIcon, GetShopCurrencyIcon(binding.Offer.PriceCurrency));
                SetButtonState(
                    binding.Button,
                    binding.ButtonLabel,
                    !purchased,
                    purchased
                        ? "Đã mua"
                        : $"{binding.Offer.Price:N0}");
            }

            RefreshShopCountdown();
        }

        private void RefreshShopCountdown()
        {
            SetText(
                _shopCountdown,
                _shopBindings.Count == 0
                    ? "00:00:00"
                    : FormatDuration(_service.ShopRotationTimeRemaining));
        }

        private void SetShopPreview(Image preview, string rarity)
        {
            if (preview == null)
            {
                return;
            }

            var sprite = GetShopPreviewSprite(rarity);
            if (preview.sprite != sprite)
            {
                preview.sprite = sprite;
            }

            if (preview.type != Image.Type.Sliced)
            {
                preview.type = Image.Type.Sliced;
            }

            if (preview.preserveAspect)
            {
                preview.preserveAspect = false;
            }

            var enabled = sprite != null;
            if (preview.enabled != enabled)
            {
                preview.enabled = enabled;
            }
        }

        private Sprite GetShopPreviewSprite(string rarity)
        {
            if (ContainsRarity(rarity, "MYTHIC"))
            {
                return _shopMythicPreviewSprite;
            }

            if (ContainsRarity(rarity, "LEGENDARY"))
            {
                return _shopLegendaryPreviewSprite;
            }

            if (ContainsRarity(rarity, "EPIC"))
            {
                return _shopEpicPreviewSprite;
            }

            if (ContainsRarity(rarity, "RARE"))
            {
                return _shopRarePreviewSprite;
            }

            return _shopCommonPreviewSprite;
        }

        private Sprite GetShopCurrencyIcon(CurrencyType currency) => currency switch
        {
            CurrencyType.Coins => _shopCoinIcon,
            CurrencyType.Candy => _service.Definition.CurrencyIcon,
            CurrencyType.Premium => _shopPremiumIcon,
            _ => null
        };

        private static Color GetShopRarityColor(string rarity)
        {
            if (ContainsRarity(rarity, "MYTHIC"))
            {
                return new Color32(255, 83, 135, 255);
            }

            if (ContainsRarity(rarity, "LEGENDARY"))
            {
                return new Color32(255, 211, 39, 255);
            }

            if (ContainsRarity(rarity, "EPIC"))
            {
                return new Color32(205, 135, 255, 255);
            }

            if (ContainsRarity(rarity, "RARE"))
            {
                return new Color32(77, 211, 255, 255);
            }

            return Color.white;
        }

        private static bool ContainsRarity(string rarity, string value) =>
            !string.IsNullOrWhiteSpace(rarity) &&
            rarity.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

        private void ApplyWheelPresentation(LiveEventWheelDefinition wheel)
        {
            var presentation = wheel?.Presentation;
            if (presentation == null)
            {
                return;
            }

            if (_wheelBackground != null && presentation.Background != null)
            {
                SetImage(_wheelBackground, presentation.Background);
                _wheelBackground.color = Color.white;
            }

            if (_wheelDecoration != null)
            {
                SetImage(_wheelDecoration, presentation.HeaderDecoration);
            }

            var wheelPanel = _tabPanels != null && _tabPanels.Length > 3
                ? _tabPanels[3]?.transform
                : null;
            if (wheelPanel == null)
            {
                return;
            }

            var graphics = wheelPanel.GetComponentsInChildren<EventWheelGraphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                graphics[i].ApplyPalette(
                    presentation.PrimaryColor,
                    presentation.SecondaryColor,
                    presentation.AccentColor,
                    presentation.SpecialSectorColor);
            }

            if (_wheelTitle != null)
            {
                _wheelTitle.color = presentation.AccentColor;
            }
        }

        private void RefreshWheel()
        {
            var wheel = _service.ActiveWheel;
            if (wheel == null)
            {
                SetText(_wheelTitle, "Wheel unavailable");
                return;
            }

            SetText(_wheelTitle, wheel.DisplayName);
            SetText(_wheelTabLabel, wheel.NavigationLabel);

            if (_wheelPitySlider != null)
            {
                _wheelPitySlider.minValue = 0f;
                _wheelPitySlider.maxValue = wheel.PityThreshold;
                _wheelPitySlider.SetValueWithoutNotify(_service.WheelPity);
            }

            SetText(_wheelPityText, $"Pity {_service.WheelPity}/{wheel.PityThreshold}");
            var freeSpins = _service.FreeSpinsRemaining;
            var paidSpins = _service.PaidSpinsRemaining;
            SetButtonState(
                _freeSpinButton,
                _freeSpinLabel,
                !_wheelInteractionLocked && _service.IsActive && (freeSpins > 0 || paidSpins > 0L),
                freeSpins > 0
                    ? $"Spin ({freeSpins})"
                    : $"Spin ({paidSpins:N0} paid)");
            SetButtonState(
                _paidSpinButton,
                _paidSpinLabel,
                !_wheelInteractionLocked && _service.IsActive && freeSpins == 0 && paidSpins > 0L,
                $"Paid spins • {paidSpins:N0}");

            var buyOne = _service.GetWheelPackQuote(1);
            var buyThree = _service.GetWheelPackQuote(3);
            var buyTen = _service.GetWheelPackQuote(10);
            SetButtonState(_buyOneSpinButton, _buyOneSpinPrice, !_wheelInteractionLocked, buyOne.PremiumPrice.ToString("N0"));
            SetButtonState(_buyThreeSpinsButton, _buyThreeSpinsPrice, !_wheelInteractionLocked, buyThree.PremiumPrice.ToString("N0"));
            SetButtonState(_buyTenSpinsButton, _buyTenSpinsPrice, !_wheelInteractionLocked, buyTen.PremiumPrice.ToString("N0"));
            SetText(_buyOneOriginalPrice, wheel.BuyOneOriginalPrice.ToString("N0"));
            SetActive(_buyOneDailyDealBadge, buyOne.UsesDailyDeal);
            SetActive(_buyOneOriginalPrice, buyOne.UsesDailyDeal);
            SetActive(_buyTenBestValueBadge, wheel.BestValueEnabled);
            RefreshSelectedWheelPrize();
            RefreshWheelCountdown();
            RefreshFastSpinVisual();
        }

        private void RefreshBundleDetails()
        {
            if (_selectedBundle == null)
            {
                SetText(_bundleTitle, "No bundle available");
                SetText(_bundleDescription, "The next limited bundle will appear in a future rotation.");
                SetText(_bundleContents, string.Empty);
                SetText(_bundleCountdown, string.Empty);
                SetText(_bundleOriginalPrice, string.Empty);
                SetText(_bundleDiscount, string.Empty);
                SetButtonState(_bundlePurchaseButton, _bundlePurchaseLabel, false, "Unavailable");
                SetImage(_bundleBanner, null);
                SetImage(_bundlePriceIcon, null);
                RefreshBundleCountdownCells(TimeSpan.Zero);
                return;
            }

            EventBundlePresentationApplicator.Apply(
                _selectedBundle.Presentation,
                _bundlePanelBackground,
                _bundleDetailsBackground,
                _bundleFeaturedBackground,
                _bundleTitle,
                _bundleFeaturedTitle,
                _bundleDiscount,
                _bundlePurchaseButton);
            SetImage(_bundleBanner, _selectedBundle.Banner);
            SetText(_bundleTitle, _selectedBundle.Title);
            SetText(_bundleDescription, _selectedBundle.Description);
            SetText(_bundleContents, _selectedBundle.Grants.Count + " rewards included");
            SetText(_bundleCountdown, "Ends in");
            SetText(_bundleOriginalPrice, _selectedBundle.OriginalPrice.ToString("N0"));
            SetImage(
                _bundlePriceIcon,
                _selectedBundle.PriceCurrency == CurrencyType.Coins ? _shopCoinIcon : _shopPremiumIcon);
            var purchased = _service.IsBundlePurchased(_selectedBundle.BundleId);
            var discount = _selectedBundle.OriginalPrice > 0L
                ? Mathf.Clamp(
                    Mathf.RoundToInt((1f - (float)_selectedBundle.SalePrice / _selectedBundle.OriginalPrice) * 100f),
                    0,
                    99)
                : 0;
            SetText(_bundleDiscount, discount > 0 ? discount + "% off" : string.Empty);
            SetButtonState(
                _bundlePurchaseButton,
                _bundlePurchaseLabel,
                !purchased,
                purchased
                    ? "Purchased"
                    : _selectedBundle.SalePrice.ToString("N0"));
            RefreshBundleCountdownCells(
                _service.GetAvailabilityTimeRemaining(_selectedBundle.EndHourOffset));
            RefreshFeaturedBundle();
        }

        private void RefreshFeaturedBundle()
        {
            var featured = GetFeaturedBundle();
            if (featured == null)
            {
                SetImage(_bundleFeaturedBanner, null);
                SetText(_bundleFeaturedTitle, "No featured bundle");
                SetText(_bundleFeaturedStatus, string.Empty);
                SetButtonState(_bundleViewButton, _bundleViewLabel, false, "View");
            }
            else
            {
                EventBundlePresentationApplicator.ApplyFeatured(
                    featured.Presentation,
                    _bundleFeaturedBackground,
                    _bundleFeaturedTitle,
                    _bundleFeaturedStatus);

                SetImage(_bundleFeaturedBanner, featured.Banner);
                SetText(_bundleFeaturedTitle, featured.Title);
                SetText(
                    _bundleFeaturedStatus,
                    _service.IsBundlePurchased(featured.BundleId) ? "SOLD OUT" : "LIMITED EDITION");
                SetButtonState(_bundleViewButton, _bundleViewLabel, true, "View");
            }

            for (var i = 0; i < MaximumBundlePreviewRewards; i++)
            {
                var hasReward = featured != null && i < featured.Grants.Count;
                var icon = i < _bundleFeaturedRewardIcons.Length ? _bundleFeaturedRewardIcons[i] : null;
                var label = i < _bundleFeaturedRewardLabels.Length ? _bundleFeaturedRewardLabels[i] : null;
                SetActive(icon, hasReward);
                SetActive(label, hasReward);
                if (!hasReward)
                {
                    continue;
                }

                var presentation = ResolveBundleGrant(featured.Grants[i], featured, i);
                SetImage(icon, presentation.Icon);
                SetText(label, presentation.Title);
            }

            for (var i = 0; i < _bundlePageDots.Length; i++)
            {
                var dot = _bundlePageDots[i];
                var active = i < _featuredBundles.Count;
                SetActive(dot, active);
                if (dot != null && active)
                {
                    dot.color = i == _featuredBundleIndex
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0.38f);
                }
            }

            var multiple = _featuredBundles.Count > 1;
            if (_bundlePreviousButton != null) { _bundlePreviousButton.interactable = multiple; }
            if (_bundleNextButton != null) { _bundleNextButton.interactable = multiple; }
        }

        private LiveEventBundleDefinition GetFeaturedBundle() =>
            _featuredBundles.Count == 0
                ? null
                : _featuredBundles[Mathf.Clamp(_featuredBundleIndex, 0, _featuredBundles.Count - 1)];

        private void RefreshBundleCountdownCells(TimeSpan remaining)
        {
            var values = new[]
            {
                Math.Max(0, (int)remaining.TotalDays),
                Math.Max(0, remaining.Hours),
                Math.Max(0, remaining.Minutes),
                Math.Max(0, remaining.Seconds)
            };
            for (var i = 0; i < _bundleCountdownCells.Length && i < values.Length; i++)
            {
                SetText(_bundleCountdownCells[i], values[i].ToString("00"));
            }
        }

        private void RefreshCountdowns()
        {
            if (_service == null)
            {
                return;
            }

            SetText(
                _eventCountdown,
                _service.IsActive
                    ? "Kết thúc sau " + FormatDuration(_service.EventTimeRemaining)
                    : "Sự kiện chưa mở hoặc đã kết thúc");
            switch (_activeTab)
            {
                case 0:
                    SetText(_rewardEndCountdown, GetRewardEndCountdownText());
                    break;
                case 1:
                    SetText(
                        _dailyResetCountdown,
                        "New quests in " +
                        FormatCompactEnglishDuration(_service.DailyResetTimeRemaining));
                    break;
                case 2:
                    RefreshShopCountdown();
                    break;
                case 3:
                    RefreshWheelCountdown();
                    break;
                case 4 when _selectedBundle != null:
                    SetText(_bundleCountdown, "Ends in");
                    RefreshBundleCountdownCells(
                        _service.GetAvailabilityTimeRemaining(_selectedBundle.EndHourOffset));
                    break;
            }
        }

        private void SwitchTab(int tabIndex)
        {
            var previousTab = _activeTab;
            _activeTab = Mathf.Clamp(tabIndex, 0, TabCount - 1);
            var tabChanged = previousTab != _activeTab;
            SetStandalonePresentationChrome(_activeTab == 3 || _activeTab == 4);
            for (var i = 0; i < _tabPanels.Length; i++)
            {
                if (_tabPanels[i] != null)
                {
                    SetActive(_tabPanels[i], i == _activeTab);
                }
            }

            for (var i = 0; i < _tabBackgrounds.Length; i++)
            {
                if (_tabBackgrounds[i] != null)
                {
                    _tabBackgrounds[i].sprite = i == _activeTab ? _activeTabSprite : _inactiveTabSprite;
                }
            }

            var isWindowVisible = IsEventWindowVisible();
            if (tabChanged &&
                isWindowVisible &&
                _activeTab < _tabPanels.Length &&
                _tabPanels[_activeTab] != null)
            {
                var transition = _tabPanels[_activeTab].GetComponent<UITabTransition>();
                transition?.PlayIn(_activeTab > previousTab ? 1 : -1);
            }

            if (_service != null && isWindowVisible)
            {
                RefreshActiveTab();
            }
        }

        private void SetStandalonePresentationChrome(bool standaloneActive)
        {
            CacheEventChrome();
            if (_eventWindowBackground != null)
            {
                _eventWindowBackground.enabled = !standaloneActive;
            }

            SetActive(_eventBannerChrome, !standaloneActive);
            SetActive(_eventSidebarChrome, !standaloneActive);
            SetActive(_eventCloseChrome, !standaloneActive);
        }

        private void CacheEventChrome()
        {
            if (_eventWindowBackground != null || _tabPanels == null || _tabPanels.Length <= 3 || _tabPanels[3] == null)
            {
                return;
            }

            var window = _tabPanels[3].transform.parent;
            if (window == null)
            {
                return;
            }

            _eventWindowBackground = window.GetComponent<Image>();
            _eventBannerChrome = Find(window, "EventBanner")?.gameObject;
            _eventSidebarChrome = Find(window, "EventSidebar")?.gameObject;
            _eventCloseChrome = Find(window, "Close_Event")?.gameObject;
        }

        private void RefreshActiveTab()
        {
            switch (_activeTab)
            {
                case 0:
                    RefreshRewards();
                    break;
                case 1:
                    RefreshQuests();
                    break;
                case 2:
                    RefreshShop();
                    break;
                case 3:
                    RefreshWheel();
                    break;
                case 4:
                    RefreshBundleDetails();
                    break;
            }

            RefreshCountdowns();
        }

        private void ClaimQuest(string questId)
        {
            _service.TryClaimQuest(questId);
            Refresh();
        }

        private void ClaimReward(string rewardId, CurrencyType paymentCurrency)
        {
            _service.TryClaimReward(rewardId, paymentCurrency);
            Refresh();
        }

        private void ClaimAllRewards()
        {
            _service.TryClaimAllRewards(CurrencyType.Premium);
            Refresh();
        }

        private void PurchaseShopOffer(string offerId)
        {
            _service.TryPurchaseShopOffer(offerId);
            Refresh();
        }

        private void Spin(bool free)
        {
            RequestWheelSpin(free);
        }

        private void RequestPrimaryWheelSpin()
        {
            if (_service == null)
            {
                return;
            }

            RequestWheelSpin(_service.FreeSpinsRemaining > 0);
        }

        private void RequestWheelSpin(bool useFreeSpin)
        {
            if (_service == null || _wheelInteractionLocked || (_wheelAnimator != null && _wheelAnimator.IsSpinning))
            {
                return;
            }

            _wheelInteractionLocked = true;
            RefreshWheel();
            var result = _service.TrySpin();
            if (!result.Succeeded || result.Prize == null)
            {
                _wheelInteractionLocked = false;
                SetText(_wheelResultText, FormatOperationResult(result.OperationResult));
                RefreshWheel();
                return;
            }

            _selectedWheelPrize = result.Prize;
            SetText(_wheelResultText, "Spinning...");
            RefreshSelectedWheelPrize();
            var sectorIndex = GetWheelDisplayIndex(result.Prize.PrizeId);
            var played = _wheelAnimator != null &&
                         _wheelAnimator.Play(
                             sectorIndex,
                             _wheelDisplayPrizes.Count,
                             _fastSpinEnabled,
                             () => CompleteWheelSpin(result.Prize));
            if (!played)
            {
                CompleteWheelSpin(result.Prize);
            }
        }

        private void CompleteWheelSpin(LiveEventWheelPrizeDefinition prize)
        {
            _wheelInteractionLocked = false;
            _selectedWheelPrize = prize;
            SetText(_wheelResultText, "You received: " + prize.Title);
            Refresh();
        }

        private void CancelWheelPresentation()
        {
            _wheelAnimator?.Cancel();
            _wheelInteractionLocked = false;
        }

        private void SelectWheelPrize(LiveEventWheelPrizeDefinition prize)
        {
            if (prize == null || _wheelInteractionLocked)
            {
                return;
            }

            _selectedWheelPrize = prize;
            RefreshSelectedWheelPrize();
        }

        private void RequestWheelPreview()
        {
            if (_selectedWheelPrize == null)
            {
                return;
            }

            if (WheelPreviewRequested == null)
            {
                SetText(_wheelResultText, "Preview hook is ready for " + _selectedWheelPrize.Title);
                return;
            }

            WheelPreviewRequested.Invoke(_selectedWheelPrize.PrizeId);
        }

        private void RequestWheelOdds()
        {
            if (WheelOddsRequested == null)
            {
                SetText(_wheelResultText, "Odds view hook is ready");
                return;
            }

            WheelOddsRequested.Invoke();
        }

        private void RequestWheelPurchase(int spinCount)
        {
            if (_wheelInteractionLocked)
            {
                return;
            }

            var result = _service.TryPurchaseWheelPack(spinCount);
            if (result != LiveEventOperationResult.Success)
            {
                SetText(_wheelResultText, FormatOperationResult(result));
                RefreshWheel();
                return;
            }

            WheelPurchaseRequested?.Invoke(spinCount);
            SetText(_wheelResultText, $"Added {spinCount} paid spin{(spinCount == 1 ? string.Empty : "s")}");
            RefreshWheel();
        }

        private void HandleFastSpinChanged(bool enabledState)
        {
            _fastSpinEnabled = enabledState;
            RefreshFastSpinVisual();
        }

        private void MoveFeaturedBundle(int direction)
        {
            if (_featuredBundles.Count <= 1)
            {
                return;
            }

            _featuredBundleIndex = (_featuredBundleIndex + direction) % _featuredBundles.Count;
            if (_featuredBundleIndex < 0)
            {
                _featuredBundleIndex += _featuredBundles.Count;
            }

            RefreshFeaturedBundle();
        }

        private void RequestFeaturedBundlePreview()
        {
            var featured = GetFeaturedBundle();
            if (featured == null)
            {
                return;
            }

            if (BundlePreviewRequested == null)
            {
                SetText(_bundleViewLabel, "3D preview ready");
                return;
            }

            BundlePreviewRequested.Invoke(featured.BundleId);
        }

        private void PurchaseSelectedBundle()
        {
            if (_selectedBundle == null)
            {
                return;
            }

            _service.TryPurchaseBundle(_selectedBundle.BundleId);
            Refresh();
        }

        private void JumpToLastReward()
        {
            if (_rewardScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _rewardScroll.horizontalNormalizedPosition = 1f;
                UpdateJumpEndState();
            }
        }

        private void FocusCurrentReward()
        {
            if (_rewardScroll == null || _regularRewardCount <= FullyVisibleRewardCards)
            {
                return;
            }

            var currentIndex = Mathf.Clamp(_service.CurrentRewardIndex, 0, _regularRewardCount - 1);
            var maxFirstVisible = Math.Max(1, _regularRewardCount - FullyVisibleRewardCards);
            _rewardScroll.horizontalNormalizedPosition = Mathf.Clamp01((float)currentIndex / maxFirstVisible);
        }

        private void HandleRewardScrollChanged(Vector2 position) => UpdateJumpEndState();

        private void UpdateJumpEndState()
        {
            if (_jumpToLastReward == null)
            {
                return;
            }

            var maxFirstVisible = Math.Max(0, _regularRewardCount - FullyVisibleRewardCards);
            var normalizedPosition = _rewardScroll != null
                ? _rewardScroll.horizontalNormalizedPosition
                : 0f;
            var firstVisible = Mathf.RoundToInt(normalizedPosition * maxFirstVisible);
            var lastFullyVisible = Math.Min(
                _regularRewardCount,
                firstVisible + FullyVisibleRewardCards);
            var remaining = Math.Max(0, _regularRewardCount - lastFullyVisible);
            _jumpToLastReward.interactable = remaining > 0;
            SetText(
                _jumpToLastRewardLabel,
                remaining > 0 ? $"»\n+{remaining}" : "✓\nCUỐI");
        }

        private void HandleServiceChanged()
        {
            if (!IsEventWindowVisible())
            {
                _refreshPending = true;
                return;
            }

            Refresh();
        }

        private void HandleBalanceChanged(CurrencyType currency, long balance)
        {
            if (!IsEventWindowVisible())
            {
                _refreshPending = true;
                return;
            }

            if (currency == CurrencyType.Candy)
            {
                SetText(_eventCurrency, balance.ToString("N0"));
            }

            if ((currency == CurrencyType.Candy || currency == CurrencyType.Premium) && _service != null)
            {
                RefreshRewards();
            }
        }

        private string GetRewardEndCountdownText()
        {
            var nextRelease = TimeSpan.MaxValue;
            var rewards = _service.Definition.Rewards;
            for (var i = 0; i < _regularRewardCount && i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null || _service.IsRewardClaimed(reward.RewardId))
                {
                    continue;
                }

                var remaining = _service.GetRewardUnlockTimeRemaining(reward.RewardId);
                if (remaining > TimeSpan.Zero && remaining < nextRelease)
                {
                    nextRelease = remaining;
                }
            }

            return nextRelease < TimeSpan.MaxValue
                ? "THÊM PHẦN THƯỞNG\nSAU " + FormatDuration(nextRelease)
                : "SỰ KIỆN CÒN\n" + FormatDuration(_service.EventTimeRemaining);
        }

        private ulong BuildAvailabilityFingerprint()
        {
            if (_service == null)
            {
                return 0UL;
            }

            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var result = offsetBasis;
            result = AddStringToFingerprint(result, _service.ActiveWheel?.WheelId, prime);
            var shopOffers = _service.ActiveShopOffers;
            result = (result ^ (uint)shopOffers.Count) * prime;
            for (var i = 0; i < shopOffers.Count; i++)
            {
                var offer = shopOffers[i];
                var available = offer != null;
                result = (result ^ (available ? 1UL : 0UL)) * prime;
                if (offer != null)
                {
                    result = AddStringToFingerprint(result, offer.OfferId, prime);
                }
            }

            var bundles = _service.Definition.Bundles;
            result = (result ^ 0xFFUL) * prime;
            result = (result ^ (uint)bundles.Count) * prime;
            for (var i = 0; i < bundles.Count; i++)
            {
                var bundle = bundles[i];
                var available = bundle != null &&
                                _service.IsAvailable(bundle.StartHourOffset, bundle.EndHourOffset);
                result = (result ^ (available ? 1UL : 0UL)) * prime;
                if (bundle != null)
                {
                    result = AddStringToFingerprint(result, bundle.BundleId, prime);
                }
            }

            return result;
        }

        private static ulong AddStringToFingerprint(ulong fingerprint, string value, ulong prime)
        {
            if (string.IsNullOrEmpty(value))
            {
                return fingerprint;
            }

            for (var i = 0; i < value.Length; i++)
            {
                fingerprint = (fingerprint ^ value[i]) * prime;
            }

            return fingerprint;
        }

        private GameObject CreateRuntimeObject(GameObject template, Transform parent, string objectName)
        {
            var instance = Instantiate(template, parent, false);
            instance.name = objectName;
            SetActive(instance, true);
            _runtimeObjects.Add(instance);
            return instance;
        }

        private void DestroyRuntimeObjects()
        {
            for (var i = 0; i < _runtimeObjects.Count; i++)
            {
                if (_runtimeObjects[i] != null)
                {
                    Destroy(_runtimeObjects[i]);
                }
            }

            _runtimeObjects.Clear();
            _questBindings.Clear();
            _rewardBindings.Clear();
            _rewardWeekBindings.Clear();
            _shopBindings.Clear();
            _bundleBindings.Clear();
            _wheelRuntimeObjects.Clear();
            _wheelDisplayPrizes.Clear();
            _selectedWheelPrize = null;
        }

        private void DestroyBindings<TBinding>(IList<TBinding> bindings) where TBinding : IRuntimeBinding
        {
            for (var i = bindings.Count - 1; i >= 0; i--)
            {
                var root = bindings[i].RuntimeRoot;
                if (root != null)
                {
                    _runtimeObjects.Remove(root);
                    Destroy(root);
                }
            }

            bindings.Clear();
        }

        private void DestroyWheelRuntimeObjects()
        {
            for (var i = _wheelRuntimeObjects.Count - 1; i >= 0; i--)
            {
                var root = _wheelRuntimeObjects[i];
                if (root == null)
                {
                    continue;
                }

                _runtimeObjects.Remove(root);
                Destroy(root);
            }

            _wheelRuntimeObjects.Clear();
            _wheelDisplayPrizes.Clear();
            _selectedWheelPrize = null;
        }

        private void BindButton(Button button, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(action);
            _runtimeListeners.Add(new ButtonListener(button, action));
        }

        private static TMP_Text FindText(Transform root, string objectName) =>
            Find(root, objectName)?.GetComponent<TMP_Text>();

        private static Image FindImage(Transform root, string objectName) =>
            Find(root, objectName)?.GetComponent<Image>();

        private static Button FindButton(Transform root, string objectName) =>
            Find(root, objectName)?.GetComponent<Button>();

        private static Transform Find(Transform root, string objectName)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName)
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            var target = new GameObject(objectName, typeof(RectTransform));
            var rect = target.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void StretchRect(
            RectTransform rect,
            float left,
            float top,
            float right,
            float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void SetText(TMP_Text target, string value)
        {
            RuntimeUiState.SetText(target, value);
        }

        private static void SetImage(Image target, Sprite sprite)
        {
            if (target == null)
            {
                return;
            }

            if (target.sprite != sprite)
            {
                target.sprite = sprite;
            }

            var enabled = sprite != null;
            if (target.enabled != enabled)
            {
                target.enabled = enabled;
            }

            if (!target.preserveAspect)
            {
                target.preserveAspect = true;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void SetActive(Component target, bool active)
        {
            if (target != null)
            {
                SetActive(target.gameObject, active);
            }
        }

        private static void SetButtonState(Button button, TMP_Text label, bool interactable, string text)
        {
            if (button != null)
            {
                if (button.interactable != interactable)
                {
                    button.interactable = interactable;
                }
            }

            SetText(label, text);
        }

        private static string FormatCurrency(CurrencyType currency) => currency switch
        {
            CurrencyType.Coins => "xu",
            CurrencyType.Candy => "kẹo",
            CurrencyType.Premium => "xu nạp",
            _ => currency.ToString()
        };

        private static string FormatOperationResult(LiveEventOperationResult result) => result switch
        {
            LiveEventOperationResult.InsufficientFunds => "Không đủ tiền tệ",
            LiveEventOperationResult.Locked => "Phần thưởng đang bị khóa",
            LiveEventOperationResult.EventInactive => "Sự kiện hiện không hoạt động",
            LiveEventOperationResult.AlreadyClaimed => "Phần thưởng đã được nhận",
            LiveEventOperationResult.AlreadyPurchased => "Vật phẩm đã được mua",
            LiveEventOperationResult.NoSpinAvailable => "Không còn lượt quay",
            LiveEventOperationResult.PersistenceFailed => "Đã cập nhật trong phiên nhưng chưa lưu được",
            _ => "Không thể thực hiện thao tác"
        };

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                return "00:00:00";
            }

            return duration.TotalDays >= 1d
                ? $"{(int)duration.TotalDays}n {duration.Hours:00}g {duration.Minutes:00}p"
                : $"{duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }

        private static string FormatCompactEnglishDuration(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                return "0m";
            }

            if (duration.TotalDays >= 1d)
            {
                return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";
            }

            return duration.TotalHours >= 1d
                ? $"{duration.Hours}h {duration.Minutes}m"
                : $"{duration.Minutes}m {duration.Seconds}s";
        }

        private readonly struct ButtonListener
        {
            public ButtonListener(Button button, UnityAction action)
            {
                Button = button;
                Action = action;
            }

            public Button Button { get; }
            public UnityAction Action { get; }
        }

        private interface IRuntimeBinding
        {
            GameObject RuntimeRoot { get; }
        }

        private sealed class QuestBinding
        {
            public QuestBinding(
                LiveEventQuestDefinition quest,
                bool hard,
                TMP_Text title,
                TMP_Text progress,
                Button button,
                Image background,
                Sprite normalSprite,
                Sprite disabledSprite,
                Image rewardIcon,
                TMP_Text buttonLabel)
            {
                Quest = quest;
                Hard = hard;
                Title = title;
                Progress = progress;
                Button = button;
                Background = background;
                NormalSprite = normalSprite;
                DisabledSprite = disabledSprite;
                RewardIcon = rewardIcon;
                ButtonLabel = buttonLabel;
            }

            public LiveEventQuestDefinition Quest { get; }
            public bool Hard { get; }
            public TMP_Text Title { get; }
            public TMP_Text Progress { get; }
            public Button Button { get; }
            public Image Background { get; }
            public Sprite NormalSprite { get; }
            public Sprite DisabledSprite { get; }
            public Image RewardIcon { get; }
            public TMP_Text ButtonLabel { get; }
        }

        private sealed class RewardBinding
        {
            public RewardBinding(
                LiveEventRewardDefinition reward,
                int rewardIndex,
                GameObject root,
                CanvasGroup canvasGroup,
                Image background,
                Image icon,
                TMP_Text level,
                TMP_Text title,
                TMP_Text status,
                GameObject lockOverlay,
                TMP_Text lockLabel,
                GameObject ownedPanel,
                TMP_Text ownedLabel,
                GameObject currentHighlight,
                Image candyIcon,
                Button candyButton,
                TMP_Text candyLabel,
                Image premiumIcon,
                Button premiumButton,
                TMP_Text premiumLabel)
            {
                Reward = reward;
                RewardIndex = rewardIndex;
                Root = root;
                CanvasGroup = canvasGroup;
                Background = background;
                Icon = icon;
                Level = level;
                Title = title;
                Status = status;
                LockOverlay = lockOverlay;
                LockLabel = lockLabel;
                OwnedPanel = ownedPanel;
                OwnedLabel = ownedLabel;
                CurrentHighlight = currentHighlight;
                CandyIcon = candyIcon;
                CandyButton = candyButton;
                CandyLabel = candyLabel;
                PremiumIcon = premiumIcon;
                PremiumButton = premiumButton;
                PremiumLabel = premiumLabel;
            }

            public LiveEventRewardDefinition Reward { get; }
            public int RewardIndex { get; }
            public GameObject Root { get; }
            public CanvasGroup CanvasGroup { get; }
            public Image Background { get; }
            public Image Icon { get; }
            public TMP_Text Level { get; }
            public TMP_Text Title { get; }
            public TMP_Text Status { get; }
            public GameObject LockOverlay { get; }
            public TMP_Text LockLabel { get; }
            public GameObject OwnedPanel { get; }
            public TMP_Text OwnedLabel { get; }
            public GameObject CurrentHighlight { get; }
            public Image CandyIcon { get; }
            public Button CandyButton { get; }
            public TMP_Text CandyLabel { get; }
            public Image PremiumIcon { get; }
            public Button PremiumButton { get; }
            public TMP_Text PremiumLabel { get; }
        }

        private sealed class RewardWeekBinding
        {
            public RewardWeekBinding(
                LiveEventRewardWeekDefinition week,
                CanvasGroup canvasGroup,
                TMP_Text header,
                TMP_Text count,
                TMP_Text countdown,
                IReadOnlyList<string> rewardIds)
            {
                Week = week;
                CanvasGroup = canvasGroup;
                Header = header;
                Count = count;
                Countdown = countdown;
                RewardIds = rewardIds;
            }

            public LiveEventRewardWeekDefinition Week { get; }
            public CanvasGroup CanvasGroup { get; }
            public TMP_Text Header { get; }
            public TMP_Text Count { get; }
            public TMP_Text Countdown { get; }
            public IReadOnlyList<string> RewardIds { get; }
        }

        private sealed class ShopBinding : IRuntimeBinding
        {
            public ShopBinding(
                LiveEventShopOfferDefinition offer,
                GameObject root,
                Image previewBackground,
                Image icon,
                TMP_Text title,
                TMP_Text rarity,
                Image currencyIcon,
                Button button,
                TMP_Text buttonLabel)
            {
                Offer = offer;
                RuntimeRoot = root;
                PreviewBackground = previewBackground;
                Icon = icon;
                Title = title;
                Rarity = rarity;
                CurrencyIcon = currencyIcon;
                Button = button;
                ButtonLabel = buttonLabel;
            }

            public LiveEventShopOfferDefinition Offer { get; }
            public GameObject RuntimeRoot { get; }
            public Image PreviewBackground { get; }
            public Image Icon { get; }
            public TMP_Text Title { get; }
            public TMP_Text Rarity { get; }
            public Image CurrencyIcon { get; }
            public Button Button { get; }
            public TMP_Text ButtonLabel { get; }
        }

        private sealed class BundleBinding : IRuntimeBinding
        {
            public BundleBinding(
                LiveEventGrantDefinition grant,
                GameObject root,
                Image icon,
                TMP_Text title,
                TMP_Text type,
                TMP_Text value)
            {
                Grant = grant;
                RuntimeRoot = root;
                Icon = icon;
                Title = title;
                Type = type;
                Value = value;
            }

            public LiveEventGrantDefinition Grant { get; }
            public GameObject RuntimeRoot { get; }
            public Image Icon { get; }
            public TMP_Text Title { get; }
            public TMP_Text Type { get; }
            public TMP_Text Value { get; }
        }

        private readonly struct BundleGrantPresentation
        {
            public BundleGrantPresentation(Sprite icon, string title, string type, string value)
            {
                Icon = icon;
                Title = title;
                Type = type;
                Value = value;
            }

            public Sprite Icon { get; }
            public string Title { get; }
            public string Type { get; }
            public string Value { get; }
        }
    }
}
