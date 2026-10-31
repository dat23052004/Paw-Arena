using System;
using System.Collections.Generic;
using PawArena.Core.Eggs;
using PawArena.Core.Inventory;
using PawArena.Core.Services;
using UnityEngine;

namespace PawArena.UI.Shop
{
    /// <summary>Connects shop presentation to the authoritative egg domain services.</summary>
    [DisallowMultipleComponent]
    public sealed class EggShopRuntimeController : MonoBehaviour
    {
        private readonly Dictionary<string, string> _selectedTierIds = new(StringComparer.Ordinal);

        private ShopScreenView _view;
        private IEggCatalog _eggCatalog;
        private IEggHatchService _hatchService;
        private IEggProgressService _progress;
        private IItemCatalog _itemCatalog;
        private EggShopOverlayView _overlay;
        private bool _initialized;

        public bool Initialize(ShopScreenView view, ShopCatalog presentationCatalog)
        {
            if (_initialized)
            {
                return true;
            }

            _view = view;
            if (_view == null
                || !ServiceRegistry.TryGet(out _eggCatalog)
                || !ServiceRegistry.TryGet(out _hatchService)
                || !ServiceRegistry.TryGet(out _progress)
                || !ServiceRegistry.TryGet(out _itemCatalog))
            {
                Debug.LogWarning("Egg services are unavailable; Shop keeps its presentation-only fallback.", this);
                return false;
            }

            for (var i = 0; i < _eggCatalog.Eggs.Count; i++)
            {
                var egg = _eggCatalog.Eggs[i];
                _selectedTierIds[egg.EggId] = _progress.GetHighestUnlockedTier(egg).TierId;
            }

            var overlayObject = new GameObject("EggShopOverlay", typeof(RectTransform));
            overlayObject.transform.SetParent(_view.transform, false);
            _overlay = overlayObject.AddComponent<EggShopOverlayView>();
            _overlay.Initialize(presentationCatalog);

            _view.PurchaseRequested += HandlePurchaseRequested;
            _view.EggOddsRequested += HandleOddsRequested;
            _view.EggTiersRequested += HandleTiersRequested;
            _view.EggGiftRequested += HandleGiftRequested;
            _initialized = true;
            return true;
        }

        public bool TryGetBinding(
            string eggId,
            out EggDescriptor egg,
            out EggTierDescriptor selectedTier)
        {
            selectedTier = null;
            if (!_initialized || !_eggCatalog.TryGet(eggId, out egg))
            {
                egg = null;
                return false;
            }

            if (_selectedTierIds.TryGetValue(eggId, out var tierId)
                && egg.TryGetTier(tierId, out selectedTier)
                && _progress.IsTierUnlocked(egg, selectedTier))
            {
                return true;
            }

            selectedTier = _progress.GetHighestUnlockedTier(egg);
            _selectedTierIds[eggId] = selectedTier.TierId;
            return true;
        }

        private void OnDestroy()
        {
            if (_view == null)
            {
                return;
            }

            _view.PurchaseRequested -= HandlePurchaseRequested;
            _view.EggOddsRequested -= HandleOddsRequested;
            _view.EggTiersRequested -= HandleTiersRequested;
            _view.EggGiftRequested -= HandleGiftRequested;
        }

        private void HandlePurchaseRequested(ShopPurchaseRequest request)
        {
            if (!_initialized
                || request.Kind != ShopPurchaseKind.Hatch
                || string.IsNullOrWhiteSpace(request.TierId)
                || string.IsNullOrWhiteSpace(request.HatchOptionId))
            {
                return;
            }

            var result = _hatchService.TryHatch(new EggHatchRequest(
                Guid.NewGuid().ToString("N"),
                request.ProductId,
                request.TierId,
                request.HatchOptionId));

            if (!result.IsSuccess)
            {
                _overlay.ShowMessage("Hatch unavailable", GetFailureMessage(result.Status));
                return;
            }

            _view.Refresh();
            _overlay.ShowReveal(result, _itemCatalog);
        }

        private void HandleOddsRequested(string eggId)
        {
            if (!TryGetBinding(eggId, out var egg, out var tier))
            {
                return;
            }

            _overlay.ShowOdds(egg, tier, _itemCatalog, egg.Tiers.Count > 1);
        }

        private void HandleTiersRequested(string eggId)
        {
            if (!TryGetBinding(eggId, out var egg, out var tier))
            {
                return;
            }

            _overlay.ShowTiers(
                egg,
                tier.TierId,
                _progress,
                selectedTier => SelectTier(egg, selectedTier),
                oddsTier => _overlay.ShowOdds(egg, oddsTier, _itemCatalog, true));
        }

        private void HandleGiftRequested(string eggId)
        {
            _overlay.ShowMessage("Gift", "Gift to a friend is coming soon. This button is UI-only for now.");
        }

        private void SelectTier(EggDescriptor egg, EggTierDescriptor tier)
        {
            if (!_progress.IsTierUnlocked(egg, tier))
            {
                _overlay.ShowMessage(
                    "Tier locked",
                    $"Hatch {tier.RequiredPreviousTierHatches} times in the previous tier first.");
                return;
            }

            _selectedTierIds[egg.EggId] = tier.TierId;
            _overlay.Hide();
            _view.Refresh();
        }

        private static string GetFailureMessage(EggHatchStatus status)
        {
            return status switch
            {
                EggHatchStatus.InsufficientFunds => "You do not have enough currency for this hatch.",
                EggHatchStatus.TierLocked => "This tier is still locked.",
                EggHatchStatus.NotStarted => "This egg event has not started yet.",
                EggHatchStatus.Expired => "This egg event has ended.",
                EggHatchStatus.DuplicateRequest => "This hatch request was already handled.",
                EggHatchStatus.InvalidConfiguration => "This egg has an invalid reward configuration.",
                _ => "The hatch could not be completed. Please try again."
            };
        }
    }
}
