using System;
using System.IO;
using PawArena.Core.Inventory;
using PawArena.Gameplay.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PawArena.Editor.Inventory
{
    /// <summary>Captures actual Unity meshes/materials in an isolated preview scene, without touching user scenes.</summary>
    public static class CatGearPreviewCapture
    {
        private const string Request = "Library/CatGearPreview.request";
        [InitializeOnLoadMethod]
        private static void Schedule() => EditorApplication.delayCall += RunRequest;
        private static void RunRequest()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "render") { return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += RunRequest; return; }
            File.WriteAllText(Request, "running");
            try { Capture(); File.WriteAllText(Request, "complete"); }
            catch (Exception exception) { File.WriteAllText(Request, "failed: " + exception); Debug.LogException(exception); }
        }

        [MenuItem("Tools/Paw Arena/Inventory/Capture Gear Preview")]
        public static void Capture()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(768, 768, TextureFormat.RGB24, false);
            var previousTarget = RenderTexture.active;
            try
            {
                var cat = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/Cat/PF_Cat_Base.prefab"), scene);
                var library = AssetDatabase.LoadAssetAtPath<CatGearLibrary>(CatGearInventoryBuilder.Folder + "/CatGearLibrary.asset");
                var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/_Project/ScriptableObjects/Inventory/CatSkins/CatSkinCatalog.asset");
                var skins = AssetDatabase.LoadAssetAtPath<CatSkinLibrary>("Assets/_Project/ScriptableObjects/Inventory/CatSkins/CatSkinLibrary.asset");
                var inventory = new InventoryService(catalog);
                foreach (var item in catalog.Items) { inventory.TryGrant(item.ItemId); }
                cat.AddComponent<CatSkinController>().Initialize(skins, inventory);
                cat.AddComponent<CatGearController>().Initialize(library, inventory);
                var cameraObject = new GameObject("GearReviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.055f, .075f, .11f);
                camera.orthographic = true; camera.orthographicSize = 1.5f;
                camera.transform.position = new Vector3(3, 2.6f, 7);
                camera.transform.LookAt(new Vector3(0, 1.2f, 0));
                camera.targetTexture = target;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                for (var i = 0; i < 2; i++)
                {
                    var light = new GameObject("GearReviewLight", typeof(Light));
                    SceneManager.MoveGameObjectToScene(light, scene);
                    light.GetComponent<Light>().type = LightType.Directional;
                    light.GetComponent<Light>().intensity = i == 0 ? 1.15f : .65f;
                    light.transform.rotation = Quaternion.Euler(i == 0 ? 35 : 15, i == 0 ? 145 : -35, 0);
                }
                Directory.CreateDirectory("Blender/Previews/Characters/Gear/Unity");
                foreach (var item in library.Gear)
                {
                    for (var i = 0; i < CatGearSlots.Count; i++) { inventory.TryUnequip(new EquipmentSlotId(EquipmentSlotType.Gear, i)); }
                    inventory.TryEquip(item.ItemId, item.BuildDescriptor().AllowedSlots[0]);
                    Render(item.ItemId);
                }
                inventory.TryEquip("gear_hachimaki", CatGearSlots.Head);
                inventory.TryEquip("gear_crimson_mawashi", CatGearSlots.Waist);
                inventory.TryEquip("gear_gyoji_fan", CatGearSlots.Hand);
                Render("Combined_Unity");
                var animator = cat.GetComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/ScriptableObjects/Inventory/CatSkins/CatInventoryIdle.controller");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind(); animator.Update(0); animator.Play("Idle", 0, .37f); animator.Update(0);
                Render("Combined_Idle_Unity");
                inventory.TryUnequip(CatGearSlots.Hand);
                inventory.TryEquip("gear_topknot", CatGearSlots.Head);
                inventory.TryEquip("gear_kesho_mawashi", CatGearSlots.Waist);
                animator.Update(0);
                Render("Topknot_Kesho_Idle_Unity");

                void Render(string name)
                {
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); pixels.Apply();
                    File.WriteAllBytes("Blender/Previews/Characters/Gear/Unity/" + name + ".png", pixels.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previousTarget;
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Object.DestroyImmediate(pixels);
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
