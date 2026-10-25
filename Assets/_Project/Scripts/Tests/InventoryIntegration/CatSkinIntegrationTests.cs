using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PawArena.Core.Inventory;
using PawArena.Core.Profiles;
using PawArena.Gameplay.Inventory;
using PawArena.Infrastructure.Persistence;
using PawArena.Infrastructure.Time;
using PawArena.UI.Inventory;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PawArena.Tests.Inventory
{
    /// <summary>Checks real imported materials, animation continuity and disk persistence with an isolated test profile.</summary>
    public sealed class CatSkinIntegrationTests
    {
        private const string Folder = "Assets/_Project/ScriptableObjects/Inventory/CatSkins/";
        private GameObject _character;
        private CatSkinLibrary _library;
        private ItemCatalog _catalog;
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _library = AssetDatabase.LoadAssetAtPath<CatSkinLibrary>(Folder + "CatSkinLibrary.asset");
            _catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(Folder + "CatSkinCatalog.asset");
            Assert.That(_library, Is.Not.Null, "Build the local skin room first.");
            _scene = EditorSceneManager.NewPreviewScene();
            _character = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/Cat/PF_Cat_Base.prefab"), _scene);
        }

        [TearDown]
        public void TearDown() { if (_scene.IsValid()) { EditorSceneManager.ClosePreviewScene(_scene); } }

        [Test]
        public void TryApply_AllNineteenSkins_ChangesOnlyMaterialPair()
        {
            var renderer = _character.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = renderer.sharedMesh;
            var animator = _character.GetComponent<Animator>();
            var avatar = animator.avatar;
            var bones = renderer.bones.ToArray();
            var control = _character.AddComponent<CatSkinController>();
            Assert.That(_library.Skins.Count, Is.EqualTo(19));
            foreach (var skin in _library.Skins)
            {
                Assert.That(control.TryApply(skin), Is.True, skin.ItemId);
                Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { skin.Coat, skin.Eyes }));
                Assert.That(renderer.sharedMesh, Is.SameAs(mesh));
                Assert.That(renderer.bones, Is.EqualTo(bones));
                Assert.That(animator.avatar, Is.SameAs(avatar));
            }
        }

        [Test]
        public void TryApply_InvalidDefinition_PreservesPreviousMaterials()
        {
            var renderer = _character.GetComponentInChildren<SkinnedMeshRenderer>();
            var original = renderer.sharedMaterials;
            var invalid = ScriptableObject.CreateInstance<CatSkinDefinition>();
            try
            {
                Assert.That(_character.AddComponent<CatSkinController>().TryApply(invalid), Is.False);
                Assert.That(renderer.sharedMaterials, Is.EqualTo(original));
            }
            finally { Object.DestroyImmediate(invalid); }
        }

        [Test]
        public void TryApply_DuringIdle_DoesNotResetAnimatorTime()
        {
            var animator = _character.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "CatInventoryIdle.controller");
            animator.Rebind();
            animator.Update(0);
            animator.Play("Idle", 0, 0.37f);
            animator.Update(0);
            var before = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(before.normalizedTime, Is.GreaterThan(0.3f));
            var control = _character.AddComponent<CatSkinController>();
            foreach (var skin in _library.Skins) { Assert.That(control.TryApply(skin), Is.True); }
            var after = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(after.fullPathHash, Is.EqualTo(before.fullPathHash));
            Assert.That(after.normalizedTime, Is.EqualTo(before.normalizedTime).Within(0.0001f));
        }

        [Test]
        public void Equip_SaveAndReloadJson_RestoresOwnershipAndAppearance()
        {
            var root = Path.Combine("Library", "InventoryTests", Guid.NewGuid().ToString("N"));
            var store = new JsonProfileStore(root);
            var profile = new ProfileService("test", store, _catalog, new SystemClock());
            var skin = _library.Skins[5];
            profile.Inventory.TryGrant(skin.ItemId);
            using (var session = new SkinInventorySession(profile, _catalog))
            {
                session.Select(skin.ItemId);
                session.EquipSelected();
                Assert.That(session.HasSaveError, Is.False);
            }
            var restored = new ProfileService("test", store, _catalog, new SystemClock());
            var control = _character.AddComponent<CatSkinController>();
            control.Initialize(_library, restored.Inventory);
            Assert.That(restored.Inventory.IsOwned(skin.ItemId), Is.True);
            Assert.That(control.CurrentSkinId, Is.EqualTo(skin.ItemId));
            // Test evidence remains under Library; never touches the user's persistent profile.
        }

        [Test]
        public void Initialize_DifferentInventory_UnsubscribesFromPreviousOwner()
        {
            var inventory = new InventoryService(_catalog);
            foreach (var skin in _library.Skins) { inventory.TryGrant(skin.ItemId); }
            var control = _character.AddComponent<CatSkinController>();
            control.Initialize(_library, inventory);
            var second = new InventoryService(_catalog);
            second.TryGrant(_library.Skins[1].ItemId);
            second.TryEquip(_library.Skins[1].ItemId, SkinInventorySession.SkinSlot);
            control.Initialize(_library, second);
            inventory.TryEquip(_library.Skins[3].ItemId, SkinInventorySession.SkinSlot);
            Assert.That(control.CurrentSkinId, Is.EqualTo(_library.Skins[1].ItemId));
        }

        [Test]
        public void View_BindSelectAndEquip_UpdatesCardsWithoutEquippingOnSelection()
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/PF_LocalSkinInventoryRoom.prefab"), _scene);
            var view = room.GetComponentInChildren<SkinInventoryView>();
            var profile = new ProfileService("ui-test", new JsonProfileStore(Path.Combine("Library", "InventoryTests", Guid.NewGuid().ToString("N"))), _catalog, new SystemClock());
            foreach (var skin in _library.Skins) { profile.Inventory.TryGrant(skin.ItemId); }
            profile.Inventory.TryEquip(_library.DefaultSkin.ItemId, SkinInventorySession.SkinSlot);
            using var session = new SkinInventorySession(profile, _catalog);
            try
            {
                view.Bind(session, id => _library.Find(id)?.Thumbnail);
                var root = view.GetComponent<UIDocument>().rootVisualElement;
                Assert.That(root.Query<Button>(className: "skin-card").ToList().Count, Is.EqualTo(19));
                Assert.That(root.Q<Button>("equip-skin").enabledSelf, Is.False);
                session.Select(_library.Skins[5].ItemId);
                Assert.That(profile.Inventory.IsEquipped(_library.DefaultSkin.ItemId), Is.True);
                Assert.That(root.Q<Button>("equip-skin").enabledSelf, Is.True);
                session.EquipSelected();
                Assert.That(root.Q<Button>("equip-skin").enabledSelf, Is.False);
                Assert.That(root.Q<Label>(className: "selected-title").text, Is.EqualTo("Calico"));
            }
            finally { view.Unbind(); }
        }
    }
}
