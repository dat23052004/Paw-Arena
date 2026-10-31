using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>Material-only appearance for the approved cat mesh; never stores mutable ownership.</summary>
    [CreateAssetMenu(fileName = "Skin_Cat", menuName = "Paw Arena/Inventory/Cat Skin")]
    public sealed class CatSkinDefinition : ItemDefinition
    {
        [Header("Shared geometry contract")]
        [SerializeField] private Mesh _compatibleMesh = null;
        [Header("Addressable renderer slots: 0 = coat, 1 = eyes")]
        [SerializeField] private AssetReference _coatReference = null;
        [SerializeField] private AssetReference _eyesReference = null;
        [SerializeField] private Texture2D _thumbnail = null;

        // Kept only long enough for the editor migration to preserve existing authored data.
        // The performance installer clears these references after creating Addressable entries.
        [FormerlySerializedAs("_coat"), SerializeField, HideInInspector]
        private Material _legacyCoat = null;
        [FormerlySerializedAs("_eyes"), SerializeField, HideInInspector]
        private Material _legacyEyes = null;

        /// <summary>The single mesh this material pair was authored for.</summary>
        public Mesh CompatibleMesh => _compatibleMesh;
        /// <summary>Lazy-loaded material for renderer slot zero.</summary>
        public AssetReference CoatReference => _coatReference;
        /// <summary>Lazy-loaded material for renderer slot one.</summary>
        public AssetReference EyesReference => _eyesReference;
        /// <summary>Small catalog preview, independent of the high-resolution skin atlas.</summary>
        public Texture2D Thumbnail => _thumbnail;
        /// <summary>Whether the complete material/mesh contract is assigned.</summary>
        public bool IsValid => _compatibleMesh != null && HasAddressableMaterials;

        private bool HasAddressableMaterials =>
            _coatReference != null && _coatReference.RuntimeKeyIsValid() &&
            _eyesReference != null && _eyesReference.RuntimeKeyIsValid();

#if UNITY_EDITOR
        /// <summary>Editor-only resolved coat used by import validation and edit-mode tests.</summary>
        public Material Coat => _coatReference?.editorAsset as Material ?? _legacyCoat;
        /// <summary>Editor-only resolved eye material used by import validation and edit-mode tests.</summary>
        public Material Eyes => _eyesReference?.editorAsset as Material ?? _legacyEyes;
#endif

        protected override void OnValidate()
        {
            SetCategory(Core.Inventory.ItemCategory.Fur);
            base.OnValidate();
        }
    }
}
