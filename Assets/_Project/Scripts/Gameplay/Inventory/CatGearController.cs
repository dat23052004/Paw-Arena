using System;
using System.Collections.Generic;
using PawArena.Core.Inventory;
using UnityEngine;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>Rebinds gear to the existing character skeleton. Each slot owns at most one renderer.</summary>
    public sealed class CatGearController : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> _bones = new(StringComparer.Ordinal);
        private readonly SkinnedMeshRenderer[] _renderers = new SkinnedMeshRenderer[CatGearSlots.Count];
        private readonly string[] _ids = new string[CatGearSlots.Count];
        private CatGearLibrary _library;
        private IInventoryService _inventory;
        private bool _listening;

        /// <summary>Returns the actually displayed item in a slot, or null for an empty slot.</summary>
        public string GetDisplayedItem(int slot) => slot >= 0 && slot < _ids.Length ? _ids[slot] : null;

        /// <summary>Uses the same inventory as skins; rebinding disconnects the previous owner.</summary>
        public void Initialize(CatGearLibrary library, IInventoryService inventory)
        {
            StopListening();
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _bones.Clear();
            var animator = GetComponent<Animator>();
            if (animator == null) { throw new InvalidOperationException("Gear requires the approved cat Animator root."); }
            foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
            {
                if (!bone.name.StartsWith("EquippedGear_", StringComparison.Ordinal)) { _bones.TryAdd(bone.name, bone); }
            }
            if (isActiveAndEnabled) { Listen(); RefreshAll(); }
        }

        private void OnEnable() { if (_inventory != null) { Listen(); RefreshAll(); } }
        private void OnDisable() => StopListening();
        private void OnDestroy()
        {
            StopListening();
            for (var i = 0; i < _renderers.Length; i++) { Clear(i); }
        }
        private void Listen()
        {
            if (_listening) { return; }
            _inventory.ItemEquipped += OnEquipment;
            _inventory.ItemUnequipped += OnEquipment;
            _listening = true;
        }
        private void StopListening()
        {
            if (!_listening) { return; }
            _inventory.ItemEquipped -= OnEquipment;
            _inventory.ItemUnequipped -= OnEquipment;
            _listening = false;
        }
        private void OnEquipment(EquipmentSlotId slot, string itemId) { if (CatGearSlots.Contains(slot)) { Refresh(slot.Index); } }
        private void RefreshAll() { for (var i = 0; i < CatGearSlots.Count; i++) { Refresh(i); } }

        private void Refresh(int index)
        {
            var slot = new EquipmentSlotId(EquipmentSlotType.Gear, index);
            if (!_inventory.TryGetEquipped(slot, out var id)) { Clear(index); return; }
            if (_ids[index] == id && _renderers[index] != null) { return; }
            var definition = _library.Find(id);
            if (definition == null || !definition.IsValid || !definition.BuildDescriptor().SupportsSlot(slot))
            {
                Debug.LogError("Invalid gear definition: " + id, this);
                return;
            }
            var mapped = new Transform[definition.BoneNames.Length];
            for (var i = 0; i < mapped.Length; i++)
            {
                if (!_bones.TryGetValue(definition.BoneNames[i], out mapped[i]))
                {
                    Debug.LogError("Gear bone is missing: " + definition.BoneNames[i], this);
                    return;
                }
            }
            if (!_bones.TryGetValue(definition.RootBoneName, out var root))
            {
                Debug.LogError("Gear root bone is missing: " + definition.RootBoneName, this);
                return;
            }
            var instance = new GameObject("EquippedGear_" + id);
            instance.layer = gameObject.layer;
            instance.transform.SetParent(transform, false);
            instance.transform.localPosition = definition.LocalPosition;
            instance.transform.localRotation = definition.LocalRotation;
            instance.transform.localScale = definition.LocalScale;
            var renderer = instance.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = definition.Mesh;
            renderer.sharedMaterial = definition.Material;
            renderer.bones = mapped;
            renderer.rootBone = root;
            renderer.updateWhenOffscreen = true; // Small cosmetic meshes must not freeze when hands leave the view.
            Clear(index);
            _renderers[index] = renderer;
            _ids[index] = id;
        }

        private void Clear(int index)
        {
            if (_renderers[index] != null)
            {
                var instance = _renderers[index].gameObject;
                instance.SetActive(false); // Destroy is deferred in Play Mode; never render both items in the same slot.
                if (Application.isPlaying) { Destroy(instance); } else { DestroyImmediate(instance); }
            }
            _renderers[index] = null;
            _ids[index] = null;
        }
    }
}
