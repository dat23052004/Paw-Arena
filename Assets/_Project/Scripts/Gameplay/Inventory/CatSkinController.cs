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
        [SerializeField] private GameObject _defaultOutfitRoot = null;
        private Material[] _fallbackMaterials = Array.Empty<Material>();
        private CatSkinLibrary _library;
        private IInventoryService _inventory;
        private AsyncOperationHandle<Material> _activeCoatHandle;
        private AsyncOperationHandle<Material> _pendingCoatHandle;
        private Coroutine _loadRoutine;
        private string _requestedSkinId;
        private int _loadVersion;
        private bool _hasActiveCoat;
        private bool _hasPendingCoat;
        private bool _subscribed;
        [SerializeField, HideInInspector] private SkinnedMeshRenderer _skinDetailRenderer;

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
            if (CanApply(skin) && skin.Coat != null)
            {
                CancelPendingLoad();
                ReleaseActiveHandles(false);
                return TryApplyResolved(skin, skin.Coat);
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
            _hasPendingCoat = true;

            yield return _pendingCoatHandle;

            if (version != _loadVersion || !isActiveAndEnabled)
            {
                ReleasePendingHandles();
                yield break;
            }

            if (_pendingCoatHandle.Status != AsyncOperationStatus.Succeeded ||
                !TryApplyResolved(skin, _pendingCoatHandle.Result))
            {
                Debug.LogError($"Could not load compatible skin materials for '{skin.ItemId}'.", this);
                _requestedSkinId = null;
                ReleasePendingHandles();
                _loadRoutine = null;
                yield break;
            }

            ReleaseActiveHandles(false);
            _activeCoatHandle = _pendingCoatHandle;
            _hasActiveCoat = true;
            _hasPendingCoat = false;
            _requestedSkinId = null;
            _loadRoutine = null;
        }

        private bool TryApplyResolved(CatSkinDefinition skin, Material coat)
        {
            if (_bodyRenderer == null) { _bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true); }
            if (!CanApply(skin) || coat == null)
            {
                return false;
            }

            if (!ApplyDetailGeometry(skin)) { return false; }

            var materials = _bodyRenderer.sharedMaterials;
            materials[0] = coat;
            _bodyRenderer.sharedMaterials = materials;
            if (_defaultOutfitRoot != null)
            {
                _defaultOutfitRoot.SetActive(skin.WearDefaultOutfit);
            }
            CurrentSkinId = skin.ItemId;
            return true;
        }

        private bool ApplyDetailGeometry(CatSkinDefinition skin)
        {
            if (skin.DetailMesh == null)
            {
                if (_skinDetailRenderer != null) { _skinDetailRenderer.gameObject.SetActive(false); }
                return true;
            }
            if (skin.DetailMaterial == null || skin.DetailBoneNames.Count != skin.DetailMesh.bindposes.Length)
            {
                Debug.LogError($"Incomplete skin geometry for '{skin.ItemId}'.", this);
                return false;
            }
            var sourceBones = _bodyRenderer.bones;
            var bones = new Transform[skin.DetailBoneNames.Count];
            for (var i = 0; i < bones.Length; i++)
            {
                foreach (var bone in sourceBones)
                {
                    if (bone != null && bone.name == skin.DetailBoneNames[i]) { bones[i] = bone; break; }
                }
                if (bones[i] == null)
                {
                    Debug.LogError($"Skin '{skin.ItemId}' requires bone '{skin.DetailBoneNames[i]}'.", this);
                    return false;
                }
            }
            if (_skinDetailRenderer == null)
            {
                var detail = new GameObject("Skin Detail Geometry");
                detail.layer = _bodyRenderer.gameObject.layer;
                detail.transform.SetParent(_bodyRenderer.transform, false);
                _skinDetailRenderer = detail.AddComponent<SkinnedMeshRenderer>();
            }
            _skinDetailRenderer.sharedMesh = skin.DetailMesh;
            _skinDetailRenderer.sharedMaterial = skin.DetailMaterial;
            _skinDetailRenderer.bones = bones;
            _skinDetailRenderer.rootBone = _bodyRenderer.rootBone;
            var bounds = skin.DetailMesh.bounds;
            bounds.Encapsulate(_bodyRenderer.localBounds);
            bounds.Expand(0.5f);
            _skinDetailRenderer.localBounds = bounds;
            _skinDetailRenderer.quality = SkinQuality.Bone4;
            _skinDetailRenderer.updateWhenOffscreen = _bodyRenderer.updateWhenOffscreen;
            _skinDetailRenderer.gameObject.SetActive(true);
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
                   _bodyRenderer.sharedMaterials.Length > 0;
        }

        private void CacheFallbackMaterials()
        {
            if (_bodyRenderer == null) { return; }
            var shared = _bodyRenderer.sharedMaterials;
            _fallbackMaterials = new Material[shared.Length];
            Array.Copy(shared, _fallbackMaterials, shared.Length);
        }

        private void ResetLoadedSkin()
        {
            if (_skinDetailRenderer != null) { _skinDetailRenderer.gameObject.SetActive(false); }
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
            _hasPendingCoat = false;
        }

        private void ReleaseActiveHandles(bool restoreFallback)
        {
            if (restoreFallback && _bodyRenderer != null && _fallbackMaterials.Length > 0)
            {
                _bodyRenderer.sharedMaterials = _fallbackMaterials;
            }
            if (_hasActiveCoat && _activeCoatHandle.IsValid())
            {
                Addressables.Release(_activeCoatHandle);
            }
            _hasActiveCoat = false;
        }
    }
}
