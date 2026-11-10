using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PawArena.Gameplay.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PawArena.Editor
{
    /// <summary>Imports authored Pirate Ships assets into a map-only scene, without installing gameplay.</summary>
    public static class PirateShipsMapImporter
    {
        private const string Source = "Blender/Map/PirateShips/Exports/";
        private const string Models = "Assets/_Project/Models/Maps/PirateShips/";
        private const string Materials = "Assets/_Project/Materials/Maps/PirateShips/";
        private const string Prefabs = "Assets/_Project/Prefabs/Maps/PirateShips/";
        private const string ScenePath = "Assets/_Project/Scenes/Maps/PirateShips.unity";
        private static readonly Dictionary<string, Material> Palette = new();
        private static readonly Dictionary<string, GameObject> Planks = new();

        /// <summary>One-time batch import. Refuses to overwrite an existing authored map.</summary>
        public static void ImportAndRender()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Run in a closed-editor batch session to preserve open scenes.");
            }
            if (File.Exists(ScenePath) || File.Exists(Prefabs + "PF_Map_PirateShips.prefab"))
            {
                throw new InvalidOperationException("Pirate Ships already exists; refusing to overwrite authored map assets.");
            }
            Palette.Clear();
            Planks.Clear();
            foreach (var folder in new[] { Models + "Meshes", Models + "Textures", Materials, Prefabs + "Planks",
                         Prefabs + "Cannons", Prefabs + "Damage", "Assets/_Project/Scenes/Maps", "Logs" })
            {
                Directory.CreateDirectory(folder);
            }
            var files = new[] { "PirateShips_Map.fbx", "PirateShips_DeckDamage_Round.fbx", "PirateShips_PlankDamage_Variants.fbx",
                "PirateShips_Layout.json", "PirateShips_Materials.json" };
            foreach (var file in files) { File.Copy(Source + file, Models + file, true); }
            foreach (var path in Directory.GetFiles(Source + "Textures", "*.png"))
            {
                File.Copy(path, Models + "Textures/" + Path.GetFileName(path), true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var file in files.Where(f => f.EndsWith(".fbx", StringComparison.Ordinal))) { ConfigureModel(file); }
            foreach (var path in Directory.GetFiles(Models + "Textures", "*.png")) { ConfigureTexture(path.Replace('\\', '/')); }
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "PirateShips_Map.fbx");
            var root = CopyNode(model.transform, null, "Map", true);
            NormalizeRoot(root);
            root.name = "PF_Map_PirateShips";
            var markers = root.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("_Spawn_"))
                .OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
            foreach (var marker in markers)
            {
                marker.rotation = Quaternion.LookRotation(new Vector3(-marker.position.x, 0, 0), Vector3.up);
            }
            var spawn = Undo.AddComponent<MapSpawnPoints>(root);
            var serialized = new SerializedObject(spawn);
            serialized.Update();
            var points = serialized.FindProperty("_points");
            points.arraySize = markers.Length;
            for (var i = 0; i < markers.Length; i++) { points.GetArrayElementAtIndex(i).objectReferenceValue = markers[i]; }
            serialized.ApplyModifiedProperties();

            var cannonRoots = root.GetComponentsInChildren<Transform>().Where(t =>
                t.name.StartsWith("Blue_Cannon_", StringComparison.Ordinal) || t.name.StartsWith("Red_Cannon_", StringComparison.Ordinal)).ToArray();
            foreach (var cannon in cannonRoots)
            {
                if (cannon.GetComponent<MeshFilter>() == null && cannon.name.Length == "Blue_Cannon_01".Length ||
                    cannon.GetComponent<MeshFilter>() == null && cannon.name.Length == "Red_Cannon_01".Length)
                {
                    SavePart(cannon.gameObject, Prefabs + "Cannons/PF_" + cannon.name + ".prefab");
                }
            }
            CreateDamagePrefabs();
            ValidateMap(root, markers.Length);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, Prefabs + "PF_Map_PirateShips.prefab", InteractionMode.AutomatedAction);
            if (prefab == null) { throw new InvalidOperationException("Failed to save map prefab."); }
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) { throw new InvalidOperationException("Failed to save map scene."); }
            AssetDatabase.SaveAssets();
            RenderPresentation();
            var report = "PIRATE_SHIPS_IMPORTED\nScene: " + ScenePath + "\n" +
                $"Scene roots: {scene.rootCount}\nRenderers: {root.GetComponentsInChildren<MeshRenderer>().Length}\n" +
                $"Deck plank prefab assets: {Planks.Count}; deck instances: {root.GetComponentsInChildren<MeshFilter>().Count(f => IsPlank(f.name))}\n" +
                $"Colliders: {root.GetComponentsInChildren<Collider>().Length}; spawns: {markers.Length}\n" +
                "Cannons: 4, original yaw/pitch pivots and muzzle markers retained.\n" +
                "Damage prefabs: 6 authored alternatives; disabled by omission from intact scene.\n" +
                "All map transform scales 1; no scene camera/light/HUD/player; water has no solid collider.\n" +
                "No firing, destruction, swimming or multiplayer logic installed. No tests or Play Mode run.\n";
            File.WriteAllText("Logs/PirateShips-import.txt", report);
            Debug.Log(report);
        }

        private static void ConfigureModel(string file)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Models + file);
            Undo.RecordObject(importer, "Configure Pirate Ships import");
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Undo.RecordObject(importer, "Configure Pirate Ships texture");
            importer.textureType = path.Contains("_Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = path.Contains("_Albedo");
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        private static void CreateMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { throw new InvalidOperationException("URP Lit is unavailable."); }
            var data = JsonUtility.FromJson<MaterialList>(File.ReadAllText(Models + "PirateShips_Materials.json"));
            foreach (var entry in data.materials)
            {
                var mat = new Material(shader) { name = entry.name };
                var c = entry.linearColor;
                mat.SetColor("_BaseColor", new Color(c[0], c[1], c[2], c[3]).gamma);
                mat.SetFloat("_Metallic", entry.metallic);
                mat.SetFloat("_Smoothness", entry.smoothness);
                if (entry.wood)
                {
                    mat.SetTexture("_BaseMap", LoadTexture("Pirate_DeckWood_Albedo"));
                    mat.SetTexture("_BumpMap", LoadTexture("Pirate_DeckWood_Normal"));
                    mat.SetFloat("_BumpScale", .22f);
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_MetallicGlossMap", LoadTexture("Pirate_DeckWood_MetallicSmoothness"));
                    mat.SetFloat("_Smoothness", 1);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                if (entry.name == "M_Pirate_Water_Preview")
                {
                    mat.SetColor("_BaseColor", Color.white);
                    mat.SetTexture("_BaseMap", CreateWaterTexture());
                }
                AssetDatabase.CreateAsset(mat, Materials + mat.name + ".mat");
                Palette.Add(entry.name, mat);
            }
        }

        private static Texture2D LoadTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "Textures/" + name + ".png");

        private static Texture2D CreateWaterTexture()
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, true);
            var colors = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dark = ((x * 23 / size + y * 23 / size) & 1) == 0;
                    colors[y * size + x] = dark ? new Color(.22f, .56f, .66f) : new Color(.24f, .60f, .69f);
                }
            }
            texture.SetPixels(colors);
            texture.Apply();
            var path = Models + "Textures/Pirate_Water_Albedo.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static bool IsPlank(string name) => name.Contains("_Deck_R") || name.Contains("_CabinEntrance_Plank_");

        private static GameObject CopyNode(Transform source, Transform parent, string context, bool plankPrefabs)
        {
            var filter = source.GetComponent<MeshFilter>();
            var plankKey = plankPrefabs && IsPlank(source.name) ? source.name.Substring(source.name.IndexOf('_') + 1) : null;
            if (plankKey != null && Planks.TryGetValue(plankKey, out var shared))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(shared);
                SetPose(instance.transform, source, parent);
                instance.name = source.name;
                return instance;
            }
            var obj = new GameObject(source.name);
            Undo.RegisterCreatedObjectUndo(obj, "Import Pirate Ships geometry");
            SetPose(obj.transform, source, parent);
            if (filter != null)
            {
                var mesh = BakeMesh(filter, context);
                Undo.AddComponent<MeshFilter>(obj).sharedMesh = mesh;
                var renderer = Undo.AddComponent<MeshRenderer>(obj);
                renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials.Select(m =>
                    m != null && Palette.TryGetValue(m.name, out var mapped) ? mapped :
                    throw new InvalidOperationException("Unmapped material: " + m?.name)).ToArray();
                if (source.name == "Ocean_Surface")
                {
                    mesh.uv = mesh.vertices.Select(v =>
                    {
                        var world = source.position + source.rotation * v;
                        return new Vector2((world.x + 20) / 40, (world.z + 17) / 34);
                    }).ToArray();
                    mesh.RecalculateTangents();
                    EditorUtility.SetDirty(mesh);
                    obj.layer = LayerMask.NameToLayer("Water");
                }
                else if (!source.name.Contains("Splinters") && !source.name.Contains("Sail") && !source.name.Contains("Pennant"))
                {
                    obj.layer = LayerMask.NameToLayer("Ground");
                    // Accurate static collision: no convex hull/box across concave hulls or cut holes.
                    Undo.AddComponent<MeshCollider>(obj).sharedMesh = mesh;
                }
            }
            foreach (Transform child in source) { CopyNode(child, obj.transform, context, plankPrefabs); }
            if (plankKey != null)
            {
                Planks.Add(plankKey, SavePart(obj, Prefabs + "Planks/PF_Pirate_" + plankKey + ".prefab"));
            }
            return obj;
        }

        private static void SetPose(Transform target, Transform source, Transform parent)
        {
            target.SetParent(parent, false);
            target.SetPositionAndRotation(source.position, source.rotation);
            target.localScale = Vector3.one;
        }

        private static Mesh BakeMesh(MeshFilter source, string context)
        {
            var mesh = Object.Instantiate(source.sharedMesh);
            mesh.name = "SM_Pirate_" + context + "_" + source.name;
            var matrix = Matrix4x4.Rotate(Quaternion.Inverse(source.transform.rotation)) * source.transform.localToWorldMatrix;
            mesh.vertices = mesh.vertices.Select(matrix.MultiplyVector).ToArray();
            mesh.normals = mesh.normals.Select(n => matrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
            if (matrix.determinant < 0)
            {
                for (var s = 0; s < mesh.subMeshCount; s++)
                {
                    var triangles = mesh.GetTriangles(s);
                    for (var i = 0; i < triangles.Length; i += 3)
                    {
                        (triangles[i+1], triangles[i+2]) = (triangles[i+2], triangles[i+1]);
                    }
                    mesh.SetTriangles(triangles, s);
                }
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, Models + "Meshes/" + mesh.name + ".asset");
            return mesh;
        }

        private static GameObject SavePart(GameObject obj, string path)
        {
            var parent = obj.transform.parent;
            var position = obj.transform.position;
            var rotation = obj.transform.rotation;
            obj.transform.SetParent(null, true);
            obj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(obj, path, InteractionMode.AutomatedAction);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.transform.SetParent(parent, true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
            if (prefab == null) { throw new InvalidOperationException("Could not save " + path); }
            return prefab;
        }

        private static void NormalizeRoot(GameObject root)
        {
            var poses = root.GetComponentsInChildren<Transform>().Skip(1)
                .Select(t => new { Transform = t, Position = t.position, Rotation = t.rotation }).ToArray();
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            foreach (var pose in poses) { pose.Transform.SetPositionAndRotation(pose.Position, pose.Rotation); }
        }

        private static void CreateDamagePrefabs()
        {
            foreach (var file in new[] { "PirateShips_DeckDamage_Round.fbx", "PirateShips_PlankDamage_Variants.fbx" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + file);
                var root = CopyNode(model.transform, null, Path.GetFileNameWithoutExtension(file), false);
                NormalizeRoot(root);
                if (file.Contains("Round"))
                {
                    root.name = "PF_Pirate_DeckDamage_Round_01";
                    PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "Damage/" + root.name + ".prefab");
                }
                else
                {
                    var groups = root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("PlankDamage_", StringComparison.Ordinal)
                        && t.name != "PlankDamage_Variants" && t.GetComponent<MeshFilter>() == null).ToArray();
                    if (groups.Length != 5) { throw new InvalidOperationException("Expected five plank damage groups, found " + groups.Length); }
                    foreach (var group in groups)
                    {
                        group.SetParent(null, true);
                        NormalizeRoot(group.gameObject);
                        PrefabUtility.SaveAsPrefabAsset(group.gameObject, Prefabs + "Damage/PF_Pirate_" + group.name + ".prefab");
                        Object.DestroyImmediate(group.gameObject);
                    }
                }
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateMap(GameObject root, int spawns)
        {
            if (spawns != 8 || root.GetComponentsInChildren<MeshFilter>().Count(f => IsPlank(f.name)) != 222)
            {
                throw new InvalidOperationException("Missing deck chunks or spawn markers.");
            }
            if (root.GetComponentsInChildren<Transform>().Any(t => (t.localScale-Vector3.one).sqrMagnitude > .000001f))
            {
                throw new InvalidOperationException("Unexpected non-unit map scale.");
            }
            var ocean = root.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Ocean_Surface");
            if (Mathf.Abs(ocean.bounds.size.x-40) > .01f || Mathf.Abs(ocean.bounds.size.z-34) > .01f)
            {
                throw new InvalidOperationException("Incorrect imported units/axes: " + ocean.bounds);
            }
            if (root.GetComponentsInChildren<Camera>().Length + root.GetComponentsInChildren<Light>().Length != 0)
            {
                throw new InvalidOperationException("Presentation objects must not be in map prefab.");
            }
        }

        /// <summary>Renders saved map geometry without saving presentation helpers.</summary>
        public static void CaptureSavedScene()
        {
            if (!Application.isBatchMode) { throw new InvalidOperationException("Batch capture only."); }
            EditorSceneManager.OpenScene(ScenePath);
            RenderPresentation();
        }

        private static void RenderPresentation()
        {
            var cameraObject = new GameObject("Temporary Pirate Ships Capture");
            var sunObject = new GameObject("Temporary Pirate Ships Lighting");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.43f, .76f, .87f);
            camera.orthographic = true;
            camera.orthographicSize = 23;
            camera.transform.position = new Vector3(37, 36, 43);
            camera.transform.LookAt(new Vector3(0, 3, 0));
            camera.farClipPlane = 200;
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.5f, .5f, .5f);
            RenderSettings.skybox = null;
            var target = new RenderTexture(1500, 1100, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(1500, 1100, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                target.Create();
                camera.aspect = 1500f/1100;
                for (var i = 0; i < 3; i++)
                {
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                }
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1500, 1100), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Blender/Map/PirateShips/Previews/PirateShips_Unity.png", pixels.EncodeToPNG());
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(sunObject);
                RenderSettings.sun = null;
            }
        }

        // JSON transfer records, not Inspector configuration or runtime components.
        [Serializable]
        private sealed class MaterialList { public MaterialEntry[] materials; }

        [Serializable]
        private sealed class MaterialEntry
        {
            public string name;
            public float[] linearColor;
            public bool wood;
            public float metallic;
            public float smoothness;
        }
    }
}
