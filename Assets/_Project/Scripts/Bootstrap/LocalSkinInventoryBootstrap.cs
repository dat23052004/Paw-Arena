using System;
using PawArena.Core.Inventory;
using PawArena.Core.Profiles;
using PawArena.Gameplay.Inventory;
using PawArena.Infrastructure.Persistence;
using PawArena.Infrastructure.Time;
using PawArena.UI.Inventory;
using UnityEngine;

namespace PawArena.Bootstrap
{
    /// <summary>Standalone offline dressing room. Reuses the existing inventory/profile implementation.</summary>
    public sealed class LocalSkinInventoryBootstrap : MonoBehaviour
    {
        [SerializeField] private ItemCatalog _catalog = null;
        [SerializeField] private CatSkinLibrary _skins = null;
        [SerializeField] private CatSkinController _character = null;
        [SerializeField] private SkinInventoryView _view = null;
        [SerializeField] private CatGearLibrary _gear = null;
        [SerializeField] private CatGearController _gearController = null;
        [Tooltip("Only for the standalone test room. Does not add purchases or combat stats.")]
        [SerializeField] private bool _grantAllGearForTesting = false;
        [SerializeField] private string _profileId = "local-skins-preview";
        [Tooltip("Only for the standalone test room. Disable for normal progression.")]
        [SerializeField] private bool _grantAllSkinsForTesting = false;
        private SkinInventorySession _session;

        /// <summary>The active local skin session, also usable by other local UI.</summary>
        public SkinInventorySession Session => _session;

        private void Start()
        {
            if (_session != null) { return; }
            if (_catalog == null || _skins == null || _character == null || _view == null)
            {
                Debug.LogError("Assign catalog, skin library, character and inventory view.", this);
                enabled = false;
                return;
            }
            try
            {
                BindProfile(new ProfileService(_profileId, new JsonProfileStore(), _catalog, new SystemClock()));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Local skin inventory could not start: {exception.Message}", this);
                enabled = false;
            }
        }

        /// <summary>Wires an existing profile instead of introducing a second inventory service.</summary>
        public void BindProfile(IProfileService profile)
        {
            if (profile == null) { throw new ArgumentNullException(nameof(profile)); }
            _view.Unbind();
            _session?.Dispose();
            if (_grantAllSkinsForTesting)
            {
                foreach (var skin in _skins.Skins) { profile.Inventory.TryGrant(skin.ItemId); }
            }
            if (_gear != null && _grantAllGearForTesting)
            {
                foreach (var gear in _gear.Gear) { profile.Inventory.TryGrant(gear.ItemId); }
            }
            if (!profile.Inventory.TryGetEquipped(SkinInventorySession.SkinSlot, out _) && _skins.DefaultSkin != null)
            {
                profile.Inventory.TryEquip(_skins.DefaultSkin.ItemId, SkinInventorySession.SkinSlot);
            }
            _session = new SkinInventorySession(profile, _catalog, _gear != null);
            _character.Initialize(_skins, profile.Inventory);
            if (_gear != null && _gearController != null) { _gearController.Initialize(_gear, profile.Inventory); }
            _view.Bind(_session, ResolveThumbnail);
            _session.Save();
        }

        private Texture2D ResolveThumbnail(string itemId) => _skins.Find(itemId)?.Thumbnail ?? _gear?.Find(itemId)?.Thumbnail;
        private void OnApplicationPause(bool paused) { if (paused) { _session?.Save(); } }
        private void OnApplicationQuit() => _session?.Save();
        private void OnDestroy()
        {
            if (_view != null) { _view.Unbind(); }
            _session?.Save();
            _session?.Dispose();
        }
    }
}
