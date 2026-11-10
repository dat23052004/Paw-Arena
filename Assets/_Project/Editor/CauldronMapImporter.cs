using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PawArena.Gameplay.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PawArena.Editor
{
    /// <summary>Imports the authored Cauldron as nested tile prefabs and a map-only scene.</summary>
    public static class CauldronMapImporter
    {
        private const string SourcePath = "Blender/Map/Cauldron/Exports/Cauldron_Map.fbx";
        private const string ModelFolder = "Assets/_Project/Models/Maps/Cauldron";
        private const string ModelPath = ModelFolder + "/Cauldron_Map.fbx";
        private const string MaterialFolder = "Assets/_Project/Materials/Maps/Cauldron";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Maps/Cauldron";
        private const string TilePath = PrefabFolder + "/Tiles/PF_CauldronTile.prefab";
        private const string MapPath = PrefabFolder + "/PF_Map_Cauldron.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Maps/Cauldron_ScalePreview.unity";
        private const int ExpectedTileCount = 676;

        /// <summary>Creates only Cauldron-owned assets; existing preview scenes are never overwritten.</summary>
        [MenuItem("Tools/Paw Arena/Maps/Cauldron/Import Map and Create Map Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before importing Cauldron.");
            }
            if (File.Exists(ScenePath))
            {
                throw new IOException("Cauldron preview already exists. Open it instead of overwriting it.");
            }
            if (!File.Exists(SourcePath)) { throw new FileNotFoundException("Missing authored Cauldron FBX.", SourcePath); }

            EnsureFolder(ModelFolder + "/Meshes");
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder + "/Tiles");
            EnsureFolder("Assets/_Project/Scenes/Maps");
            File.Copy(SourcePath, ModelPath, true);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter();

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelPrefab == null)
            {
                throw new InvalidOperationException("Missing imported Cauldron model.");
            }

            var originalScene = SceneManager.GetActiveScene();
            // Batch Unity starts with an untitled scene, which cannot host an additive scene.
            // Never replace a user's open scene when this command is run interactively.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameObject imported = null;
            try
            {
                imported = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, scene);
                var filters = imported.GetComponentsInChildren<MeshFilter>(true);
                var tileSources = filters.Where(filter => filter.name.StartsWith("Tile_", StringComparison.Ordinal)).ToArray();
                if (tileSources.Length != ExpectedTileCount)
                {
                    throw new InvalidOperationException($"Expected {ExpectedTileCount} tiles, found {tileSources.Length}.");
                }

                var materials = CreateMaterials(filters);
                var sharedTileMesh = BakeWorldAxes(tileSources[0], ModelFolder + "/Meshes/SM_CauldronTile.asset");
                if (Mathf.Abs(sharedTileMesh.bounds.max.y) > .001f ||
                    Mathf.Abs(sharedTileMesh.bounds.size.y - .24f) > .001f)
                {
                    throw new InvalidOperationException("Tile import has an unexpected unit scale or up axis.");
                }

                var baseTile = CreateBaseTile(scene, tileSources[0], sharedTileMesh, materials);
                var variants = CreateTileVariants(scene, tileSources, baseTile, materials);
                var map = Root("PF_Map_Cauldron", scene);
                var tilesRoot = Child(map.transform, "Tiles");
                var layerRoots = new Dictionary<string, Transform>(StringComparer.Ordinal);
                foreach (var source in tileSources)
                {
                    var sourceLayer = source.transform.parent;
                    if (!layerRoots.TryGetValue(sourceLayer.name, out var layer))
                    {
                        layer = Child(tilesRoot, sourceLayer.name);
                        layer.position = sourceLayer.position;
                        layerRoots.Add(sourceLayer.name, layer);
                    }
                    var materialKey = source.GetComponent<MeshRenderer>().sharedMaterials[0].name;
                    var tile = (GameObject)PrefabUtility.InstantiatePrefab(variants[materialKey], scene);
                    Undo.RegisterCreatedObjectUndo(tile, "Add Cauldron tile instance");
                    tile.transform.SetParent(layer, false);
                    tile.transform.position = source.transform.position;
                    tile.name = source.name;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tile);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tile.transform);
                }

                var vessel = Child(map.transform, "Cauldron");
                foreach (var source in filters.Except(tileSources))
                {
                    var part = Child(vessel, source.name).gameObject;
                    part.transform.position = source.transform.position;
                    var mesh = BakeWorldAxes(source, ModelFolder + "/Meshes/SM_" + source.name + ".asset");
                    Undo.AddComponent<MeshFilter>(part).sharedMesh = mesh;
                    Undo.AddComponent<MeshRenderer>(part).sharedMaterials = RemapMaterials(source, materials);
                    SetGroundLayer(part);
                    if (source.name.StartsWith("Cauldron_", StringComparison.Ordinal))
                    {
                        var collider = Undo.AddComponent<MeshCollider>(part);
                        collider.sharedMesh = mesh;
                        // Preserve the open vessel: a convex collider would fill its mouth.
                        collider.convex = false;
                    }
                }

                var spawnRoot = Child(map.transform, "SpawnPoints");
                var points = imported.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name.StartsWith("Spawn_", StringComparison.Ordinal))
                    .OrderBy(item => item.name)
                    .Select(source =>
                    {
                        var marker = Child(spawnRoot, source.name);
                        marker.position = source.position;
                        return marker;
                    }).ToArray();
                var spawnData = new SerializedObject(Undo.AddComponent<MapSpawnPoints>(map));
                var array = spawnData.FindProperty("_points");
                array.arraySize = points.Length;
                for (var i = 0; i < points.Length; i++) { array.GetArrayElementAtIndex(i).objectReferenceValue = points[i]; }
                spawnData.ApplyModifiedProperties();

                var mapPrefab = PrefabUtility.SaveAsPrefabAsset(map, MapPath);
                if (mapPrefab == null) { throw new IOException("Could not save Cauldron map prefab."); }
                Undo.DestroyObjectImmediate(map);
                Undo.DestroyObjectImmediate(imported);
                imported = null;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Place Cauldron preview");
                instance.name = "Cauldron Map - Scale This Root";
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);

                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) { throw new IOException("Could not save preview scene."); }
                Debug.Log($"CAULDRON_IMPORTED: {tileSources.Length} nested prefab tile instances, " +
                    $"{variants.Count} material variants, one shared tile mesh; map Scale=1; scene={ScenePath}. " +
                    "No Play Mode tests were run and build settings were not changed.");
            }
            finally
            {
                if (imported != null) { Object.DestroyImmediate(imported); }
                if (SceneManager.sceneCount > 1) { EditorSceneManager.CloseScene(scene, true); }
                if (originalScene.IsValid() && originalScene.isLoaded) { SceneManager.SetActiveScene(originalScene); }
            }
        }

        /// <summary>Opens the map-only scene with Unity's normal unsaved-scene prompt.</summary>
        [MenuItem("Tools/Paw Arena/Maps/Cauldron/Open Map Scene")]
        public static void OpenPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            EditorSceneManager.OpenScene(ScenePath);
            var map = GameObject.Find("Cauldron Map - Scale This Root");
            Selection.activeGameObject = map;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0, 17, 0), Quaternion.Euler(20, -30, 0), 48);
        }

        private static void ConfigureImporter()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) { throw new InvalidOperationException("FBX importer was not available."); }
            Undo.RecordObject(importer, "Configure Cauldron FBX units");
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }

        private static Dictionary<string, Material> CreateMaterials(IEnumerable<MeshFilter> filters)
        {
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { throw new InvalidOperationException("URP Lit shader is unavailable."); }
            foreach (var source in filters.SelectMany(item => item.GetComponent<MeshRenderer>().sharedMaterials).Distinct())
            {
                if (source == null) { throw new InvalidOperationException("Imported mesh has a missing material."); }
                var path = MaterialFolder + "/" + source.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = source.name };
                    AssetDatabase.CreateAsset(material, path);
                }
                Undo.RecordObject(material, "Configure Cauldron URP material");
                material.shader = shader;
                var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", source.name.Contains("Cauldron") ? .35f : .15f);
                material.SetFloat("_Metallic", source.name.Contains("Gold") ? .35f : 0);
                if (source.name.StartsWith("M_Potion_", StringComparison.Ordinal))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 1.4f);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                result.Add(source.name, material);
            }
            return result;
        }

        private static Material[] RemapMaterials(MeshFilter source, IReadOnlyDictionary<string, Material> materials) =>
            source.GetComponent<MeshRenderer>().sharedMaterials.Select(item => materials[item.name]).ToArray();

        private static Mesh BakeWorldAxes(MeshFilter source, string path)
        {
            var mesh = Object.Instantiate(source.sharedMesh);
            mesh.name = Path.GetFileNameWithoutExtension(path);
            var matrix = source.transform.localToWorldMatrix;
            var vertices = mesh.vertices;
            for (var i = 0; i < vertices.Length; i++) { vertices[i] = matrix.MultiplyVector(vertices[i]); }
            mesh.vertices = vertices;
            var normals = mesh.normals;
            var normalMatrix = matrix.inverse.transpose;
            for (var i = 0; i < normals.Length; i++) { normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized; }
            mesh.normals = normals;
            if (matrix.determinant < 0)
            {
                for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    var triangles = mesh.GetTriangles(subMesh);
                    for (var i = 0; i < triangles.Length; i += 3)
                    {
                        (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                    }
                    mesh.SetTriangles(triangles, subMesh);
                }
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            Undo.RecordObject(existing, "Update Cauldron baked mesh");
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static GameObject CreateBaseTile(Scene scene, MeshFilter source, Mesh mesh,
            IReadOnlyDictionary<string, Material> materials)
        {
            var tile = Root("PF_CauldronTile", scene);
            try
            {
                SetGroundLayer(tile);
                Undo.AddComponent<MeshFilter>(tile).sharedMesh = mesh;
                Undo.AddComponent<MeshRenderer>(tile).sharedMaterials = RemapMaterials(source, materials);
                var collider = Undo.AddComponent<MeshCollider>(tile);
                collider.sharedMesh = mesh;
                collider.convex = true;
                return PrefabUtility.SaveAsPrefabAsset(tile, TilePath);
            }
            finally { Undo.DestroyObjectImmediate(tile); }
        }

        private static Dictionary<string, GameObject> CreateTileVariants(Scene scene,
            IEnumerable<MeshFilter> sources, GameObject baseTile, IReadOnlyDictionary<string, Material> materials)
        {
            var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (var source in sources)
            {
                var key = source.GetComponent<MeshRenderer>().sharedMaterials[0].name;
                if (result.ContainsKey(key)) { continue; }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(baseTile, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Create Cauldron tile variant");
                try
                {
                    var renderer = instance.GetComponent<MeshRenderer>();
                    Undo.RecordObject(renderer, "Assign Cauldron tile palette");
                    renderer.sharedMaterials = RemapMaterials(source, materials);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    var name = "PF_Tile_" + key.Replace("M_", "");
                    instance.name = name;
                    result.Add(key, PrefabUtility.SaveAsPrefabAsset(instance, PrefabFolder + "/Tiles/" + name + ".prefab"));
                }
                finally { Undo.DestroyObjectImmediate(instance); }
            }
            return result;
        }

        private static GameObject Root(string name, Scene scene)
        {
            var obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj, scene);
            Undo.RegisterCreatedObjectUndo(obj, "Create Cauldron preview object");
            return obj;
        }

        private static Transform Child(Transform parent, string name)
        {
            var obj = Root(name, parent.gameObject.scene);
            obj.transform.SetParent(parent, false);
            return obj.transform;
        }

        private static void SetGroundLayer(GameObject obj)
        {
            var layer = LayerMask.NameToLayer("Ground");
            obj.layer = layer >= 0 ? layer : 0;
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
