using System;
using System.IO;
using System.Linq;
using PawArena.Bootstrap;
using PawArena.Core.Inventory;
using PawArena.Gameplay.Inventory;
using UnityEditor;
using UnityEngine;

namespace PawArena.Editor.Inventory
{
    /// <summary>Imports the authored Blender gear and extends the existing local room, without changing open scenes.</summary>
    public static class CatGearInventoryBuilder
    {
        public const string Folder = "Assets/_Project/ScriptableObjects/Inventory/CatGear";
        private const string Models = "Assets/_Project/Models/Characters/Cat/Gear";
        private const string Materials = "Assets/_Project/Materials/Characters/Cat/Gear";
        private const string Textures = "Assets/_Project/Textures/Characters/Cat/Gear";
        private const string RoomPath = "Assets/_Project/Prefabs/UI/PF_LocalSkinInventoryRoom.prefab";
        private const string CatalogPath = "Assets/_Project/ScriptableObjects/Inventory/CatSkins/CatSkinCatalog.asset";
        private const string Request = "Library/CatGearBuild.request";
        [Serializable] private sealed class Manifest { public Entry[] gear; }
        [Serializable] private sealed class Entry { public string name; public int slot; public int rarity; public Palette[] materials; }
        [Serializable] private sealed class Palette { public string name; public float[] color; public float metallic; public float roughness; }

        [InitializeOnLoadMethod]
        private static void Schedule() => EditorApplication.delayCall += RunRequest;
        private static void RunRequest()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "build") { return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += RunRequest; return; }
            File.WriteAllText(Request, "running");
            try { Build(); File.WriteAllText(Request, "complete"); }
            catch (Exception exception) { File.WriteAllText(Request, "failed: " + exception); Debug.LogException(exception); }
        }

        /// <summary>Creates gear assets once and backs up the catalog/room before adding gear references.</summary>
        [MenuItem("Tools/Paw Arena/Inventory/Import Sumo Gear")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Exit Play Mode first."); }
            if (File.Exists(Folder + "/CatGearLibrary.asset")) { throw new IOException("Gear already exists. Use authored assets; this builder will not overwrite them."); }
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Blender/Characters/Exports/Gear_Manifest.json"));
            foreach (var folder in new[] { Folder, Materials, Textures }) { Directory.CreateDirectory(folder); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var definitions = new CatGearDefinition[manifest.gear.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                var entry = manifest.gear[i];
                var fbxPath = Models + "/SK_Gear_" + entry.name + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
                if (importer == null) { throw new FileNotFoundException("Gear FBX not imported", fbxPath); }
                Undo.RecordObject(importer, "Configure shared-rig cosmetic mesh");
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.isReadable = true;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                var source = model.GetComponentInChildren<SkinnedMeshRenderer>();
                if (source == null || source.bones.Any(b => b == null)) { throw new InvalidOperationException("Invalid gear skinning: " + entry.name); }
                var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
                mesh.name = "MSH_Gear_" + entry.name;
                var triangles = mesh.triangles;
                mesh.subMeshCount = 1;
                mesh.SetTriangles(triangles, 0);
                Create(mesh, Models + "/" + mesh.name + ".asset");
                var material = CreateMaterial(entry);
                var iconPath = Textures + "/Icon_" + entry.name + ".png";
                File.Copy("Blender/Previews/Characters/Gear/" + entry.name + ".png", iconPath, false);
                AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);
                var iconImporter = (TextureImporter)AssetImporter.GetAtPath(iconPath);
                iconImporter.maxTextureSize = 256;
                iconImporter.mipmapEnabled = false;
                iconImporter.SaveAndReimport();
                var definition = ScriptableObject.CreateInstance<CatGearDefinition>();
                definition.name = "Gear_" + entry.name;
                var data = new SerializedObject(definition);
                data.FindProperty("_itemId").stringValue = "gear_" + entry.name.ToLowerInvariant();
                data.FindProperty("_displayName").stringValue = entry.name.Replace('_', ' ');
                data.FindProperty("_description").stringValue = "Cosmetic sumo gear. Shared cat rig; no combat stats or cloth simulation.";
                data.FindProperty("_category").intValue = (int)ItemCategory.Gear;
                data.FindProperty("_rarity").intValue = entry.rarity;
                data.FindProperty("_unlockType").intValue = (int)UnlockType.Grant;
                var slots = data.FindProperty("_allowedSlots"); slots.arraySize = 1;
                slots.GetArrayElementAtIndex(0).FindPropertyRelative("_type").intValue = (int)EquipmentSlotType.Gear;
                slots.GetArrayElementAtIndex(0).FindPropertyRelative("_index").intValue = entry.slot;
                data.FindProperty("_mesh").objectReferenceValue = mesh;
                data.FindProperty("_material").objectReferenceValue = material;
                data.FindProperty("_thumbnail").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                var names = data.FindProperty("_boneNames"); names.arraySize = source.bones.Length;
                for (var b = 0; b < source.bones.Length; b++) { names.GetArrayElementAtIndex(b).stringValue = source.bones[b].name; }
                data.FindProperty("_rootBoneName").stringValue = source.rootBone.name;
                var matrix = model.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                data.FindProperty("_localPosition").vector3Value = matrix.GetColumn(3);
                data.FindProperty("_localRotation").quaternionValue = matrix.rotation;
                data.FindProperty("_localScale").vector3Value = matrix.lossyScale;
                data.ApplyModifiedPropertiesWithoutUndo();
                Create(definition, Folder + "/" + definition.name + ".asset");
                if (!definition.IsValid) { throw new InvalidOperationException("Generated definition invalid: " + entry.name); }
                definitions[i] = definition;
            }
            var library = ScriptableObject.CreateInstance<CatGearLibrary>();
            var libraryData = new SerializedObject(library);
            var gears = libraryData.FindProperty("_gear"); gears.arraySize = definitions.Length;
            for (var i = 0; i < definitions.Length; i++) { gears.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i]; }
            libraryData.ApplyModifiedPropertiesWithoutUndo();
            Create(library, Folder + "/CatGearLibrary.asset");

            var backup = "Blender/Backups/BeforeGear_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup);
            File.Copy(CatalogPath, backup + "/CatSkinCatalog.asset");
            File.Copy(RoomPath, backup + "/PF_LocalSkinInventoryRoom.prefab");
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            Undo.RecordObject(catalog, "Add eight cosmetic gears to local inventory");
            var catalogData = new SerializedObject(catalog);
            var items = catalogData.FindProperty("_definitions");
            var previousCount = items.arraySize;
            items.arraySize += definitions.Length;
            for (var i = 0; i < definitions.Length; i++) { items.GetArrayElementAtIndex(previousCount + i).objectReferenceValue = definitions[i]; }
            catalogData.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
            var room = PrefabUtility.LoadPrefabContents(RoomPath);
            try
            {
                var skin = room.GetComponentInChildren<CatSkinController>();
                var control = Undo.AddComponent<CatGearController>(skin.gameObject);
                var bootstrap = room.GetComponentInChildren<LocalSkinInventoryBootstrap>();
                var setup = new SerializedObject(bootstrap);
                setup.FindProperty("_gear").objectReferenceValue = library;
                setup.FindProperty("_gearController").objectReferenceValue = control;
                setup.FindProperty("_grantAllGearForTesting").boolValue = true;
                setup.ApplyModifiedProperties();
                var camera = room.GetComponentInChildren<Camera>();
                Undo.RecordObject(camera, "Fit dressed cat preview");
                camera.orthographicSize = 1.55f;
                camera.rect = new Rect(.605f, .16f, .38f, .41f);
                Undo.RecordObject(camera.transform, "Show cat face in inventory preview");
                camera.transform.position = new Vector3(.8f, 1.4f, 5);
                camera.transform.LookAt(new Vector3(0, 1.2f, 0));
                foreach (var light in room.GetComponentsInChildren<Light>())
                {
                    Undo.RecordObject(light, "Balance gear room lighting");
                    Undo.RecordObject(light.transform, "Light cat face");
                    var key = light.name.Contains("Key");
                    light.intensity = key ? 1.15f : .65f;
                    light.transform.rotation = Quaternion.Euler(key ? 35 : 15, key ? 145 : -35, 0);
                }
                PrefabUtility.SaveAsPrefabAsset(room, RoomPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Cat Gear] Eight authored meshes imported. Local inventory now has 19 skins + 8 gear; head, waist, hand slots. User scenes unchanged.");
        }

        private static Material CreateMaterial(Entry entry)
        {
            var basePath = Textures + "/T_Gear_" + entry.name;
            var color = new Texture2D(entry.materials.Length * 16, 16, TextureFormat.RGBA32, false);
            var mask = new Texture2D(color.width, color.height, TextureFormat.RGBA32, false, true);
            for (var y = 0; y < color.height; y++)
            {
                for (var x = 0; x < color.width; x++)
                {
                    var swatch = entry.materials[x / 16];
                    color.SetPixel(x, y, new Color(swatch.color[0], swatch.color[1], swatch.color[2], 1));
                    mask.SetPixel(x, y, new Color(swatch.metallic, 0, 0, 1 - swatch.roughness));
                }
            }
            color.Apply(); mask.Apply();
            File.WriteAllBytes(basePath + "_BaseColor.png", color.EncodeToPNG());
            File.WriteAllBytes(basePath + "_MetallicSmoothness.png", mask.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(color); UnityEngine.Object.DestroyImmediate(mask);
            foreach (var suffix in new[] { "_BaseColor", "_MetallicSmoothness" })
            {
                var path = basePath + suffix + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.sRGBTexture = suffix == "_BaseColor";
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Gear_" + entry.name };
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "_BaseColor.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "_MetallicSmoothness.png"));
            material.SetFloat("_Smoothness", 1);
            material.SetFloat("_Metallic", 1);
            material.SetFloat("_Cull", 0); // Tiny fabric ribbons are double-sided.
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            Create(material, Materials + "/" + material.name + ".mat");
            return material;
        }

        private static void Create(UnityEngine.Object asset, string path)
        {
            if (File.Exists(path)) { throw new IOException("Refusing to overwrite existing asset: " + path); }
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create cat gear asset");
        }
    }
}
