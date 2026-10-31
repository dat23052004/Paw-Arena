using System;
using System.Collections.Generic;
using PawArena.Core.Eggs;
using UnityEngine;
using UnityEngine.UI;

namespace PawArena.UI.Shop
{
    /// <summary>Owns Shop presentation, section navigation and purchase-intent forwarding.</summary>
    [DisallowMultipleComponent]
    public sealed class ShopScreenView : MonoBehaviour
    {
        private const float CountdownRefreshInterval = 1f;

        [Header("Data")]
        [SerializeField] private ShopCatalog _catalog = null;

        [Header("Scrolling")]
        [SerializeField] private ScrollRect _scrollRect = null;
        [SerializeField] private RectTransform _viewport = null;
        [SerializeField] private RectTransform _content = null;
        [SerializeField] private RectTransform _eggsSection = null;
        [SerializeField] private RectTransform _bundlesSection = null;
        [SerializeField] private RectTransform _coinsSection = null;

        [Header("Tabs")]
        [SerializeField] private Button _eggsTab = null;
        [SerializeField] private Button _bundlesTab = null;
        [SerializeField] private Button _coinsTab = null;
        [SerializeField] private Image _eggsTabBackground = null;
        [SerializeField] private Image _bundlesTabBackground = null;
        [SerializeField] private Image _coinsTabBackground = null;
        [SerializeField] private Color _selectedTabColor = new(0.34f, 0.82f, 0.22f, 1f);
        [SerializeField] private Color _normalTabColor = new(0.08f, 0.14f, 0.31f, 1f);

        [Header("Dynamic content")]
        [SerializeField] private RectTransform _eggContainer = null;
        [SerializeField] private ShopOfferCardView _eggTemplate = null;
        [SerializeField] private RectTransform _bundleContainer = null;
        [SerializeField] private ShopOfferCardView _bundleTemplate = null;
        [SerializeField] private RectTransform _specialPackContainer = null;
        [SerializeField] private ShopOfferCardView _specialPackTemplate = null;
        [SerializeField] private RectTransform _coinPackContainer = null;
        [SerializeField] private ShopOfferCardView _coinPackTemplate = null;

        private readonly List<ShopOfferCardView> _cards = new();
        private readonly Vector3[] _viewportCorners = new Vector3[4];
        private readonly Vector3[] _sectionCorners = new Vector3[4];
        private bool _initialized;
        private bool _isProgrammaticScroll;
        private float _nextCountdownRefresh;
        private ShopSection _selectedSection = ShopSection.Eggs;
        private EggShopRuntimeController _eggRuntimeController;

        /// <summary>Raised when a purchase button is pressed; no transaction is executed by the view.</summary>
        public event Action<ShopPurchaseRequest> PurchaseRequested;

        /// <summary>Raised when the player requests the odds for an egg.</summary>
        public event Action<string> EggOddsRequested;

        /// <summary>Raised when the player requests the tier selector for an egg.</summary>
        public event Action<string> EggTiersRequested;

        /// <summary>Raised by the gift placeholder button.</summary>
        public event Action<string> EggGiftRequested;

        private void Awake() => Initialize();

        private void Start()
        {
            if (!_initialized)
            {
                return;
            }

            _eggRuntimeController = GetComponent<EggShopRuntimeController>();
            if (_eggRuntimeController == null)
            {
                _eggRuntimeController = gameObject.AddComponent<EggShopRuntimeController>();
            }

            if (_eggRuntimeController.Initialize(this, _catalog))
            {
                Refresh();
            }
        }

        private void OnEnable()
        {
            Initialize();
            if (!_initialized)
            {
                return;
            }

            RebuildLayoutHierarchy();
            SelectSection(ShopSection.Eggs, true);
            RefreshCountdowns();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCountdownRefresh)
            {
                return;
            }

            RefreshCountdowns();
        }

        private void OnDestroy()
        {
            if (!_initialized)
            {
                return;
            }

            _eggsTab?.onClick.RemoveListener(HandleEggsRequested);
            _bundlesTab?.onClick.RemoveListener(HandleBundlesRequested);
            _coinsTab?.onClick.RemoveListener(HandleCoinsRequested);
            _scrollRect?.onValueChanged.RemoveListener(HandleScrollChanged);
        }

        /// <summary>Rebuilds visible cards from the currently assigned catalog.</summary>
        public void Refresh()
        {
            ClearCards();
            if (_catalog == null)
            {
                Debug.LogError($"{nameof(_catalog)} is not assigned.", this);
                return;
            }

            BuildEggs();
            BuildBundles();
            BuildCoinPacks();
            RebuildLayoutHierarchy();
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            if (_scrollRect == null || _viewport == null || _content == null)
            {
                Debug.LogError("Shop ScrollRect, Viewport and Content references are required.", this);
                enabled = false;
                return;
            }

            _eggsTab?.onClick.AddListener(HandleEggsRequested);
            _bundlesTab?.onClick.AddListener(HandleBundlesRequested);
            _coinsTab?.onClick.AddListener(HandleCoinsRequested);
            _scrollRect.onValueChanged.AddListener(HandleScrollChanged);
            ConfigureLayoutHierarchy();
            _initialized = true;
            Refresh();
        }

        private void ConfigureLayoutHierarchy()
        {
            ConfigureVerticalLayout(_eggContainer);
            ConfigureVerticalLayout(_bundleContainer);
            ConfigureVerticalLayout(_specialPackContainer);
            ConfigureVerticalLayout(_coinsSection);
        }

        private static void ConfigureVerticalLayout(RectTransform target)
        {
            if (target == null || !target.TryGetComponent(out VerticalLayoutGroup layout))
            {
                return;
            }

            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
        }

        private void BuildEggs()
        {
            if (_eggTemplate == null || _eggContainer == null)
            {
                return;
            }

            _eggTemplate.gameObject.SetActive(false);
            for (var i = 0; i < _catalog.Eggs.Count; i++)
            {
                var card = Instantiate(_eggTemplate, _eggContainer);
                card.name = $"Egg_{_catalog.Eggs[i].Id}";
                if (_eggRuntimeController != null
                    && _eggRuntimeController.TryGetBinding(
                        _catalog.Eggs[i].Id,
                        out EggDescriptor egg,
                        out EggTierDescriptor tier))
                {
                    card.BindEgg(
                        _catalog.Eggs[i],
                        egg,
                        tier,
                        HandlePurchaseRequested,
                        HandleEggOddsRequested,
                        HandleEggTiersRequested,
                        HandleEggGiftRequested);
                }
                else
                {
                    card.BindEgg(_catalog.Eggs[i], HandlePurchaseRequested);
                }
                _cards.Add(card);
            }
        }

        private void BuildBundles()
        {
            if (_bundleTemplate != null)
            {
                _bundleTemplate.gameObject.SetActive(false);
            }

            if (_specialPackTemplate != null)
            {
                _specialPackTemplate.gameObject.SetActive(false);
            }

            for (var i = 0; i < _catalog.Bundles.Count; i++)
            {
                var offer = _catalog.Bundles[i];
                var template = offer.SpecialPack ? _specialPackTemplate : _bundleTemplate;
                var container = offer.SpecialPack ? _specialPackContainer : _bundleContainer;
                if (offer.SpecialPack && (template == null || container == null))
                {
                    template = _bundleTemplate;
                    container = _bundleContainer;
                }

                if (template == null || container == null)
                {
                    continue;
                }

                var card = Instantiate(template, container);
                card.name = offer.SpecialPack
                    ? $"SpecialPack_{offer.Id}"
                    : $"Bundle_{offer.Id}";
                card.BindBundle(offer, HandlePurchaseRequested);
                _cards.Add(card);
            }
        }

        private void BuildCoinPacks()
        {
            if (_coinPackTemplate == null || _coinPackContainer == null)
            {
                return;
            }

            _coinPackTemplate.gameObject.SetActive(false);
            for (var i = 0; i < _catalog.CoinPacks.Count; i++)
            {
                var card = Instantiate(_coinPackTemplate, _coinPackContainer);
                card.name = $"CoinPack_{_catalog.CoinPacks[i].Id}";
                card.BindCoinPack(_catalog.CoinPacks[i], HandlePurchaseRequested);
                _cards.Add(card);
            }
        }

        private void ClearCards()
        {
            for (var i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] != null)
                {
                    _cards[i].gameObject.SetActive(false);
                    Destroy(_cards[i].gameObject);
                }
            }

            _cards.Clear();
        }

        private void RebuildLayoutHierarchy()
        {
            Canvas.ForceUpdateCanvases();
            RebuildLayout(_eggContainer);
            RebuildLayout(_bundleContainer);
            RebuildLayout(_specialPackContainer);
            RebuildLayout(_coinPackContainer);
            RebuildLayout(_eggsSection);
            RebuildLayout(_bundlesSection);
            RebuildLayout(_coinsSection);
            RebuildLayout(_content);
            Canvas.ForceUpdateCanvases();
        }

        private static void RebuildLayout(RectTransform target)
        {
            if (target != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(target);
            }
        }

        private void HandleEggsRequested() => SelectSection(ShopSection.Eggs, true);
        private void HandleBundlesRequested() => SelectSection(ShopSection.Bundles, true);
        private void HandleCoinsRequested() => SelectSection(ShopSection.Coins, true);

        private void HandlePurchaseRequested(ShopPurchaseRequest request) => PurchaseRequested?.Invoke(request);

        private void HandleEggOddsRequested(string eggId) => EggOddsRequested?.Invoke(eggId);

        private void HandleEggTiersRequested(string eggId) => EggTiersRequested?.Invoke(eggId);

        private void HandleEggGiftRequested(string eggId) => EggGiftRequested?.Invoke(eggId);

        private void HandleScrollChanged(Vector2 _)
        {
            if (_isProgrammaticScroll)
            {
                return;
            }

            SelectSection(FindVisibleSection(), false);
        }

        private void SelectSection(ShopSection section, bool scroll)
        {
            _selectedSection = section;
            ApplyTabVisuals();
            if (!scroll)
            {
                return;
            }

            var target = section switch
            {
                ShopSection.Bundles => _bundlesSection,
                ShopSection.Coins => _coinsSection,
                _ => _eggsSection
            };
            ScrollTo(target);
        }

        private void ScrollTo(RectTransform section)
        {
            if (section == null)
            {
                return;
            }

            RebuildLayoutHierarchy();
            var scrollableHeight = Mathf.Max(0f, _content.rect.height - _viewport.rect.height);
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_content, section);
            var distanceFromTop = Mathf.Max(0f, _content.rect.yMax - bounds.max.y);
            var normalized = scrollableHeight <= 0.01f
                ? 1f
                : 1f - Mathf.Clamp01(distanceFromTop / scrollableHeight);

            _isProgrammaticScroll = true;
            _scrollRect.StopMovement();
            _scrollRect.verticalNormalizedPosition = normalized;
            _isProgrammaticScroll = false;
        }

        private ShopSection FindVisibleSection()
        {
            _viewport.GetWorldCorners(_viewportCorners);
            var viewportTop = _viewportCorners[1].y;
            var bestSection = ShopSection.Eggs;
            var bestDistance = float.MaxValue;
            EvaluateSection(_eggsSection, ShopSection.Eggs, viewportTop, ref bestSection, ref bestDistance);
            EvaluateSection(_bundlesSection, ShopSection.Bundles, viewportTop, ref bestSection, ref bestDistance);
            EvaluateSection(_coinsSection, ShopSection.Coins, viewportTop, ref bestSection, ref bestDistance);
            return bestSection;
        }

        private void EvaluateSection(
            RectTransform section,
            ShopSection candidate,
            float viewportTop,
            ref ShopSection bestSection,
            ref float bestDistance)
        {
            if (section == null)
            {
                return;
            }

            section.GetWorldCorners(_sectionCorners);
            var distance = Mathf.Abs(viewportTop - _sectionCorners[1].y);
            if (distance >= bestDistance)
            {
                return;
            }

            bestDistance = distance;
            bestSection = candidate;
        }

        private void ApplyTabVisuals()
        {
            SetTabColor(_eggsTabBackground, _selectedSection == ShopSection.Eggs);
            SetTabColor(_bundlesTabBackground, _selectedSection == ShopSection.Bundles);
            SetTabColor(_coinsTabBackground, _selectedSection == ShopSection.Coins);
        }

        private void SetTabColor(Image image, bool selected)
        {
            if (image != null)
            {
                image.color = selected ? _selectedTabColor : _normalTabColor;
            }
        }

        private void RefreshCountdowns()
        {
            _nextCountdownRefresh = Time.unscaledTime + CountdownRefreshInterval;
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < _cards.Count; i++)
            {
                _cards[i]?.RefreshCountdown(now);
            }
        }

        private enum ShopSection
        {
            Eggs,
            Bundles,
            Coins
        }
    }
}
