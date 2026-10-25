using System;
using System.IO;
using System.Linq;
using PawArena.Bootstrap;
using PawArena.Core.Inventory;
using PawArena.Gameplay.Inventory;
using PawArena.UI.Inventory;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PawArena.Editor.Inventory
{
    /// <summary>Creates an isolated dressing-room scene and catalog without changing the approved base or open scenes.</summary>
    public static class LocalSkinInventoryBuilder
    {
        /// <summary>Generated scene, intentionally not added to the existing build scene list.</summary>
        public const string ScenePath = "Assets/_Project/Scenes/Prototype/LocalSkinInventory.unity";
        /// <summary>Data shared by the standalone demo and future gameplay integration.</summary>
        public const string DataFolder = "Assets/_Project/ScriptableObjects/Inventory/CatSkins";
        private const string RequestPath = "Library/LocalSkinInventoryPrefab.request";
        private const string BasePath = "Assets/_Project/Prefabs/Characters/Cat/PF_Cat_Base.prefab";
        private const string UiFolder = "Assets/_Project/UI/Inventory";
        private const string IconFolder = "Assets/_Project/Textures/UI/SkinInventory";
        private static readonly string[] Names = { "Ginger", "Grey", "Black", "Tuxedo", "White", "Calico", "Pumpkin", "Mummy", "Russian Blue", "Siamese", "Tiger", "Ninja", "Black Widow", "Crystal", "Gold", "Hologram", "Skeleton", "Snow Leopard", "Frankencat" };

        [InitializeOnLoadMethod]
        private static void Schedule() => EditorApplication.delayCall += RunRequest;
        private static void RunRequest()
        {
            if (!File.Exists(RequestPath) || File.ReadAllText(RequestPath).Trim() != "build") { return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunRequest;
                return;
            }
            File.WriteAllText(RequestPath, "running");
            try { Build(); File.WriteAllText(RequestPath, "complete"); }
            catch (Exception exception) { File.WriteAllText(RequestPath, "failed: " + exception); Debug.LogException(exception); }
        }

        /// <summary>Builds new assets once. Refuses to overwrite an existing scene or authored catalog.</summary>
        [MenuItem("Tools/Paw Arena/Inventory/Create Local Skin Room")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Exit Play Mode first."); }
            if (File.Exists(ScenePath))
            {
                throw new InvalidOperationException("Local inventory already exists; existing assets will not be overwritten.");
            }
            var basePrefab = Load<GameObject>(BasePath);
            var mesh = basePrefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            if (Directory.Exists(DataFolder))
            {
                // Resume scene generation after an interrupted build, without rewriting authored data.
                CreateRoom(basePrefab, Load<ItemCatalog>(DataFolder + "/CatSkinCatalog.asset"),
                    Load<CatSkinLibrary>(DataFolder + "/CatSkinLibrary.asset"),
                    Load<PanelSettings>(DataFolder + "/InventoryPanelSettings.asset"),
                    Load<AnimatorController>(DataFolder + "/CatInventoryIdle.controller"));
                return;
            }
            EnsureFolder(DataFolder);
            EnsureFolder(IconFolder);
            var definitions = new CatSkinDefinition[Names.Length];
            for (var i = 0; i < Names.Length; i++)
            {
                var slug = Names[i].Replace(' ', '_');
                var iconPath = IconFolder + "/" + slug + ".png";
                var source = $"Blender/Previews/Characters/Skins/{i + 1:00}_{slug}_Front.png";
                if (!File.Exists(source)) { throw new FileNotFoundException("Skin preview missing", source); }
                File.Copy(source, iconPath, false);
                AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
                Undo.RecordObject(importer, "Configure skin thumbnail");
                importer.maxTextureSize = 256;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                var definition = ScriptableObject.CreateInstance<CatSkinDefinition>();
                definition.name = "Skin_" + slug;
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("_itemId").stringValue = "fur_" + slug.ToLowerInvariant();
                serialized.FindProperty("_displayName").stringValue = Names[i];
                serialized.FindProperty("_description").stringValue = "Material-only cat skin. Shared mesh, UV, rig and animations.";
                serialized.FindProperty("_category").intValue = (int)ItemCategory.Fur;
                serialized.FindProperty("_rarity").intValue = (int)(i < 5 ? ItemRarity.Common : i < 11 ? ItemRarity.Rare : ItemRarity.Epic);
                serialized.FindProperty("_unlockType").intValue = (int)(i == 0 ? UnlockType.Default : UnlockType.Grant);
                serialized.FindProperty("_price").longValue = 0;
                serialized.FindProperty("_compatibleMesh").objectReferenceValue = mesh;
                serialized.FindProperty("_coat").objectReferenceValue = Load<Material>($"Assets/_Project/Materials/Characters/Cat/Skins/M_Cat_{slug}_Coat.mat");
                serialized.FindProperty("_eyes").objectReferenceValue = Load<Material>($"Assets/_Project/Materials/Characters/Cat/Skins/M_Cat_{slug}_Eyes.mat");
                serialized.FindProperty("_thumbnail").objectReferenceValue = Load<Texture2D>(iconPath);
                var slots = serialized.FindProperty("_allowedSlots");
                slots.arraySize = 1;
                slots.GetArrayElementAtIndex(0).FindPropertyRelative("_type").intValue = (int)EquipmentSlotType.Fur;
                slots.GetArrayElementAtIndex(0).FindPropertyRelative("_index").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                CreateAsset(definition, DataFolder + "/Skin_" + slug + ".asset");
                definitions[i] = definition;
            }
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            SetArray(catalog, "_definitions", definitions);
            CreateAsset(catalog, DataFolder + "/CatSkinCatalog.asset");
            var library = ScriptableObject.CreateInstance<CatSkinLibrary>();
            SetArray(library, "_skins", definitions);
            SetObject(library, "_defaultSkin", definitions[0]);
            CreateAsset(library, DataFolder + "/CatSkinLibrary.asset");
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 800);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.themeStyleSheet = Load<ThemeStyleSheet>(UiFolder + "/SkinInventoryTheme.tss");
            CreateAsset(panel, DataFolder + "/InventoryPanelSettings.asset");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(DataFolder + "/CatInventoryIdle.controller");
            Undo.RegisterCreatedObjectUndo(controller, "Create dressing room Animator");
            var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Animations/Characters/Cat/Base/AN_Cat_Idle.fbx")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
            var state = controller.layers[0].stateMachine.AddState("Idle");
            state.motion = idle;
            controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);

            CreateRoom(basePrefab, catalog, library, panel, controller);
        }

        private static void CreateRoom(GameObject basePrefab, ItemCatalog catalog, CatSkinLibrary library,
            PanelSettings panel, AnimatorController controller)
        {
            if (controller.layers.Length == 0) { controller.AddLayer("Base Layer"); }
            if (controller.layers[0].stateMachine.states.Length == 0)
            {
                var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Animations/Characters/Cat/Base/AN_Cat_Idle.fbx")
                    .OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
                var idleState = controller.layers[0].stateMachine.AddState("Idle");
                idleState.motion = idle;
                controller.layers[0].stateMachine.defaultState = idleState;
                EditorUtility.SetDirty(controller);
            }
            AssetDatabase.SaveAssets();
            var existingScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .Select(s => s.handle + ":" + s.isDirty + ":" + s.rootCount).ToArray();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var room = new GameObject("LocalSkinInventoryRoom");
                SceneManager.MoveGameObjectToScene(room, scene);
                var character = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, scene);
                character.transform.SetParent(room.transform, false);
                character.name = "LocalCat";
                character.transform.rotation = Quaternion.Euler(0, -12, 0);
                var animator = character.GetComponent<Animator>();
                Undo.RecordObject(animator, "Assign preview Idle");
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                var renderer = character.GetComponentInChildren<SkinnedMeshRenderer>();
                var skinController = Undo.AddComponent<CatSkinController>(character);
                SetObject(skinController, "_bodyRenderer", renderer);
                var cameraObject = new GameObject("InventoryCamera", typeof(Camera), typeof(AudioListener));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.transform.SetParent(room.transform, false);
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create inventory camera");
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.12f, 0.18f);
                camera.rect = new Rect(0.605f, 0.085f, 0.38f, 0.48f);
                camera.orthographic = true;
                var bounds = renderer.bounds;
                camera.orthographicSize = Mathf.Max(bounds.extents.y * 1.3f, 0.8f);
                camera.transform.position = bounds.center + new Vector3(0, 0.05f, -5);
                camera.transform.LookAt(bounds.center);
                var lightObject = new GameObject("InventoryKeyLight", typeof(Light));
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                lightObject.transform.SetParent(room.transform, false);
                Undo.RegisterCreatedObjectUndo(lightObject, "Create inventory light");
                var light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2.2f;
                light.transform.rotation = Quaternion.Euler(35, -35, 0);
                var fillObject = new GameObject("InventoryFillLight", typeof(Light));
                SceneManager.MoveGameObjectToScene(fillObject, scene);
                fillObject.transform.SetParent(room.transform, false);
                var fill = fillObject.GetComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 1.2f;
                fill.transform.rotation = Quaternion.Euler(15, 145, 0);
                var ui = new GameObject("SkinInventoryUI", typeof(UIDocument), typeof(SkinInventoryView));
                SceneManager.MoveGameObjectToScene(ui, scene);
                ui.transform.SetParent(room.transform, false);
                Undo.RegisterCreatedObjectUndo(ui, "Create skin inventory UI");
                ui.GetComponent<UIDocument>().panelSettings = panel;
                var view = ui.GetComponent<SkinInventoryView>();
                SetObject(view, "_styleSheet", Load<StyleSheet>(UiFolder + "/SkinInventory.uss"));
                var systems = new GameObject("LocalSkinInventory", typeof(LocalSkinInventoryBootstrap));
                SceneManager.MoveGameObjectToScene(systems, scene);
                systems.transform.SetParent(room.transform, false);
                Undo.RegisterCreatedObjectUndo(systems, "Create local skin composition");
                var bootstrap = systems.GetComponent<LocalSkinInventoryBootstrap>();
                SetObject(bootstrap, "_catalog", catalog);
                SetObject(bootstrap, "_skins", library);
                SetObject(bootstrap, "_character", skinController);
                SetObject(bootstrap, "_view", view);
                var bootstrapData = new SerializedObject(bootstrap);
                bootstrapData.FindProperty("_grantAllSkinsForTesting").boolValue = true;
                bootstrapData.ApplyModifiedProperties();
                const string roomPath = "Assets/_Project/Prefabs/UI/PF_LocalSkinInventoryRoom.prefab";
                if (File.Exists(roomPath)) { throw new IOException("The room prefab already exists; not overwriting it."); }
                EnsureFolder("Assets/_Project/Prefabs/UI");
                var prefab = PrefabUtility.SaveAsPrefabAsset(room, roomPath);
                if (prefab == null) { throw new IOException("Could not save the inventory room prefab."); }
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab.transform, out string guid, out long transformId);
                // A scene containing one prefab instance can be authored without switching or saving the user's untitled scene.
                var sceneText = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" +
                    "--- !u!1001 &1000\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {fileID: 0}\n    m_Modifications: []\n    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n" +
                    $"  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}\n" +
                    "--- !u!4 &1001 stripped\nTransform:\n" +
                    $"  m_CorrespondingSourceObject: {{fileID: {transformId}, guid: {guid}, type: 3}}\n" +
                    "  m_PrefabInstance: {fileID: 1000}\n  m_PrefabAsset: {fileID: 0}\n" +
                    "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n  - {fileID: 1001}\n";
                File.WriteAllText(ScenePath, sceneText);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.SaveAssets();
            var verificationScene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var roots = verificationScene.GetRootGameObjects();
                if (roots.Length != 1 || roots[0].GetComponentInChildren<LocalSkinInventoryBootstrap>() == null)
                {
                    throw new InvalidOperationException("Room scene did not deserialize correctly.");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(verificationScene); }
            var after = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .Select(s => s.handle + ":" + s.isDirty + ":" + s.rootCount).ToArray();
            if (!existingScenes.SequenceEqual(after)) { throw new InvalidOperationException("Open scene state changed unexpectedly."); }
            Selection.activeObject = Load<SceneAsset>(ScenePath);
            Debug.Log("[Local Skin Inventory] Created 19 definitions, catalog, library and isolated demo scene. Existing scenes unchanged.");
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null ? asset : throw new FileNotFoundException(typeof(T).Name + " missing", path);
        }
        private static void CreateAsset(UnityEngine.Object asset, string path)
        {
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create local skin inventory asset");
        }
        private static void SetObject(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedProperties();
        }
        private static void SetArray(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var data = new SerializedObject(target);
            var array = data.FindProperty(field);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) { array.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
            data.ApplyModifiedProperties();
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
