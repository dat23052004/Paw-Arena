using System;
using System.Collections.Generic;
using PawArena.Core.Economy;
using PawArena.Core.Inventory;
using PawArena.Core.Profiles;
using PawArena.Core.Services;
using PawArena.Gameplay.Inventory;
using PawArena.Gameplay.Player;
using PawArena.Infrastructure.Persistence;
using PawArena.Infrastructure.Time;
using PawArena.UI.Hud;
using PawArena.UI.Inventory;
using UnityEngine;

namespace PawArena.Bootstrap
{
    /// <summary>Connects the inventory presentation to the application profile and character visuals.</summary>
    [DisallowMultipleComponent]
    public sealed class InventoryBootstrap : MonoBehaviour
    {
        [SerializeField] private InventoryView _view = null;
        [SerializeField] private LobbyHudView _hudView = null;
        [Tooltip("Used by the standalone UI showcase when the application composition root is absent.")]
        [SerializeField] private ItemCatalog _fallbackCatalog = null;
        [SerializeField] private CatSkinLibrary _skinLibrary = null;
        [SerializeField] private CatGearLibrary _gearLibrary = null;
        [SerializeField] private string _standaloneProfileId = "paw-arena-ui-preview";
        [SerializeField, Min(0)] private long _standaloneStartingCoins = 10000L;

        private readonly Dictionary<string, Sprite> _generatedSprites = new(StringComparer.Ordinal);
        private InventorySession _session;
        private ItemCatalog _presentationCatalog;

        /// <summary>The active inventory session after scene composition completes.</summary>
        public InventorySession Session => _session;

        private void Start()
        {
            if (_session != null)
            {
                return;
            }

            _view ??= GetComponent<InventoryView>();
            _hudView ??= GetComponent<LobbyHudView>();
            if (_view == null)
            {
                Debug.LogError($"{nameof(InventoryView)} is required on the inventory canvas.", this);
                enabled = false;
                return;
            }

            try
            {
                ResolveServices(out var profile, out var catalog);
                _presentationCatalog = catalog as ItemCatalog ?? _fallbackCatalog;
                _session = new InventorySession(profile, catalog);
                BindCharacter(profile.Inventory);
                _view.Bind(_session, ResolveIcon, ResolveDescription);
                _hudView?.Bind(_session.Wallet, () => _session.Save());
            }
            catch (Exception exception)
            {
                Debug.LogError($"Inventory UI could not start: {exception.Message}", this);
                enabled = false;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _session?.Save();
            }
        }

        private void OnApplicationQuit() => _session?.Save();

        private void OnDestroy()
        {
            if (_view != null)
            {
                _view.Unbind();
            }

            _hudView?.Unbind();

            _session?.Dispose();
            foreach (var sprite in _generatedSprites.Values)
            {
                if (sprite != null)
                {
                    Destroy(sprite);
                }
            }

            _generatedSprites.Clear();
        }

        private void ResolveServices(out IProfileService profile, out IItemCatalog catalog)
        {
            if (ServiceRegistry.TryGet<IProfileService>(out profile) &&
                ServiceRegistry.TryGet<IItemCatalog>(out catalog))
            {
                return;
            }

            if (_fallbackCatalog == null)
            {
                throw new InvalidOperationException(
                    "No application profile is registered and no fallback catalog is assigned.");
            }

            catalog = _fallbackCatalog;
            var profileId = string.IsNullOrWhiteSpace(_standaloneProfileId)
                ? "paw-arena-ui-preview"
                : _standaloneProfileId.Trim();
            var standaloneProfile = new ProfileService(
                profileId,
                new JsonProfileStore(),
                catalog,
                new SystemClock());
            if (_standaloneStartingCoins > 0L)
            {
                standaloneProfile.Wallet.TryApply(
                    "starter-coins-v1:" + profileId,
                    CurrencyType.Coins,
                    _standaloneStartingCoins,
                    "starter_balance",
                    string.Empty,
                    out _);
                standaloneProfile.Save();
            }

            profile = standaloneProfile;
        }

        private void BindCharacter(IInventoryService inventory)
        {
            var player = FindAnyObjectByType<CatPlayerController>();
            var skinController = player != null
                ? player.GetComponentInChildren<CatSkinController>(true)
                : FindAnyObjectByType<CatSkinController>();

            if (_skinLibrary != null && skinController != null)
            {
                skinController.Initialize(_skinLibrary, inventory);
            }

            if (_gearLibrary == null || skinController == null)
            {
                return;
            }

            var gearController = skinController.GetComponent<CatGearController>();
            if (gearController == null)
            {
                gearController = skinController.gameObject.AddComponent<CatGearController>();
            }

            gearController.Initialize(_gearLibrary, inventory);
        }

        private Sprite ResolveIcon(string itemId)
        {
            if (_presentationCatalog == null ||
                !_presentationCatalog.TryGetDefinition(itemId, out var definition))
            {
                return null;
            }

            if (definition.Icon != null)
            {
                return definition.Icon;
            }

            if (_generatedSprites.TryGetValue(itemId, out var cached))
            {
                return cached;
            }

            var texture = definition switch
            {
                CatSkinDefinition skin => skin.Thumbnail,
                CatGearDefinition gear => gear.Thumbnail,
                _ => null
            };
            if (texture == null)
            {
                return null;
            }

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = itemId + "_InventoryIcon";
            _generatedSprites.Add(itemId, sprite);
            return sprite;
        }

        private string ResolveDescription(string itemId)
        {
            return _presentationCatalog != null &&
                _presentationCatalog.TryGetDefinition(itemId, out var definition)
                    ? definition.Description
                    : string.Empty;
        }

        private void OnValidate()
        {
            _view ??= GetComponent<InventoryView>();
            _hudView ??= GetComponent<LobbyHudView>();
            _standaloneProfileId = string.IsNullOrWhiteSpace(_standaloneProfileId)
                ? "paw-arena-ui-preview"
                : _standaloneProfileId.Trim();
            _standaloneStartingCoins = Math.Max(0L, _standaloneStartingCoins);
        }
    }
}
