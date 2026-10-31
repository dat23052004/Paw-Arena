using System;
using System.Collections.Generic;
using System.Text;
using PawArena.Core.Economy;
using PawArena.Core.Eggs;
using PawArena.Core.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PawArena.UI.Shop
{
    /// <summary>Runtime-built modal UI shared by Odds, Tiers and Hatch Reveal.</summary>
    [DisallowMultipleComponent]
    public sealed class EggShopOverlayView : MonoBehaviour
    {
        private static readonly Color BackdropColor = new(0.01f, 0.03f, 0.09f, 0.82f);
        private static readonly Color PanelColor = new(0.025f, 0.07f, 0.17f, 0.99f);
        private static readonly Color RowColor = new(0.08f, 0.16f, 0.34f, 1f);
        private static readonly Color ButtonColor = new(0.22f, 0.72f, 0.22f, 1f);
        private static readonly Color DisabledColor = new(0.22f, 0.27f, 0.36f, 1f);

        private readonly List<GameObject> _dynamicObjects = new();

        private ShopCatalog _presentationCatalog;
        private RectTransform _content;
        private TMP_Text _title;
        private Button _footerButton;
        private TMP_Text _footerLabel;
        private EggHatchResult _revealResult;
        private IItemCatalog _revealItemCatalog;
        private int _revealIndex;
        private bool _initialized;

        public void Initialize(ShopCatalog presentationCatalog)
        {
            if (_initialized)
            {
                return;
            }

            _presentationCatalog = presentationCatalog;
            BuildView();
            gameObject.SetActive(false);
            _initialized = true;
        }

        public void ShowOdds(
            EggDescriptor egg,
            EggTierDescriptor tier,
            IItemCatalog itemCatalog,
            bool flattenItems)
        {
            if (egg == null || tier == null || itemCatalog == null)
            {
                return;
            }

            var title = egg.Tiers.Count > 1
                ? $"{egg.DisplayName} {tier.DisplayName} Odds"
                : $"{egg.DisplayName} Odds";
            Prepare(title);
            for (var poolIndex = 0; poolIndex < tier.RarityPools.Count; poolIndex++)
            {
                var pool = tier.RarityPools[poolIndex];
                if (flattenItems)
                {
                    var itemChance = pool.ChanceBasisPoints / 100f / pool.ItemIds.Count;
                    for (var itemIndex = 0; itemIndex < pool.ItemIds.Count; itemIndex++)
                    {
                        var itemName = GetItemName(itemCatalog, pool.ItemIds[itemIndex]);
                        AddTwoColumnRow(
                            itemName,
                            $"{itemChance:0.##}%",
                            GetRarityColor(pool.Rarity));
                    }
                }
                else
                {
                    AddSectionHeader(
                        $"{pool.ChanceBasisPoints / 100f:0.##}%  {pool.Rarity}",
                        GetRarityColor(pool.Rarity));
                    var names = new StringBuilder();
                    for (var itemIndex = 0; itemIndex < pool.ItemIds.Count; itemIndex++)
                    {
                        if (itemIndex > 0)
                        {
                            names.Append("   •   ");
                        }

                        names.Append(GetItemName(itemCatalog, pool.ItemIds[itemIndex]));
                    }

                    AddParagraph(names.ToString(), 31f, 82f);
                }
            }

            ConfigureFooter("Close", Hide);
            Show();
        }

        public void ShowTiers(
            EggDescriptor egg,
            string selectedTierId,
            IEggProgressService progress,
            Action<EggTierDescriptor> onSelect,
            Action<EggTierDescriptor> onOdds)
        {
            if (egg == null || progress == null)
            {
                return;
            }

            Prepare($"{egg.DisplayName} Tiers");
            for (var i = 0; i < egg.Tiers.Count; i++)
            {
                var tier = egg.Tiers[i];
                var isUnlocked = progress.IsTierUnlocked(egg, tier);
                var isSelected = string.Equals(selectedTierId, tier.TierId, StringComparison.Ordinal);
                var progressText = GetTierProgressText(egg, tier, progress, isUnlocked);
                var priceText = GetTierPriceText(tier);
                AddTierRow(
                    tier,
                    $"{tier.DisplayName}{(isSelected ? "  •  SELECTED" : string.Empty)}\n{priceText}\n{progressText}",
                    isUnlocked,
                    () => onOdds?.Invoke(tier),
                    () => onSelect?.Invoke(tier));
            }

            ConfigureFooter("Close", Hide);
            Show();
        }

        public void ShowReveal(EggHatchResult result, IItemCatalog itemCatalog)
        {
            if (result == null || !result.IsSuccess || result.Rewards.Count == 0)
            {
                return;
            }

            _revealResult = result;
            _revealItemCatalog = itemCatalog;
            _revealIndex = 0;
            RenderReveal();
            Show();
        }

        public void ShowMessage(string title, string message)
        {
            Prepare(title);
            AddParagraph(message, 38f, 220f, TextAlignmentOptions.Center);
            ConfigureFooter("Close", Hide);
            Show();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void BuildView()
        {
            var root = transform as RectTransform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var backdrop = gameObject.AddComponent<Image>();
            backdrop.color = BackdropColor;
            backdrop.raycastTarget = true;

            var panel = CreateRect("Panel", root);
            panel.anchorMin = new Vector2(0.08f, 0.08f);
            panel.anchorMax = new Vector2(0.92f, 0.92f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;

            _title = CreateText(panel, string.Empty, 52f, FontStyles.Bold, TextAlignmentOptions.Center);
            var titleRect = _title.rectTransform;
            titleRect.anchorMin = new Vector2(0.08f, 0.85f);
            titleRect.anchorMax = new Vector2(0.92f, 0.98f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var closeButton = CreateButton(panel, "×", new Color(0.84f, 0.16f, 0.18f, 1f), Hide);
            var closeRect = closeButton.transform as RectTransform;
            closeRect.anchorMin = new Vector2(0.91f, 0.87f);
            closeRect.anchorMax = new Vector2(0.98f, 0.97f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;

            var scrollRectTransform = CreateRect("Scroll", panel);
            scrollRectTransform.anchorMin = new Vector2(0.04f, 0.15f);
            scrollRectTransform.anchorMax = new Vector2(0.96f, 0.84f);
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;
            var scrollRect = scrollRectTransform.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateRect("Viewport", scrollRectTransform);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.08f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            _content = CreateRect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = new Vector2(10f, 0f);
            _content.offsetMax = new Vector2(-10f, 0f);
            var contentLayout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(10, 10, 10, 10);
            contentLayout.spacing = 10f;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;
            var contentFitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = _content;

            _footerButton = CreateButton(panel, "Close", ButtonColor, Hide);
            var footerRect = _footerButton.transform as RectTransform;
            footerRect.anchorMin = new Vector2(0.36f, 0.025f);
            footerRect.anchorMax = new Vector2(0.64f, 0.125f);
            footerRect.offsetMin = Vector2.zero;
            footerRect.offsetMax = Vector2.zero;
            _footerLabel = _footerButton.GetComponentInChildren<TMP_Text>();
        }

        private void Prepare(string title)
        {
            ClearDynamicContent();
            _title.text = title ?? string.Empty;
            _revealResult = null;
            _revealItemCatalog = null;
            _revealIndex = 0;
        }

        private void RenderReveal()
        {
            var result = _revealResult;
            var itemCatalog = _revealItemCatalog;
            var index = _revealIndex;
            ClearDynamicContent();

            var reward = result.Rewards[index];
            var itemName = GetItemName(itemCatalog, reward.ItemId);
            _title.text = reward.WasAlreadyOwned ? "Repeat Item" : "New Reward!";
            AddParagraph(itemName, 64f, 150f, TextAlignmentOptions.Center, FontStyles.Bold);
            AddParagraph(
                reward.Rarity.ToString(),
                44f,
                80f,
                TextAlignmentOptions.Center,
                FontStyles.Bold,
                GetRarityColor(reward.Rarity));

            if (reward.WasAlreadyOwned)
            {
                AddParagraph(
                    $"Already owned  •  +{reward.RefundCoins:N0} Coins",
                    38f,
                    100f,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold,
                    new Color(1f, 0.78f, 0.16f, 1f));
            }
            else
            {
                AddParagraph("Added to your collection", 34f, 100f, TextAlignmentOptions.Center);
            }

            var isLast = index >= result.Rewards.Count - 1;
            var footerText = isLast
                ? "Continue"
                : $"Next  ({index + 1}/{result.Rewards.Count})";
            ConfigureFooter(footerText, AdvanceReveal);
        }

        private void AdvanceReveal()
        {
            if (_revealResult == null || _revealIndex >= _revealResult.Rewards.Count - 1)
            {
                Hide();
                return;
            }

            _revealIndex++;
            RenderReveal();
        }

        private void AddTierRow(
            EggTierDescriptor tier,
            string description,
            bool isUnlocked,
            UnityAction onOdds,
            UnityAction onOpen)
        {
            var row = CreateLayoutRow($"Tier_{tier.Order}", 130f, RowColor);
            var label = CreateText(row.transform, description, 29f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            AddFlexibleLayout(label.gameObject, 1f, 0f);
            var odds = CreateButton(row.transform as RectTransform, "Odds", new Color(0.16f, 0.25f, 0.48f, 1f), onOdds);
            AddFixedLayout(odds.gameObject, 130f, 75f);
            var open = CreateButton(
                row.transform as RectTransform,
                isUnlocked ? "Open" : "Locked",
                isUnlocked ? ButtonColor : DisabledColor,
                isUnlocked ? onOpen : null);
            open.interactable = isUnlocked;
            AddFixedLayout(open.gameObject, 145f, 75f);
        }

        private void AddSectionHeader(string text, Color color)
        {
            var label = CreateText(_content, text, 40f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            label.color = color;
            AddFixedLayout(label.gameObject, -1f, 62f);
            _dynamicObjects.Add(label.gameObject);
        }

        private void AddTwoColumnRow(string left, string right, Color leftColor)
        {
            var row = CreateLayoutRow("OddsRow", 64f, RowColor);
            var leftLabel = CreateText(row.transform, left, 31f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            leftLabel.color = leftColor;
            AddFlexibleLayout(leftLabel.gameObject, 1f, 0f);
            var rightLabel = CreateText(row.transform, right, 31f, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            AddFixedLayout(rightLabel.gameObject, 170f, 55f);
        }

        private void AddParagraph(
            string text,
            float fontSize,
            float preferredHeight,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
            FontStyles style = FontStyles.Normal,
            Color? color = null)
        {
            var label = CreateText(_content, text, fontSize, style, alignment);
            label.textWrappingMode = TextWrappingModes.Normal;
            if (color.HasValue)
            {
                label.color = color.Value;
            }

            AddFixedLayout(label.gameObject, -1f, preferredHeight);
            _dynamicObjects.Add(label.gameObject);
        }

        private GameObject CreateLayoutRow(string name, float height, Color color)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(_content, false);
            row.GetComponent<Image>().color = color;
            row.GetComponent<LayoutElement>().preferredHeight = height;
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 12, 12);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            _dynamicObjects.Add(row);
            return row;
        }

        private void ConfigureFooter(string text, UnityAction action)
        {
            _footerLabel.text = text ?? string.Empty;
            _footerButton.onClick.RemoveAllListeners();
            if (action != null)
            {
                _footerButton.onClick.AddListener(action);
            }
        }

        private void ClearDynamicContent()
        {
            for (var i = 0; i < _dynamicObjects.Count; i++)
            {
                if (_dynamicObjects[i] != null)
                {
                    _dynamicObjects[i].SetActive(false);
                    Destroy(_dynamicObjects[i]);
                }
            }

            _dynamicObjects.Clear();
        }

        private static string GetTierProgressText(
            EggDescriptor egg,
            EggTierDescriptor tier,
            IEggProgressService progress,
            bool isUnlocked)
        {
            if (isUnlocked)
            {
                return "Unlocked";
            }

            var previousTier = egg.Tiers[Mathf.Max(0, tier.Order - 2)];
            var count = progress.GetHatchCount(egg.EggId, previousTier.TierId);
            return $"{Mathf.Min(count, tier.RequiredPreviousTierHatches)}/{tier.RequiredPreviousTierHatches} previous-tier hatches";
        }

        private static string GetTierPriceText(EggTierDescriptor tier)
        {
            tier.TryGetHatchOption(EggHatchOptionIds.CoinsSingle, out var coins);
            tier.TryGetHatchOption(EggHatchOptionIds.PremiumSingle, out var premium);
            var coinText = coins == null ? string.Empty : $"{coins.Price:N0} coins";
            var premiumText = premium == null ? string.Empty : $"{premium.Price:N0} premium each";
            return string.IsNullOrEmpty(coinText) ? premiumText : $"{coinText}  •  {premiumText}";
        }

        private static string GetItemName(IItemCatalog itemCatalog, string itemId)
        {
            return itemCatalog != null && itemCatalog.TryGet(itemId, out var item)
                ? item.DisplayName
                : itemId;
        }

        private static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Rare => new Color(0.18f, 0.72f, 1f, 1f),
                ItemRarity.Epic => new Color(0.66f, 0.29f, 1f, 1f),
                ItemRarity.Legendary => new Color(1f, 0.67f, 0.08f, 1f),
                ItemRarity.Mythic => new Color(1f, 0.22f, 0.48f, 1f),
                _ => new Color(0.85f, 0.9f, 1f, 1f)
            };
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform as RectTransform;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string text,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            var gameObject = new GameObject("Text", typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var label = gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text ?? string.Empty;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(
            RectTransform parent,
            string text,
            Color color,
            UnityAction action)
        {
            var buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = color;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            var label = CreateText(buttonObject.transform, text, 31f, FontStyles.Bold, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static void AddFixedLayout(GameObject target, float width, float height)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            if (width >= 0f)
            {
                layout.preferredWidth = width;
            }

            layout.preferredHeight = height;
        }

        private static void AddFlexibleLayout(GameObject target, float flexibleWidth, float flexibleHeight)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.flexibleWidth = flexibleWidth;
            layout.flexibleHeight = flexibleHeight;
        }
    }
}
