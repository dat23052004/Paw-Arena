using UnityEngine;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>Material-only appearance for the approved cat mesh; never stores mutable ownership.</summary>
    [CreateAssetMenu(fileName = "Skin_Cat", menuName = "Paw Arena/Inventory/Cat Skin")]
    public sealed class CatSkinDefinition : ItemDefinition
    {
        [Header("Shared geometry contract")]
        [SerializeField] private Mesh _compatibleMesh = null;
        [Header("Renderer slots: 0 = coat, 1 = eyes")]
        [SerializeField] private Material _coat = null;
        [SerializeField] private Material _eyes = null;
        [SerializeField] private Texture2D _thumbnail = null;

        /// <summary>The single mesh this material pair was authored for.</summary>
        public Mesh CompatibleMesh => _compatibleMesh;
        /// <summary>Material for slot zero.</summary>
        public Material Coat => _coat;
        /// <summary>Material for slot one.</summary>
        public Material Eyes => _eyes;
        /// <summary>Small catalog preview, independent of the high-resolution skin atlas.</summary>
        public Texture2D Thumbnail => _thumbnail;
        /// <summary>Whether the complete material/mesh contract is assigned.</summary>
        public bool IsValid => _compatibleMesh != null && _coat != null && _eyes != null;

        protected override void OnValidate()
        {
            SetCategory(Core.Inventory.ItemCategory.Fur);
            base.OnValidate();
        }
    }
}
