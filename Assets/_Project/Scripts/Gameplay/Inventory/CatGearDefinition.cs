using System;
using PawArena.Core.Inventory;
using UnityEngine;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>A cosmetic mesh authored in the approved cat's bind pose. Adds no Animator or gameplay collider.</summary>
    [CreateAssetMenu(fileName = "Gear", menuName = "Paw Arena/Inventory/Cat Gear")]
    public sealed class CatGearDefinition : ItemDefinition
    {
        [SerializeField] private Mesh _mesh;
        [SerializeField] private Material _material;
        [SerializeField] private string[] _boneNames = Array.Empty<string>();
        [SerializeField] private string _rootBoneName = "Root";
        [SerializeField] private Vector3 _localPosition;
        [SerializeField] private Quaternion _localRotation = Quaternion.identity;
        [SerializeField] private Vector3 _localScale = Vector3.one;
        [SerializeField] private Texture2D _thumbnail;

        public Mesh Mesh => _mesh;
        public Material Material => _material;
        public string[] BoneNames => _boneNames;
        public string RootBoneName => _rootBoneName;
        public Vector3 LocalPosition => _localPosition;
        public Quaternion LocalRotation => _localRotation;
        public Vector3 LocalScale => _localScale;
        public Texture2D Thumbnail => _thumbnail;

        /// <summary>Reject incomplete data before replacing an existing cosmetic renderer.</summary>
        public bool IsValid => _mesh != null && _material != null && _boneNames.Length > 0 &&
            _mesh.bindposes.Length == _boneNames.Length && BuildDescriptor().AllowedSlots.Count == 1 &&
            CatGearSlots.Contains(BuildDescriptor().AllowedSlots[0]);

        protected override void OnValidate() { base.OnValidate(); SetCategory(ItemCategory.Gear); }
    }
}
