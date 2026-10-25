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
    public sealed class CatGearIntegrationTests
    {
        private Scene _scene;
        private GameObject _cat;
        private CatGearLibrary _gear;
        private CatSkinLibrary _skins;
        private ItemCatalog _catalog;
        private InventoryService _inventory;
        private CatGearController _controller;
        private const string Skins = "Assets/_Project/ScriptableObjects/Inventory/CatSkins/";

        [SetUp]
        public void SetUp()
        {
            _gear = AssetDatabase.LoadAssetAtPath<CatGearLibrary>("Assets/_Project/ScriptableObjects/Inventory/CatGear/CatGearLibrary.asset");
            Assert.That(_gear, Is.Not.Null, "Import sumo gear first.");
            _skins = AssetDatabase.LoadAssetAtPath<CatSkinLibrary>(Skins + "CatSkinLibrary.asset");
            _catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(Skins + "CatSkinCatalog.asset");
            _scene = EditorSceneManager.NewPreviewScene();
            _cat = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/Cat/PF_Cat_Base.prefab"), _scene);
            _inventory = new InventoryService(_catalog);
            foreach (var item in _catalog.Items) { _inventory.TryGrant(item.ItemId); }
            _controller = _cat.AddComponent<CatGearController>();
            _controller.Initialize(_gear, _inventory);
        }
        [TearDown]
        public void TearDown() { if (_scene.IsValid()) { EditorSceneManager.ClosePreviewScene(_scene); } }
        private void Equip(string id)
        {
            var item = _gear.Find("gear_" + id);
            Assert.That(item, Is.Not.Null);
            Assert.That(_inventory.TryEquip(item.ItemId, item.BuildDescriptor().AllowedSlots[0]), Is.EqualTo(InventoryOperationResult.Success));
        }

        [Test]
        public void ImportedGear_AllEight_BindPoseMatchesApprovedCharacter()
        {
            Assert.That(_gear.Gear.Count, Is.EqualTo(8));
            foreach (var item in _gear.Gear)
            {
                Assert.That(item.IsValid, Is.True, item.ItemId);
                var slot = item.BuildDescriptor().AllowedSlots[0];
                _inventory.TryEquip(item.ItemId, slot);
                Assert.That(_controller.GetDisplayedItem(slot.Index), Is.EqualTo(item.ItemId));
                var renderer = _cat.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.gameObject.name == "EquippedGear_" + item.ItemId);
                for (var i = 0; i < renderer.bones.Length; i++)
                {
                    var matrix = renderer.transform.worldToLocalMatrix * renderer.bones[i].localToWorldMatrix * renderer.sharedMesh.bindposes[i];
                    for (var j = 0; j < 16; j++) { Assert.That(matrix[j], Is.EqualTo(Matrix4x4.identity[j]).Within(.002f), item.ItemId + " / " + renderer.bones[i].name + " / " + j); }
                }
                Assert.That(renderer.sharedMesh.subMeshCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void EquipReplaceRemove_OnlyChangesOneRenderer()
        {
            Equip("hachimaki"); Equip("black_mawashi"); Equip("gyoji_fan");
            var before = _cat.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.That(before.Length, Is.EqualTo(4));
            var head = before.Single(r => r.name.Contains("hachimaki"));
            var hand = before.Single(r => r.name.Contains("gyoji"));
            Equip("crimson_mawashi");
            Assert.That(_cat.GetComponentsInChildren<SkinnedMeshRenderer>(), Has.Member(head).And.Member(hand));
            Assert.That(_cat.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.EqualTo(4));
            _inventory.TryUnequip(CatGearSlots.Waist);
            Assert.That(_cat.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.EqualTo(3));
            Assert.That(_cat.GetComponentsInChildren<Animator>().Length, Is.EqualTo(1));
            Assert.That(_cat.GetComponentsInChildren<Collider>(), Is.Empty);
        }

        [Test]
        public void ChangeNineteenSkins_PreservesGearAndAnimatorState()
        {
            var animator = _cat.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Skins + "CatInventoryIdle.controller");
            animator.Rebind(); animator.Update(0); animator.Play("Idle", 0, .37f); animator.Update(0);
            var before = animator.GetCurrentAnimatorStateInfo(0);
            Equip("topknot"); Equip("kesho_mawashi"); Equip("gyoji_fan");
            var renderers = _cat.GetComponentsInChildren<SkinnedMeshRenderer>();
            var skin = _cat.AddComponent<CatSkinController>();
            foreach (var definition in _skins.Skins) { Assert.That(skin.TryApply(definition), Is.True); }
            Assert.That(_cat.GetComponentsInChildren<SkinnedMeshRenderer>(), Is.EqualTo(renderers));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(before.normalizedTime).Within(.0001f));
        }

        [Test]
        public void ReinitializeAndDisable_DetachesOldInventoryAndRefreshesOnEnable()
        {
            Equip("hachimaki");
            var second = new InventoryService(_catalog);
            second.TryGrant("gear_topknot"); second.TryEquip("gear_topknot", CatGearSlots.Head);
            _controller.Initialize(_gear, second);
            _inventory.TryUnequip(CatGearSlots.Head);
            Assert.That(_controller.GetDisplayedItem(0), Is.EqualTo("gear_topknot"));
            _controller.enabled = false;
            second.TryUnequip(CatGearSlots.Head);
            _controller.enabled = true;
            Assert.That(_controller.GetDisplayedItem(0), Is.Null);
        }

        [Test]
        public void JsonSaveRestore_RecoversSkinAndAllGearSlots()
        {
            var store = new JsonProfileStore(Path.Combine("Library", "InventoryTests", Guid.NewGuid().ToString("N")));
            var profile = new ProfileService("gear-test", store, _catalog, new SystemClock());
            foreach (var item in _catalog.Items) { profile.Inventory.TryGrant(item.ItemId); }
            using var session = new SkinInventorySession(profile, _catalog, true);
            foreach (var id in new[] { "fur_calico", "gear_topknot", "gear_yokozuna_rope", "gear_gyoji_fan" }) { session.Select(id); session.EquipSelected(); }
            var restored = new ProfileService("gear-test", store, _catalog, new SystemClock());
            _controller.Initialize(_gear, restored.Inventory);
            Assert.That(_controller.GetDisplayedItem(0), Is.EqualTo("gear_topknot"));
            Assert.That(_controller.GetDisplayedItem(1), Is.EqualTo("gear_yokozuna_rope"));
            Assert.That(_controller.GetDisplayedItem(2), Is.EqualTo("gear_gyoji_fan"));
            Assert.That(restored.Inventory.IsEquipped("fur_calico"), Is.True);
        }

        [Test]
        public void SavedScene_Bootstrap_RehydratesEquippedGearOnActualCharacter()
        {
            var preview = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Prototype/LocalSkinInventory.unity");
            try
            {
                var room = preview.GetRootGameObjects().Single();
                var bootstrap = room.GetComponentsInChildren<MonoBehaviour>().Single(c => c.GetType().Name == "LocalSkinInventoryBootstrap");
                var store = new JsonProfileStore(Path.Combine("Library", "InventoryTests", Guid.NewGuid().ToString("N")));
                var profile = new ProfileService("gear-bootstrap-test", store, _catalog, new SystemClock());
                var bind = bootstrap.GetType().GetMethod("BindProfile");
                bind.Invoke(bootstrap, new object[] { profile });
                var session = (SkinInventorySession)bootstrap.GetType().GetProperty("Session").GetValue(bootstrap);
                Assert.That(session.Items.Count, Is.EqualTo(27));
                foreach (var id in new[] { "fur_calico", "gear_hachimaki", "gear_crimson_mawashi", "gear_gyoji_fan" })
                {
                    Assert.That(session.Select(id), Is.True);
                    Assert.That(session.EquipSelected(), Is.EqualTo(InventoryOperationResult.Success));
                }
                var restored = new ProfileService("gear-bootstrap-test", store, _catalog, new SystemClock());
                bind.Invoke(bootstrap, new object[] { restored });
                var gearControl = room.GetComponentInChildren<CatGearController>();
                Assert.That(gearControl.GetDisplayedItem(0), Is.EqualTo("gear_hachimaki"));
                Assert.That(gearControl.GetDisplayedItem(1), Is.EqualTo("gear_crimson_mawashi"));
                Assert.That(gearControl.GetDisplayedItem(2), Is.EqualTo("gear_gyoji_fan"));
                Assert.That(room.GetComponentInChildren<CatSkinController>().CurrentSkinId, Is.EqualTo("fur_calico"));
                Assert.That(room.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.EqualTo(4));
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [Test]
        public void Room_PreviewCamera_ShowsFrontAndContainsSavedGearWiring()
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/PF_LocalSkinInventoryRoom.prefab"), _scene);
            var camera = room.GetComponentInChildren<Camera>();
            var cat = room.GetComponentInChildren<CatGearController>();
            Assert.That(cat, Is.Not.Null);
            var towardsCamera = (camera.transform.position - (cat.transform.position + Vector3.up)).normalized;
            Assert.That(Vector3.Dot(cat.transform.forward, towardsCamera), Is.GreaterThan(.5f), "Inventory must show the face, not the tail.");
            var bootstrap = room.GetComponentsInChildren<MonoBehaviour>().Single(c => c.GetType().Name == "LocalSkinInventoryBootstrap");
            var data = new SerializedObject(bootstrap);
            Assert.That(data.FindProperty("_gear").objectReferenceValue, Is.SameAs(_gear));
            Assert.That(data.FindProperty("_gearController").objectReferenceValue, Is.SameAs(cat));
            Assert.That(data.FindProperty("_catalog").objectReferenceValue, Is.SameAs(_catalog));
            Assert.That(data.FindProperty("_grantAllGearForTesting").boolValue, Is.True);
        }

        [Test]
        public void View_GearCategoriesAndUnequip_UpdatesSelectionAndAvailability()
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/PF_LocalSkinInventoryRoom.prefab"), _scene);
            var view = room.GetComponentInChildren<SkinInventoryView>();
            var profile = new ProfileService("gear-ui-test", new JsonProfileStore(Path.Combine("Library", "InventoryTests", Guid.NewGuid().ToString("N"))), _catalog, new SystemClock());
            foreach (var item in _catalog.Items) { profile.Inventory.TryGrant(item.ItemId); }
            using var session = new SkinInventorySession(profile, _catalog, true);
            try
            {
                view.Bind(session, id => _gear.Find(id)?.Thumbnail ?? _skins.Find(id)?.Thumbnail);
                var root = view.GetComponent<UIDocument>().rootVisualElement;
                Assert.That(root.Query<Button>(className: "skin-card").ToList().Count, Is.EqualTo(27));
                var dropdown = root.Q<DropdownField>("item-category"); dropdown.value = "Eo";
                Assert.That(root.Query<Button>(className: "skin-card").ToList().Count(b => b.style.display.value != DisplayStyle.None), Is.EqualTo(5));
                session.Select("gear_black_mawashi");
                Assert.That(root.Q<Button>("unequip-gear").enabledSelf, Is.False);
                session.EquipSelected();
                Assert.That(root.Q<Button>("unequip-gear").enabledSelf, Is.True);
                session.UnequipSelected();
                Assert.That(root.Q<Button>("unequip-gear").enabledSelf, Is.False);
            }
            finally { view.Unbind(); }
        }
    }
}
