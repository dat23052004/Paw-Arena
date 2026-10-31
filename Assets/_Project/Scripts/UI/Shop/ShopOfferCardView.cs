using System;
using System.Collections.Generic;
using PawArena.Core.Eggs;
using PawArena.Core.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PawArena.UI.Shop
{
    /// <summary>Reusable card renderer for egg, bundle and coin-pack catalog entries.</summary>
    [DisallowMultipleComponent]
    public sealed class ShopOfferCardView : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private Image _artwork = null;
        [SerializeField] private TMP_Text _title = null;
        [SerializeField] private TMP_Text _subtitle = null;
        [SerializeField] private TMP_Text _badge = null;
        [SerializeField] private TMP_Text _countdown = null;
        [SerializeField] private GameObject _soldOutOverlay = null;

        [Header("Rewards")]
        [SerializeField] private RectTransform _rewardContainer = null;
        [SerializeField] private ShopRewardCardView _rewardTemplate = null;

        [Header("Actions")]
        [SerializeField] private Button _secondaryButton = null;
        [SerializeField] private TMP_Text _secondaryLabel = null;
        [SerializeField] private Button _primaryButton = null;
        [SerializeField] private TMP_Text _primaryLabel = null;
        [SerializeField] private Button _buyOneButton = null;
        [SerializeField] private TMP_Text _buyOneLabel = null;
        [SerializeField] private Button _buyThreeButton = null;
        [SerializeField] private TMP_Text _buyThreeLabel = null;
        [SerializeField] private Button _buyTenButton = null;
        [SerializeField] private TMP_Text _buyTenLabel = null;
        [SerializeField] private Button _giftButton = null;

        private readonly List<ShopRewardCardView> _rewardCards = new();
        private readonly List<ButtonBinding> _buttonBindings = new();
        private string _endsAtUtc = string.Empty;

        /// <summary>Applies one egg definition and forwards all purchase controls as intents.</summary>
        public void BindEgg(ShopCatalog.EggOffer offer, Action<ShopPurchaseRequest> onPurchase)
        {
            ResetBindings();
            if (offer == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            ApplyCommon(offer.Title, offer.Subtitle, offer.Artwork, offer.EndsAtUtc);
            SetText(_badge, offer.LimitedTime ? "LIMITED TIME" : string.Empty);
            SetActive(_soldOutOverlay, false);
            RefreshRewards(offer.Rewards);

            ConfigureButton(
                _secondaryButton,
                _secondaryLabel,
                offer.ShowTiers ? "Tiers" : "Odds",
                true,
                false,
                null);
            ConfigureButton(
                _primaryButton,
                _primaryLabel,
                $"Hatch  {FormatPrice(offer.HatchPrice)}",
                true,
                offer.HatchPrice >= 0,
                () => onPurchase?.Invoke(new ShopPurchaseRequest(
                    ShopPurchaseKind.Hatch,
                    offer.Id,
                    1,
                    offer.HatchCurrency,
                    offer.HatchPrice,
                    false)));
            ConfigureButton(
                _buyOneButton,
                _buyOneLabel,
                FormatPrice(offer.BuyOnePrice),
                offer.BuyOnePrice > 0,
                true,
                () => RaiseEggPack(onPurchase, offer, 1, offer.BuyOnePrice, false));
            ConfigureButton(
                _buyThreeButton,
                _buyThreeLabel,
                $"×3  {FormatPrice(offer.BuyThreePrice)}",
                offer.BuyThreePrice > 0,
                true,
                () => RaiseEggPack(onPurchase, offer, 3, offer.BuyThreePrice, false));
            ConfigureButton(
                _buyTenButton,
                _buyTenLabel,
                $"×10  {FormatPrice(offer.BuyTenPrice)}",
                offer.BuyTenPrice > 0,
                true,
                () => RaiseEggPack(onPurchase, offer, 10, offer.BuyTenPrice, false));
            ConfigureGiftButton(
                offer.BuyOnePrice > 0,
                () => RaiseEggPack(onPurchase, offer, 1, offer.BuyOnePrice, true));
            RefreshCountdown(DateTimeOffset.UtcNow);
        }

        /// <summary>Applies authoritative egg and selected-tier data while retaining catalog artwork.</summary>
        public void BindEgg(
            ShopCatalog.EggOffer offer,
            EggDescriptor egg,
            EggTierDescriptor tier,
            Action<ShopPurchaseRequest> onPurchase,
            Action<string> onOdds,
            Action<string> onTiers,
            Action<string> onGift)
        {
            ResetBindings();
            if (offer == null || egg == null || tier == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            var subtitle = egg.Tiers.Count > 1
                ? $"{offer.Subtitle}  •  {tier.DisplayName}"
                : offer.Subtitle;
            var endsAt = egg.EndsAtUtc?.ToString("O") ?? string.Empty;
            ApplyCommon(offer.Title, subtitle, offer.Artwork, endsAt);
            SetText(_badge, egg.IsLimitedTime ? "LIMITED TIME" : string.Empty);
            SetActive(_soldOutOverlay, false);
            RefreshRewards(offer.Rewards);

            var isAvailable = egg.IsAvailable(DateTimeOffset.UtcNow);
            ConfigureButton(
                _secondaryButton,
                _secondaryLabel,
                egg.Tiers.Count > 1 ? "Tiers" : "Odds",
                true,
                true,
                egg.Tiers.Count > 1
                    ? () => onTiers?.Invoke(egg.EggId)
                    : () => onOdds?.Invoke(egg.EggId));
            ConfigureEggOption(
                _primaryButton,
                _primaryLabel,
                egg.EggId,
                tier,
                EggHatchOptionIds.CoinsSingle,
                "Hatch",
                isAvailable,
                onPurchase);
            ConfigureEggOption(
                _buyOneButton,
                _buyOneLabel,
                egg.EggId,
                tier,
                EggHatchOptionIds.PremiumSingle,
                string.Empty,
                isAvailable,
                onPurchase);
            ConfigureEggOption(
                _buyThreeButton,
                _buyThreeLabel,
                egg.EggId,
                tier,
                EggHatchOptionIds.PremiumTriple,
                "×3",
                isAvailable,
                onPurchase);
            ConfigureEggOption(
                _buyTenButton,
                _buyTenLabel,
                egg.EggId,
                tier,
                EggHatchOptionIds.PremiumTen,
                "×10",
                isAvailable,
                onPurchase);
            ConfigureGiftButton(true, () => onGift?.Invoke(egg.EggId));
            RefreshCountdown(DateTimeOffset.UtcNow);
        }

        /// <summary>Applies one bundle definition and its availability state.</summary>
        public void BindBundle(ShopCatalog.BundleOffer offer, Action<ShopPurchaseRequest> onPurchase)
        {
            ResetBindings();
            if (offer == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            ApplyCommon(offer.Title, offer.Subtitle, offer.Artwork, offer.EndsAtUtc);
            var badge = offer.SoldOut
                ? "SOLD OUT"
                : offer.DiscountPercent > 0
                    ? $"{offer.DiscountPercent}% OFF"
                    : offer.SpecialPack ? "SPECIAL" : string.Empty;
            SetText(_badge, badge);
            SetActive(_soldOutOverlay, offer.SoldOut);
            RefreshRewards(offer.Rewards);

            var priceLabel = offer.OriginalPrice > offer.Price
                ? $"{FormatPrice(offer.Price)}  (was {FormatPrice(offer.OriginalPrice)})"
                : FormatPrice(offer.Price);
            ConfigureButton(_secondaryButton, _secondaryLabel, string.Empty, false, false, null);
            ConfigureButton(
                _primaryButton,
                _primaryLabel,
                offer.SoldOut ? "SOLD OUT" : priceLabel,
                true,
                !offer.SoldOut,
                () => onPurchase?.Invoke(new ShopPurchaseRequest(
                    ShopPurchaseKind.Bundle,
                    offer.Id,
                    1,
                    offer.Currency,
                    offer.Price,
                    false)));
            ConfigureButton(_buyOneButton, _buyOneLabel, string.Empty, false, false, null);
            ConfigureButton(_buyThreeButton, _buyThreeLabel, string.Empty, false, false, null);
            ConfigureButton(_buyTenButton, _buyTenLabel, string.Empty, false, false, null);
            ConfigureGiftButton(
                !offer.SoldOut,
                () => onPurchase?.Invoke(new ShopPurchaseRequest(
                    ShopPurchaseKind.Bundle,
                    offer.Id,
                    1,
                    offer.Currency,
                    offer.Price,
                    true)));
            RefreshCountdown(DateTimeOffset.UtcNow);
        }

        /// <summary>Applies one coin pack without executing its platform purchase.</summary>
        public void BindCoinPack(ShopCatalog.CoinPackOffer offer, Action<ShopPurchaseRequest> onPurchase)
        {
            ResetBindings();
            if (offer == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            ApplyCommon(
                string.IsNullOrWhiteSpace(offer.Title) ? $"{offer.CoinAmount:N0} Coins" : offer.Title,
                $"{offer.CoinAmount:N0} Coins",
                offer.Artwork,
                string.Empty);
            SetText(_badge, offer.BonusPercent > 0 ? $"+{offer.BonusPercent}%" : string.Empty);
            SetActive(_soldOutOverlay, false);
            RefreshRewards(Array.Empty<ShopCatalog.RewardDefinition>());

            ConfigureButton(_secondaryButton, _secondaryLabel, string.Empty, false, false, null);
            ConfigureButton(
                _primaryButton,
                _primaryLabel,
                FormatPrice(offer.PremiumPrice),
                true,
                true,
                () => onPurchase?.Invoke(new ShopPurchaseRequest(
                    ShopPurchaseKind.CoinPack,
                    offer.Id,
                    1,
                    CurrencyType.Premium,
                    offer.PremiumPrice,
                    false)));
            ConfigureButton(_buyOneButton, _buyOneLabel, string.Empty, false, false, null);
            ConfigureButton(_buyThreeButton, _buyThreeLabel, string.Empty, false, false, null);
            ConfigureButton(_buyTenButton, _buyTenLabel, string.Empty, false, false, null);
            ConfigureGiftButton(
                true,
                () => onPurchase?.Invoke(new ShopPurchaseRequest(
                    ShopPurchaseKind.CoinPack,
                    offer.Id,
                    1,
                    CurrencyType.Premium,
                    offer.PremiumPrice,
                    true)));
        }

        /// <summary>Refreshes the countdown using an externally supplied clock value.</summary>
        public void RefreshCountdown(DateTimeOffset now)
        {
            if (_countdown == null)
            {
                return;
            }

            if (!DateTimeOffset.TryParse(_endsAtUtc, out var endsAt))
            {
                _countdown.gameObject.SetActive(false);
                return;
            }

            _countdown.gameObject.SetActive(true);
            var remaining = endsAt.ToUniversalTime() - now.ToUniversalTime();
            if (remaining <= TimeSpan.Zero)
            {
                _countdown.text = "Ended";
                return;
            }

            _countdown.text = remaining.Days > 0
                ? $"{remaining.Days:00}d {remaining.Hours:00}h {remaining.Minutes:00}m"
                : $"{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        private void OnDestroy() => ResetBindings();

        private void ApplyCommon(string title, string subtitle, Sprite artwork, string endsAtUtc)
        {
            SetText(_title, title);
            SetText(_subtitle, subtitle);
            _endsAtUtc = endsAtUtc ?? string.Empty;
            if (_artwork != null)
            {
                _artwork.sprite = artwork;
                _artwork.enabled = artwork != null;
            }
        }

        private void RefreshRewards(IReadOnlyList<ShopCatalog.RewardDefinition> rewards)
        {
            for (var i = 0; i < _rewardCards.Count; i++)
            {
                if (_rewardCards[i] != null)
                {
                    Destroy(_rewardCards[i].gameObject);
                }
            }

            _rewardCards.Clear();
            if (_rewardTemplate == null || _rewardContainer == null)
            {
                return;
            }

            _rewardTemplate.gameObject.SetActive(false);
            for (var i = 0; i < rewards.Count; i++)
            {
                var card = Instantiate(_rewardTemplate, _rewardContainer);
                card.name = $"Reward_{i + 1}";
                card.Bind(rewards[i]);
                _rewardCards.Add(card);
            }
        }

        private static void RaiseEggPack(
            Action<ShopPurchaseRequest> onPurchase,
            ShopCatalog.EggOffer offer,
            int quantity,
            long price,
            bool isGift)
        {
            onPurchase?.Invoke(new ShopPurchaseRequest(
                ShopPurchaseKind.EggPack,
                offer.Id,
                quantity,
                offer.PackCurrency,
                price,
                isGift));
        }

        private void ConfigureEggOption(
            Button button,
            TMP_Text label,
            string eggId,
            EggTierDescriptor tier,
            string optionId,
            string prefix,
            bool isAvailable,
            Action<ShopPurchaseRequest> onPurchase)
        {
            if (!tier.TryGetHatchOption(optionId, out var option))
            {
                ConfigureButton(button, label, string.Empty, false, false, null);
                return;
            }

            var currencyMark = option.Currency == CurrencyType.Coins ? "Coins" : "Premium";
            var text = string.IsNullOrWhiteSpace(prefix)
                ? $"{currencyMark} {FormatPrice(option.Price)}"
                : $"{prefix}  {currencyMark} {FormatPrice(option.Price)}";
            ConfigureButton(
                button,
                label,
                text,
                true,
                isAvailable,
                () => onPurchase?.Invoke(new ShopPurchaseRequest(
                    eggId,
                    tier.TierId,
                    option.OptionId,
                    option.Quantity,
                    option.Currency,
                    option.Price)));
        }

        private void ConfigureGiftButton(bool visible, UnityAction action)
        {
            if (_giftButton == null)
            {
                return;
            }

            _giftButton.gameObject.SetActive(visible);
            _giftButton.interactable = visible;
            AddBinding(_giftButton, visible ? action : null);
        }

        private void ConfigureButton(
            Button button,
            TMP_Text label,
            string text,
            bool visible,
            bool interactable,
            UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(visible);
            button.interactable = visible && interactable;
            SetText(label, text);
            AddBinding(button, visible && interactable ? action : null);
        }

        private void AddBinding(Button button, UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.AddListener(action);
            _buttonBindings.Add(new ButtonBinding(button, action));
        }

        private void ResetBindings()
        {
            for (var i = 0; i < _buttonBindings.Count; i++)
            {
                var binding = _buttonBindings[i];
                if (binding.Button != null)
                {
                    binding.Button.onClick.RemoveListener(binding.Action);
                }
            }

            _buttonBindings.Clear();
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static string FormatPrice(long price) => price.ToString("N0");

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
