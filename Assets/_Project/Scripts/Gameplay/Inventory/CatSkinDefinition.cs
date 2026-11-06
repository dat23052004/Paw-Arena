using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using PawArena.Core.Inventory;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>Appearance for the approved cat mesh, with optional rig-bound skin geometry.</summary>
    [CreateAssetMenu(fileName = "Skin_Cat", menuName = "Paw Arena/Inventory/Cat Skin")]
    public sealed class CatSkinDefinition : ItemDefinition
    {
        [Header("Development preview")]
        [Tooltip("Temporarily unlocked in Editor / Development Build. Not saved as ownership. Disable after art approval. New fur assets enable this by default.")]
        [SerializeField] private bool _unlockForTesting = true;
        [Header("Shared geometry contract")]
        [SerializeField] private Mesh _compatibleMesh = null;
        [Header("Addressable fur material for V23 renderer slot 0")]
        [SerializeField] private AssetReference _coatReference = null;
        [Tooltip("Legacy eye reference retained so existing catalog assets and Addressable GUIDs stay stable.")]
        [SerializeField] private AssetReference _eyesReference = null;
        [SerializeField] private Texture2D _thumbnail = null;
        [Tooltip("Common/Rare/Epic and recolor-only Legendary skins keep the black default outfit. Mythic skins never use it.")]
        [SerializeField] private bool _wearDefaultOutfit = true;
        [Header("Optional skin geometry in body-renderer local space")]
        [SerializeField] private Mesh _detailMesh = null;
        [SerializeField] private Material _detailMaterial = null;
        [SerializeField] private string[] _detailBoneNames = System.Array.Empty<string>();

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
        /// <summary>Legacy eye material retained for backward-compatible authored data.</summary>
        public AssetReference EyesReference => _eyesReference;
        /// <summary>Small catalog preview, independent of the high-resolution skin atlas.</summary>
        public Texture2D Thumbnail => _thumbnail;
        /// <summary>Optional ornaments sharing the character's existing skeleton.</summary>
        public Mesh DetailMesh => _detailMesh;
        /// <summary>Material for optional skin ornaments.</summary>
        public Material DetailMaterial => _detailMaterial;
        /// <summary>Bone names in the detail mesh's bind-pose order.</summary>
        public System.Collections.Generic.IReadOnlyList<string> DetailBoneNames => _detailBoneNames;
        /// <summary>Whether this fur uses the shared black default outfit.</summary>
        public bool WearDefaultOutfit => Rarity != ItemRarity.Mythic && _wearDefaultOutfit;
        /// <summary>Whether the complete material/mesh contract is assigned.</summary>
        public bool IsValid => _compatibleMesh != null && HasAddressableCoat;

        private bool HasAddressableCoat =>
            _coatReference != null && _coatReference.RuntimeKeyIsValid();

        protected override bool DevelopmentPreview
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return _unlockForTesting;
#else
                return false;
#endif
            }
        }

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
