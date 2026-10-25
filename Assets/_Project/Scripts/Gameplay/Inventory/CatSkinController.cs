using System;
using PawArena.Core.Inventory;
using UnityEngine;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>Applies inventory skin materials in place, without touching Animator, bones or colliders.</summary>
    [DisallowMultipleComponent]
    public sealed class CatSkinController : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer _bodyRenderer = null;
        private readonly Material[] _materials = new Material[2];
        private CatSkinLibrary _library;
        private IInventoryService _inventory;
        private bool _subscribed;

        /// <summary>The successfully applied appearance ID, not the UI selection.</summary>
        public string CurrentSkinId { get; private set; }

        private void Awake()
        {
            if (_bodyRenderer == null) { _bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true); }
        }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        /// <summary>Binds this character to a local inventory; explicit injection supports later multiplayer ownership.</summary>
        public void Initialize(CatSkinLibrary library, IInventoryService inventory)
        {
            Unsubscribe();
            _library = library != null ? library : throw new ArgumentNullException(nameof(library));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            if (_bodyRenderer == null) { _bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true); }
            if (isActiveAndEnabled) { Subscribe(); Refresh(); }
        }

        /// <summary>Applies a validated material pair. Intended for previews; gameplay ownership is enforced by inventory.</summary>
        public bool TryApply(CatSkinDefinition skin)
        {
            if (_bodyRenderer == null) { _bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true); }
            if (_bodyRenderer == null || skin == null || !skin.IsValid ||
                _bodyRenderer.sharedMesh != skin.CompatibleMesh || _bodyRenderer.sharedMaterials.Length != 2)
            {
                return false;
            }
            _materials[0] = skin.Coat;
            _materials[1] = skin.Eyes;
            _bodyRenderer.sharedMaterials = _materials;
            CurrentSkinId = skin.ItemId;
            return true;
        }

        private void Subscribe()
        {
            if (_subscribed || _inventory == null) { return; }
            _inventory.ItemEquipped += HandleEquipment;
            _inventory.ItemUnequipped += HandleEquipment;
            _subscribed = true;
        }
        private void Unsubscribe()
        {
            if (!_subscribed) { return; }
            _inventory.ItemEquipped -= HandleEquipment;
            _inventory.ItemUnequipped -= HandleEquipment;
            _subscribed = false;
        }
        private void HandleEquipment(EquipmentSlotId slot, string itemId)
        {
            if (slot == SkinInventorySession.SkinSlot) { Refresh(); }
        }
        private void Refresh()
        {
            if (_inventory == null || _library == null) { return; }
            var skin = _inventory.TryGetEquipped(SkinInventorySession.SkinSlot, out var id)
                ? _library.Find(id) : _library.DefaultSkin;
            if (skin != null && CurrentSkinId == skin.ItemId) { return; }
            if (!TryApply(skin)) { Debug.LogError("Cat skin is missing or incompatible with the assigned renderer.", this); }
        }
    }
}
