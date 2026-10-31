using System;
using System.Collections;
using PawArena.Core.Inventory;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>Applies inventory skin materials in place, without touching Animator, bones or colliders.</summary>
    [DisallowMultipleComponent]
    public sealed class CatSkinController : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer _bodyRenderer = null;
        private readonly Material[] _materials = new Material[2];
        private readonly Material[] _fallbackMaterials = new Material[2];
        private CatSkinLibrary _library;
        private IInventoryService _inventory;
        private AsyncOperationHandle<Material> _activeCoatHandle;
        private AsyncOperationHandle<Material> _activeEyesHandle;
        private AsyncOperationHandle<Material> _pendingCoatHandle;
        private AsyncOperationHandle<Material> _pendingEyesHandle;
        private Coroutine _loadRoutine;
        private string _requestedSkinId;
        private int _loadVersion;
        private bool _hasActiveCoat;
        private bool _hasActiveEyes;
        private bool _hasPendingCoat;
        private bool _hasPendingEyes;
        private bool _subscribed;

        /// <summary>The successfully applied appearance ID, not the UI selection.</summary>
        public string CurrentSkinId { get; private set; }

        private void Awake()
        {
            if (_bodyRenderer == null) { _bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true); }
            CacheFallbackMaterials();
        }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable()
        {
            Unsubscribe();
            ResetLoadedSkin();
        }
        private void OnDestroy()
        {
            Unsubscribe();
            ResetLoadedSkin();
        }

        /// <summary>Binds this character to a local inventory; explicit injection supports later multiplayer ownership.</summary>
        public void Initialize(CatSkinLibrary library, IInventoryService inventory)
        {
            Unsubscribe();
            ResetLoadedSkin();
            _library = library != null ? library : throw new ArgumentNullException(nameof(library));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            if (_bodyRenderer == null) { _bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true); }
            CacheFallbackMaterials();
            if (isActiveAndEnabled) { Subscribe(); Refresh(); }
        }

        /// <summary>
        /// Applies a validated material pair. Editor previews resolve synchronously; player builds queue an
        /// Addressables request and report whether the request was accepted.
        /// </summary>
        public bool TryApply(CatSkinDefinition skin)
        {
#if UNITY_EDITOR
            if (CanApply(skin) && skin.Coat != null && skin.Eyes != null)
            {
                CancelPendingLoad();
                ReleaseActiveHandles(false);
                return TryApplyResolved(skin, skin.Coat, skin.Eyes);
            }
#endif
            return QueueAddressableApply(skin);
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
            if (skin == null || !skin.IsValid)
            {
                Debug.LogError("Cat skin is missing or has no Addressable material pair.", this);
                return;
            }
            if (!TryApply(skin))
            {
                Debug.LogError($"Cat skin '{skin.ItemId}' is incompatible with the assigned renderer.", this);
            }
        }

        private bool QueueAddressableApply(CatSkinDefinition skin)
        {
            if (!CanApply(skin) || !isActiveAndEnabled)
            {
                return false;
            }

            if (CurrentSkinId == skin.ItemId || _requestedSkinId == skin.ItemId)
            {
                return true;
            }

            CancelPendingLoad();
            _requestedSkinId = skin.ItemId;
            var version = ++_loadVersion;
            _loadRoutine = StartCoroutine(LoadAndApply(skin, version));
            return true;
        }

        private IEnumerator LoadAndApply(CatSkinDefinition skin, int version)
        {
            _pendingCoatHandle = Addressables.LoadAssetAsync<Material>(skin.CoatReference);
            _pendingEyesHandle = Addressables.LoadAssetAsync<Material>(skin.EyesReference);
            _hasPendingCoat = true;
            _hasPendingEyes = true;

            yield return _pendingCoatHandle;
            yield return _pendingEyesHandle;

            if (version != _loadVersion || !isActiveAndEnabled)
            {
                ReleasePendingHandles();
                yield break;
            }

            if (_pendingCoatHandle.Status != AsyncOperationStatus.Succeeded ||
                _pendingEyesHandle.Status != AsyncOperationStatus.Succeeded ||
                !TryApplyResolved(skin, _pendingCoatHandle.Result, _pendingEyesHandle.Result))
            {
                Debug.LogError($"Could not load compatible skin materials for '{skin.ItemId}'.", this);
                _requestedSkinId = null;
                ReleasePendingHandles();
                _loadRoutine = null;
                yield break;
            }

            ReleaseActiveHandles(false);
            _activeCoatHandle = _pendingCoatHandle;
            _activeEyesHandle = _pendingEyesHandle;
            _hasActiveCoat = true;
            _hasActiveEyes = true;
            _hasPendingCoat = false;
            _hasPendingEyes = false;
            _requestedSkinId = null;
            _loadRoutine = null;
        }

        private bool TryApplyResolved(CatSkinDefinition skin, Material coat, Material eyes)
        {
            if (_bodyRenderer == null) { _bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true); }
            if (!CanApply(skin) || coat == null || eyes == null)
            {
                return false;
            }

            _materials[0] = coat;
            _materials[1] = eyes;
            _bodyRenderer.sharedMaterials = _materials;
            CurrentSkinId = skin.ItemId;
            return true;
        }

        private bool CanApply(CatSkinDefinition skin)
        {
            if (_bodyRenderer == null)
            {
                _bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            }

            return _bodyRenderer != null && skin != null && skin.IsValid &&
                   _bodyRenderer.sharedMesh == skin.CompatibleMesh &&
                   _bodyRenderer.sharedMaterials.Length == 2;
        }

        private void CacheFallbackMaterials()
        {
            if (_bodyRenderer == null) { return; }
            var shared = _bodyRenderer.sharedMaterials;
            if (shared.Length != 2) { return; }
            _fallbackMaterials[0] = shared[0];
            _fallbackMaterials[1] = shared[1];
        }

        private void ResetLoadedSkin()
        {
            CancelPendingLoad();
            ReleaseActiveHandles(true);
            CurrentSkinId = null;
            _requestedSkinId = null;
        }

        private void CancelPendingLoad()
        {
            _loadVersion++;
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
            }
            ReleasePendingHandles();
            _requestedSkinId = null;
        }

        private void ReleasePendingHandles()
        {
            if (_hasPendingCoat && _pendingCoatHandle.IsValid())
            {
                Addressables.Release(_pendingCoatHandle);
            }
            if (_hasPendingEyes && _pendingEyesHandle.IsValid())
            {
                Addressables.Release(_pendingEyesHandle);
            }
            _hasPendingCoat = false;
            _hasPendingEyes = false;
        }

        private void ReleaseActiveHandles(bool restoreFallback)
        {
            if (restoreFallback && _bodyRenderer != null &&
                _fallbackMaterials[0] != null && _fallbackMaterials[1] != null)
            {
                _bodyRenderer.sharedMaterials = _fallbackMaterials;
            }
            if (_hasActiveCoat && _activeCoatHandle.IsValid())
            {
                Addressables.Release(_activeCoatHandle);
            }
            if (_hasActiveEyes && _activeEyesHandle.IsValid())
            {
                Addressables.Release(_activeEyesHandle);
            }
            _hasActiveCoat = false;
            _hasActiveEyes = false;
        }
    }
}
