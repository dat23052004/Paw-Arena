using System;
using System.Collections.Generic;
using PawArena.Core.Economy;
using PawArena.Core.Inventory;
using PawArena.UI.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PawArena.UI.Inventory
{
    /// <summary>Renders the inventory session and raises item-selection intents from uGUI.</summary>
    [DisallowMultipleComponent]
    public sealed class InventoryView : MonoBehaviour
    {
        private const int CategoryCount = 5;

        private static readonly ItemCategory[] Categories =
        {
            ItemCategory.Fur,
            ItemCategory.Ability,
            ItemCategory.Gear,
            ItemCategory.Effect,
            ItemCategory.Emote
        };

        [Header("Screen")]
        [SerializeField] private UIScreen _screen = null;
        [SerializeField] private Button[] _categoryButtons = new Button[CategoryCount];
        [SerializeField] private Image[] _categoryBackgrounds = new Image[CategoryCount];
        [SerializeField] private RectTransform[] _categoryPages = new RectTransform[CategoryCount];
        [SerializeField] private GameObject[] _itemTemplates = new GameObject[CategoryCount];
        [SerializeField] private TMP_InputField _searchInput = null;

        [Header("Details")]
        [SerializeField] private Image _detailIcon = null;
        [SerializeField] private TMP_Text _detailName = null;
        [SerializeField] private TMP_Text _detailRarity = null;
        [SerializeField] private TMP_Text _detailDescription = null;
        [SerializeField] private TMP_Text _coinText = null;
        [SerializeField] private Button _actionButton = null;
        [SerializeField] private TMP_Text _actionLabel = null;

        [Header("States")]
        [SerializeField] private Sprite _activeTabSprite = null;
        [SerializeField] private Sprite _inactiveTabSprite = null;
        [SerializeField] private Sprite _ownedCardSprite = null;
        [SerializeField] private Sprite _lockedCardSprite = null;
        [SerializeField] private Sprite _selectedCardSprite = null;
        [SerializeField] private Sprite _equippedCardSprite = null;

        private readonly Dictionary<string, CardBinding> _cards = new(StringComparer.Ordinal);
        private readonly List<ButtonBinding> _buttons = new();
        private InventorySession _session;
        private Func<string, Sprite> _iconResolver;
        private Func<string, string> _descriptionResolver;
        private string _searchQuery = string.Empty;
        private int _activeCategory;
        private bool _listening;
        private bool _built;

        /// <summary>Binds live inventory state and presentation resolvers to this view.</summary>
        public void Bind(
            InventorySession session,
            Func<string, Sprite> iconResolver,
            Func<string, string> descriptionResolver)
        {
            Unbind();
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _iconResolver = iconResolver;
            _descriptionResolver = descriptionResolver;
            Build();
            Listen();
            SwitchCategory(_activeCategory);
        }

        /// <summary>Disconnects UI callbacks without disposing the shared domain session.</summary>
        public void Unbind()
        {
            StopListening();
            for (var i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Button.onClick.RemoveListener(_buttons[i].Action);
            }

            if (_searchInput != null)
            {
                _searchInput.onValueChanged.RemoveListener(HandleSearchChanged);
            }

            _buttons.Clear();
            _cards.Clear();
            DestroyRuntimeLists();
            _session = null;
            _iconResolver = null;
            _descriptionResolver = null;
            _searchQuery = string.Empty;
            _built = false;
        }

        private void OnEnable()
        {
            if (_session == null)
            {
                return;
            }

            Listen();
            Refresh();
        }

        private void OnDisable() => StopListening();

        private void OnDestroy() => Unbind();

        private void Build()
        {
            if (_built)
            {
                return;
            }

            ResolveFallbackReferences();
            _cards.Clear();
            if (_actionButton != null)
            {
                BindButton(_actionButton, HandleActionRequested);
            }

            for (var i = 0; i < Categories.Length; i++)
            {
                var index = i;
                if (i < _categoryButtons.Length)
                {
                    BindButton(_categoryButtons[i], () => SwitchCategory(index));
                }

                BuildCategory(index);
            }

            if (_searchInput != null)
            {
                _searchInput.onValueChanged.AddListener(HandleSearchChanged);
            }

            _built = true;
        }

        private void BuildCategory(int categoryIndex)
        {
            if (categoryIndex >= _categoryPages.Length || categoryIndex >= _itemTemplates.Length)
            {
                return;
            }

            var page = _categoryPages[categoryIndex];
            var template = _itemTemplates[categoryIndex];
            if (page == null || template == null)
            {
                return;
            }

            HideAuthoredCards(page, categoryIndex);
            CreateRuntimeList(page, categoryIndex, out var ownedGrid, out var unownedGrid);
            for (var i = 0; i < _session.Items.Count; i++)
            {
                var item = _session.Items[i];
                if (item.Category != Categories[categoryIndex])
                {
                    continue;
                }

                var owned = _session.Inventory.IsOwned(item.ItemId);
                var cardObject = Instantiate(template, owned ? ownedGrid : unownedGrid, false);
                cardObject.name = "RuntimeItem_" + item.ItemId;
                cardObject.SetActive(true);
                var card = new CardBinding(
                    item,
                    cardObject,
                    cardObject.GetComponent<Button>(),
                    cardObject.GetComponent<Image>(),
                    FindChildComponent<Image>(cardObject.transform, "ItemIcon_"),
                    FindChildComponent<TMP_Text>(cardObject.transform, "ItemName_"),
                    FindChildComponent<Transform>(cardObject.transform, "ItemLock_"),
                    FindChildComponent<Transform>(cardObject.transform, "ItemCheck_"),
                    FindChildComponent<TMP_Text>(cardObject.transform, "ItemPrice_"),
                    ownedGrid,
                    unownedGrid);
                _cards.Add(item.ItemId, card);

                var itemId = item.ItemId;
                BindButton(card.Button, () => _session?.Select(itemId));
            }
        }

        private static void HideAuthoredCards(RectTransform page, int categoryIndex)
        {
            var prefix = "Item_" + categoryIndex + "_";
            var authoredCards = page.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < authoredCards.Length; i++)
            {
                if (authoredCards[i].name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    authoredCards[i].gameObject.SetActive(false);
                }
            }
        }

        private void CreateRuntimeList(
            RectTransform page,
            int categoryIndex,
            out RectTransform ownedGrid,
            out RectTransform unownedGrid)
        {
            var viewportObject = new GameObject(
                "RuntimeInventoryViewport_" + categoryIndex,
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect));
            var viewport = (RectTransform)viewportObject.transform;
            viewport.SetParent(page, false);
            Stretch(viewport, new Vector2(18f, 14f), new Vector2(-18f, -14f));
            var viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);

            var contentObject = new GameObject(
                "RuntimeInventoryContent_" + categoryIndex,
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            var content = (RectTransform)contentObject.transform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var vertical = contentObject.GetComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(6, 6, 0, 18);
            vertical.spacing = 10f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateSectionHeader(content, "OwnedHeader_" + categoryIndex, "SỞ HỮU");
            ownedGrid = CreateGrid(content, "OwnedGrid_" + categoryIndex);
            CreateSectionHeader(content, "UnownedHeader_" + categoryIndex, "CHƯA SỞ HỮU");
            unownedGrid = CreateGrid(content, "UnownedGrid_" + categoryIndex);

            var scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
        }

        private void CreateSectionHeader(Transform parent, string objectName, string label)
        {
            var headerObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            var header = (RectTransform)headerObject.transform;
            header.SetParent(parent, false);
            var text = headerObject.GetComponent<TextMeshProUGUI>();
            text.font = _detailName != null ? _detailName.font : TMP_Settings.defaultFontAsset;
            text.text = "────────  " + label + "  ────────";
            text.fontSize = 30;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.outlineColor = new Color32(5, 20, 51, 242);
            text.outlineWidth = 0.18f;
            headerObject.GetComponent<LayoutElement>().preferredHeight = 54f;
        }

        private static RectTransform CreateGrid(Transform parent, string objectName)
        {
            var gridObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(GridLayoutGroup),
                typeof(ContentSizeFitter),
                typeof(LayoutElement));
            var gridRect = (RectTransform)gridObject.transform;
            gridRect.SetParent(parent, false);

            var grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(10, 10, 4, 8);
            grid.cellSize = new Vector2(159f, 201f);
            grid.spacing = new Vector2(18f, 16f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            var fitter = gridObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            gridObject.GetComponent<LayoutElement>().minHeight = 12f;
            return gridRect;
        }

        private void SwitchCategory(int categoryIndex)
        {
            if (_session == null || categoryIndex < 0 || categoryIndex >= Categories.Length)
            {
                return;
            }

            _activeCategory = categoryIndex;
            for (var i = 0; i < Categories.Length; i++)
            {
                if (i < _categoryPages.Length && _categoryPages[i] != null)
                {
                    _categoryPages[i].gameObject.SetActive(i == categoryIndex);
                }

                if (i < _categoryBackgrounds.Length && _categoryBackgrounds[i] != null)
                {
                    _categoryBackgrounds[i].sprite = i == categoryIndex
                        ? _activeTabSprite ?? _categoryBackgrounds[i].sprite
                        : _inactiveTabSprite ?? _categoryBackgrounds[i].sprite;
                    _categoryBackgrounds[i].color = Color.white;
                }
            }

            var selected = _session.Find(_session.SelectedItemId);
            if (selected == null || selected.Category != Categories[categoryIndex])
            {
                SelectFirstItemInCategory(categoryIndex);
            }

            Refresh();
        }

        private void SelectFirstItemInCategory(int categoryIndex)
        {
            for (var i = 0; i < _session.Items.Count; i++)
            {
                if (_session.Items[i].Category != Categories[categoryIndex])
                {
                    continue;
                }

                _session.Select(_session.Items[i].ItemId);
                return;
            }
        }

        private void HandleActionRequested()
        {
            if (_session == null)
            {
                return;
            }

            if (_session.HasSaveError)
            {
                _session.Save();
            }
            else
            {
                _session.ActivateSelected();
            }
        }

        private void HandleScreenShown() => SwitchCategory(_activeCategory);

        private void HandleSearchChanged(string value)
        {
            _searchQuery = value?.Trim() ?? string.Empty;
            RefreshCards();
        }

        private void Refresh()
        {
            if (_session == null)
            {
                return;
            }

            if (_coinText != null)
            {
                _coinText.text = _session.Wallet.GetBalance(CurrencyType.Coins).ToString("N0");
            }

            RefreshCards();
            var selected = _session.Find(_session.SelectedItemId);
            if (selected == null || selected.Category != Categories[_activeCategory])
            {
                RefreshEmptyDetails();
                return;
            }

            var owned = _session.Inventory.IsOwned(selected.ItemId);
            var equipped = _session.Inventory.IsEquipped(selected.ItemId);
            if (_detailIcon != null)
            {
                _detailIcon.sprite = _iconResolver?.Invoke(selected.ItemId);
                _detailIcon.preserveAspect = true;
            }

            if (_detailName != null)
            {
                _detailName.text = selected.DisplayName;
            }

            if (_detailRarity != null)
            {
                _detailRarity.text = RarityLabel(selected.Rarity);
                _detailRarity.color = RarityColor(selected.Rarity);
            }

            if (_detailDescription != null)
            {
                var description = _descriptionResolver?.Invoke(selected.ItemId);
                if (string.IsNullOrWhiteSpace(description))
                {
                    description = CategoryDescription(selected.Category);
                }

                _detailDescription.text = string.IsNullOrWhiteSpace(_session.Status)
                    ? description
                    : description + "\n\n" + _session.Status;
                _detailDescription.color = _session.HasSaveError
                    ? new Color(1f, 0.55f, 0.55f)
                    : new Color(0.84f, 0.91f, 1f);
            }

            RefreshAction(selected, owned, equipped);
        }

        private void RefreshCards()
        {
            foreach (var pair in _cards)
            {
                RefreshCard(pair.Value);
            }
        }

        private void RefreshCard(CardBinding card)
        {
            var owned = _session.Inventory.IsOwned(card.Item.ItemId);
            var equipped = _session.Inventory.IsEquipped(card.Item.ItemId);
            var expectedParent = owned ? card.OwnedParent : card.UnownedParent;
            if (card.Root.transform.parent != expectedParent)
            {
                card.Root.transform.SetParent(expectedParent, false);
            }

            var matchesSearch = string.IsNullOrWhiteSpace(_searchQuery) ||
                card.Item.DisplayName.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                card.Item.ItemId.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
            card.Root.SetActive(matchesSearch);

            if (card.Icon != null)
            {
                card.Icon.sprite = _iconResolver?.Invoke(card.Item.ItemId);
                card.Icon.preserveAspect = true;
            }

            if (card.Name != null)
            {
                card.Name.text = card.Item.DisplayName;
                card.Name.color = RarityColor(card.Item.Rarity);
            }

            if (card.Lock != null)
            {
                card.Lock.gameObject.SetActive(!owned);
            }

            if (card.Check != null)
            {
                card.Check.gameObject.SetActive(equipped);
            }

            if (card.Price != null)
            {
                card.Price.text = !owned && card.Item.UnlockType == UnlockType.Coins && card.Item.Price > 0L
                    ? card.Item.Price.ToString("N0")
                    : string.Empty;
            }

            if (card.Background != null)
            {
                card.Background.sprite = ResolveCardSprite(card.Item.ItemId, owned, equipped) ?? card.Background.sprite;
                card.Background.color = Color.white;
            }
        }

        private Sprite ResolveCardSprite(string itemId, bool owned, bool equipped)
        {
            if (itemId == _session.SelectedItemId && _selectedCardSprite != null)
            {
                return _selectedCardSprite;
            }

            if (equipped && _equippedCardSprite != null)
            {
                return _equippedCardSprite;
            }

            return owned ? _ownedCardSprite : _lockedCardSprite;
        }

        private void RefreshAction(ItemDescriptor item, bool owned, bool equipped)
        {
            if (_actionButton == null || _actionLabel == null)
            {
                return;
            }

            if (_session.HasSaveError)
            {
                _actionButton.interactable = true;
                _actionLabel.text = "Thử lưu lại";
                return;
            }

            if (!owned)
            {
                var purchasable = item.UnlockType == UnlockType.Coins && item.Price > 0L;
                _actionButton.interactable = purchasable;
                _actionLabel.text = purchasable
                    ? item.Price.ToString("N0") + " xu • Mở khóa"
                    : UnlockLabel(item.UnlockType);
                return;
            }

            _actionButton.interactable = item.AllowedSlots.Count > 0;
            _actionLabel.text = item.AllowedSlots.Count == 0
                ? "Đã sở hữu"
                : equipped ? "Tháo" : "Trang bị";
        }

        private void RefreshEmptyDetails()
        {
            if (_detailIcon != null)
            {
                _detailIcon.sprite = null;
            }

            if (_detailName != null)
            {
                _detailName.text = "Chưa có vật phẩm";
            }

            if (_detailRarity != null)
            {
                _detailRarity.text = string.Empty;
            }

            if (_detailDescription != null)
            {
                _detailDescription.text = "Danh mục này chưa có dữ liệu được thiết kế.";
                _detailDescription.color = new Color(0.84f, 0.91f, 1f);
            }

            if (_actionButton != null)
            {
                _actionButton.interactable = false;
            }

            if (_actionLabel != null)
            {
                _actionLabel.text = "Không khả dụng";
            }
        }

        private void Listen()
        {
            if (_listening || _session == null)
            {
                return;
            }

            _session.Changed += Refresh;
            if (_screen != null)
            {
                _screen.Shown += HandleScreenShown;
            }

            _listening = true;
        }

        private void StopListening()
        {
            if (!_listening)
            {
                return;
            }

            if (_session != null)
            {
                _session.Changed -= Refresh;
            }

            if (_screen != null)
            {
                _screen.Shown -= HandleScreenShown;
            }

            _listening = false;
        }

        private void BindButton(Button button, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(action);
            _buttons.Add(new ButtonBinding(button, action));
        }

        private void ResolveFallbackReferences()
        {
            _screen ??= FindComponent<UIScreen>("Window_Inventory");
            _detailIcon ??= FindComponent<Image>("Inventory_Icon");
            _detailName ??= FindComponent<TMP_Text>("Inventory_Name");
            _detailRarity ??= FindComponent<TMP_Text>("Inventory_Rarity");
            _detailDescription ??= FindComponent<TMP_Text>("Inventory_Description");
            _coinText ??= FindComponent<TMP_Text>("HUD_CoinText");
            _actionButton ??= FindComponent<Button>("Btn_Equip");
            _actionLabel ??= FindComponent<TMP_Text>("EquipButtonLabel");
            _searchInput ??= FindComponent<TMP_InputField>("InventorySearchPanel");

            EnsureArraySize(ref _categoryButtons);
            EnsureArraySize(ref _categoryBackgrounds);
            EnsureArraySize(ref _categoryPages);
            EnsureArraySize(ref _itemTemplates);
            for (var i = 0; i < CategoryCount; i++)
            {
                _categoryButtons[i] ??= FindComponent<Button>("InvTab_" + i);
                _categoryBackgrounds[i] ??= FindComponent<Image>("TabBackground_" + i);
                _categoryPages[i] ??= FindTransform("InvPage_" + i) as RectTransform;
                _itemTemplates[i] ??= FindTemplate(i);
            }
        }

        private GameObject FindTemplate(int categoryIndex)
        {
            var page = categoryIndex < _categoryPages.Length ? _categoryPages[categoryIndex] : null;
            if (page == null)
            {
                return null;
            }

            var descendants = page.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < descendants.Length; i++)
            {
                if (descendants[i].name.StartsWith("Item_" + categoryIndex + "_", StringComparison.Ordinal))
                {
                    return descendants[i].gameObject;
                }
            }

            return null;
        }

        private Transform FindTransform(string objectName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, objectName, StringComparison.Ordinal))
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private T FindComponent<T>(string objectName) where T : Component =>
            FindTransform(objectName)?.GetComponent<T>();

        private static T FindChildComponent<T>(Transform parent, string namePrefix) where T : Component
        {
            var descendants = parent.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < descendants.Length; i++)
            {
                if (descendants[i].name.StartsWith(namePrefix, StringComparison.Ordinal))
                {
                    return descendants[i].GetComponent<T>();
                }
            }

            return null;
        }

        private void DestroyRuntimeLists()
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = transforms.Length - 1; i >= 0; i--)
            {
                if (!transforms[i].name.StartsWith("RuntimeInventoryViewport_", StringComparison.Ordinal))
                {
                    continue;
                }

                var runtimeObject = transforms[i].gameObject;
                runtimeObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(runtimeObject);
                }
                else
                {
                    DestroyImmediate(runtimeObject);
                }
            }
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void EnsureArraySize<T>(ref T[] array) where T : UnityEngine.Object
        {
            if (array != null && array.Length == CategoryCount)
            {
                return;
            }

            var resized = new T[CategoryCount];
            if (array != null)
            {
                Array.Copy(array, resized, Mathf.Min(array.Length, resized.Length));
            }

            array = resized;
        }

        private static string RarityLabel(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => "Thường",
            ItemRarity.Rare => "Hiếm",
            ItemRarity.Epic => "Sử thi",
            ItemRarity.Legendary => "Huyền thoại",
            ItemRarity.Mythic => "Thần thoại",
            _ => rarity.ToString()
        };

        private static Color RarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Rare => new Color(0.28f, 0.83f, 1f),
            ItemRarity.Epic => new Color(0.79f, 0.39f, 1f),
            ItemRarity.Legendary => new Color(1f, 0.79f, 0.25f),
            ItemRarity.Mythic => new Color(1f, 0.38f, 0.55f),
            _ => Color.white
        };

        private static string UnlockLabel(UnlockType unlockType) => unlockType switch
        {
            UnlockType.Event => "Mở qua sự kiện",
            UnlockType.Egg => "Mở từ trứng",
            UnlockType.Pack => "Mở từ gói",
            UnlockType.Grant => "Mở từ phần thưởng",
            _ => "Chưa mở khóa"
        };

        private static string CategoryDescription(ItemCategory category) => category switch
        {
            ItemCategory.Fur => "Thay đổi ngoại hình nhân vật.",
            ItemCategory.Ability => "Kỹ năng có thể trang bị.",
            ItemCategory.Gear => "Phụ kiện trang trí, không tăng chỉ số.",
            ItemCategory.Effect => "Hiệu ứng được phát theo sự kiện tương ứng.",
            ItemCategory.Emote => "Biểu cảm có thể dùng trong trận đấu.",
            _ => string.Empty
        };

        private sealed class CardBinding
        {
            public CardBinding(
                ItemDescriptor item,
                GameObject root,
                Button button,
                Image background,
                Image icon,
                TMP_Text name,
                Transform itemLock,
                Transform check,
                TMP_Text price,
                RectTransform ownedParent,
                RectTransform unownedParent)
            {
                Item = item;
                Root = root;
                Button = button;
                Background = background;
                Icon = icon;
                Name = name;
                Lock = itemLock;
                Check = check;
                Price = price;
                OwnedParent = ownedParent;
                UnownedParent = unownedParent;
            }

            public ItemDescriptor Item { get; }
            public GameObject Root { get; }
            public Button Button { get; }
            public Image Background { get; }
            public Image Icon { get; }
            public TMP_Text Name { get; }
            public Transform Lock { get; }
            public Transform Check { get; }
            public TMP_Text Price { get; }
            public RectTransform OwnedParent { get; }
            public RectTransform UnownedParent { get; }
        }

        private readonly struct ButtonBinding
        {
            public ButtonBinding(Button button, UnityAction action)
            {
                Button = button;
                Action = action;
            }

            public Button Button { get; }
            public UnityAction Action { get; }
        }
    }
}
