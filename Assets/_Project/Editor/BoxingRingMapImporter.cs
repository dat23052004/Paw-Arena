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
    /// <summary>One-time, isolated import of the authored Boxing Ring. Does not install gameplay physics.</summary>
    public static class BoxingRingMapImporter
    {
        private const string Source = "Blender/Map/BoxingRing/Exports/";
        private const string Models = "Assets/_Project/Models/Maps/BoxingRing/";
        private const string Materials = "Assets/_Project/Materials/Maps/BoxingRing/";
        private const string Prefabs = "Assets/_Project/Prefabs/Maps/BoxingRing/";
        private const string ScenePath = "Assets/_Project/Scenes/Maps/BoxingRing.unity";
        private static readonly Dictionary<string, Material> Palette = new();
        private static readonly Dictionary<string, GameObject> Parts = new();

        /// <summary>Run with -executeMethod in a closed-editor batch session; leaves other maps untouched.</summary>
        public static void ImportAndRender()
        {
            if (!Application.isBatchMode)
            { throw new InvalidOperationException("Run this one-time importer in an isolated Unity batch session."); }
            if (File.Exists(ScenePath) || File.Exists(Prefabs + "PF_Map_BoxingRing.prefab"))
            { throw new InvalidOperationException("Boxing Ring already exists; refusing to overwrite authored map assets."); }
            Palette.Clear();
            Parts.Clear();
            foreach (var folder in new[] { Models + "Meshes", Materials, Prefabs, "Assets/_Project/Scenes/Maps" })
            { Directory.CreateDirectory(folder); }
            foreach (var file in new[] { "BoxingRing_Map.fbx", "Boxing_Ground_Albedo.png", "BoxingRing_Layout.json" })
            { File.Copy(Source + file, Models + file, true); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Models + "BoxingRing_Map.fbx");
            Undo.RecordObject(importer, "Configure Boxing Ring model");
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Models + "Boxing_Ground_Albedo.png");
            Undo.RecordObject(textureImporter, "Configure Boxing Ring albedo");
            textureImporter.sRGBTexture = true;
            textureImporter.mipmapEnabled = true;
            textureImporter.filterMode = FilterMode.Trilinear;
            textureImporter.anisoLevel = 8;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.SaveAndReimport();
            CreatePalette();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "BoxingRing_Map.fbx");
            // Unity can collapse the single FBX root and rename it after the file.
            var authoredRoot = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MAP_BoxingRing");
            if (authoredRoot == null) { authoredRoot = model.transform; }
            var root = CopyNode(authoredRoot, null);
            root.name = "PF_Map_BoxingRing";
            var spawnComponent = Undo.AddComponent<MapSpawnPoints>(root);
            var spawnData = new SerializedObject(spawnComponent);
            spawnData.Update();
            var markers = root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Spawn_", StringComparison.Ordinal))
                .OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
            var points = spawnData.FindProperty("_points");
            points.arraySize = markers.Length;
            for (var i = 0; i < markers.Length; i++) { points.GetArrayElementAtIndex(i).objectReferenceValue = markers[i]; }
            spawnData.ApplyModifiedProperties();
            NormalizeMapTransforms(root);
            var mapPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, Prefabs + "PF_Map_BoxingRing.prefab", InteractionMode.AutomatedAction);
            if (mapPrefab == null) { throw new InvalidOperationException("Could not save Boxing Ring prefab."); }

            EditorSceneManager.SaveScene(scene, ScenePath);
            RenderPresentation();
            Directory.CreateDirectory("Logs");
            var ground = root.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Ground_Island");
            var report = $"BOXING_RING_IMPORTED\nScene: {ScenePath}\nPrefab: {AssetDatabase.GetAssetPath(mapPrefab)}\n" +
                $"Renderers: {root.GetComponentsInChildren<MeshRenderer>().Length}\n" +
                $"Colliders: {root.GetComponentsInChildren<Collider>().Length}\nSpawn markers: {markers.Length}\n" +
                $"Ground bounds: {ground.bounds}\n" +
                $"Non-unit transforms: {root.GetComponentsInChildren<Transform>().Count(t => (t.localScale - Vector3.one).sqrMagnitude > .000001f)}\n" +
                "Ropes: 48 static capsule links in 8 strands; shared nested prefab. Chairs: 28 shared nested prefabs.\n" +
                "No Rigidbody, joints or breaking behaviour installed. No tests or Play Mode run.\n";
            File.WriteAllText("Logs/BoxingRing-import.txt", report);
            Debug.Log(report);
        }

        private static void CreatePalette()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { throw new InvalidOperationException("URP Lit shader is unavailable."); }
            var colors = new Dictionary<string, string>
            {
                { "Ground", "FFFFFF" }, { "Canvas", "D6D1B1" }, { "Apron", "393C37" },
                { "Trim", "222A28" }, { "Posts", "282F2C" }, { "Collars", "55513B" },
                { "Ropes_Red", "B6291D" }, { "CornerPads", "9D2718" }, { "Emblem", "48432A" },
                { "ChairSeat", "47452B" }, { "ChairFrame", "1C2622" }
            };
            foreach (var pair in colors)
            {
                var name = "M_Boxing_" + pair.Key;
                ColorUtility.TryParseHtmlString("#" + pair.Value, out var color);
                var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
                var isNew = material == null;
                if (isNew) { material = new Material(shader) { name = name }; }
                Undo.RecordObject(material, "Set Boxing Ring material");
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", pair.Key == "Ropes_Red" ? .32f : .16f);
                material.SetFloat("_Metallic", pair.Key == "Posts" || pair.Key == "ChairFrame" ? .15f : 0);
                if (pair.Key == "Ground")
                { material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "Boxing_Ground_Albedo.png")); }
                if (isNew) { AssetDatabase.CreateAsset(material, Materials + name + ".mat"); }
                else { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
                Palette.Add(name, material);
            }
        }

        private static GameObject CopyNode(Transform source, Transform parent)
        {
            var filter = source.GetComponent<MeshFilter>();
            var partKey = source.name.StartsWith("Chair_", StringComparison.Ordinal) ? "Chair" :
                source.name.Contains("_Link_") ? "RopeLink" : null;
            if (filter != null && partKey != null && Parts.TryGetValue(partKey, out var prefab))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Place Boxing Ring part");
                SetPose(instance.transform, source, parent);
                instance.name = source.name;
                return instance;
            }
            var obj = new GameObject(source.name);
            Undo.RegisterCreatedObjectUndo(obj, "Import Boxing Ring object");
            SetPose(obj.transform, source, parent);
            if (filter != null)
            {
                var mesh = BakeUnitMesh(filter, partKey ?? source.name);
                Undo.AddComponent<MeshFilter>(obj).sharedMesh = mesh;
                var renderer = Undo.AddComponent<MeshRenderer>(obj);
                renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials.Select(material =>
                {
                    if (material == null || !Palette.TryGetValue(material.name, out var mapped))
                    { throw new InvalidOperationException($"Unknown Boxing Ring material on {source.name}: {material?.name}"); }
                    return mapped;
                }).ToArray();
                AddCollider(obj, mesh, partKey);
            }
            foreach (Transform child in source) { CopyNode(child, obj.transform); }
            if (filter != null && partKey != null)
            {
                var position = obj.transform.position;
                var rotation = obj.transform.rotation;
                obj.transform.SetParent(null, true);
                obj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var part = PrefabUtility.SaveAsPrefabAssetAndConnect(obj, Prefabs + "PF_Boxing_" + partKey + ".prefab", InteractionMode.AutomatedAction);
                if (part == null) { throw new InvalidOperationException("Could not save Boxing Ring part."); }
                Parts.Add(partKey, part);
                obj.transform.SetPositionAndRotation(position, rotation);
                obj.transform.SetParent(parent, true);
            }
            return obj;
        }

        private static void SetPose(Transform target, Transform source, Transform parent)
        {
            target.SetParent(parent, false);
            target.SetPositionAndRotation(source.position, source.rotation);
            target.localScale = Vector3.one;
        }

        private static void NormalizeMapTransforms(GameObject root)
        {
            var poses = root.GetComponentsInChildren<Transform>(true)
                .Select(t => new { Transform = t, Position = t.position, Rotation = t.rotation }).ToArray();
            foreach (var pose in poses)
            {
                var transform = pose.Transform;
                Undo.RecordObject(transform, "Normalize Boxing Ring axes");
                var rotation = transform.GetComponent<MeshFilter>() != null || transform.name.StartsWith("End_", StringComparison.Ordinal)
                    ? pose.Rotation : Quaternion.identity;
                if (transform.name.StartsWith("Spawn_", StringComparison.Ordinal))
                { rotation = Quaternion.LookRotation(new Vector3(-pose.Position.x, 0, -pose.Position.z), Vector3.up); }
                transform.SetPositionAndRotation(pose.Position, rotation);
                transform.localScale = Vector3.one;
            }
        }

        private static Mesh BakeUnitMesh(MeshFilter source, string name)
        {
            var mesh = Object.Instantiate(source.sharedMesh);
            mesh.name = "SM_Boxing_" + name;
            // Keep the source rotation/pivot, but bake FBX unit conversion into geometry.
            var matrix = Matrix4x4.Rotate(Quaternion.Inverse(source.transform.rotation)) * source.transform.localToWorldMatrix;
            var normals = matrix.inverse.transpose;
            mesh.vertices = mesh.vertices.Select(matrix.MultiplyVector).ToArray();
            mesh.normals = mesh.normals.Select(n => normals.MultiplyVector(n).normalized).ToArray();
            if (matrix.determinant < 0)
            {
                for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    var triangles = mesh.GetTriangles(submesh);
                    for (var i = 0; i < triangles.Length; i += 3)
                    { (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]); }
                    mesh.SetTriangles(triangles, submesh);
                }
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, Models + "Meshes/" + mesh.name + ".asset");
            return mesh;
        }

        private static void AddCollider(GameObject obj, Mesh mesh, string partKey)
        {
            if (obj.name == "Mat_Emblem_Cat_And_Stars") { return; }
            obj.layer = LayerMask.NameToLayer("Ground");
            if (partKey == "RopeLink")
            {
                var collider = Undo.AddComponent<CapsuleCollider>(obj);
                var size = mesh.bounds.size;
                var axis = size.x > size.y ? 0 : 1;
                if (size.z > size[axis]) { axis = 2; }
                collider.direction = axis;
                collider.center = mesh.bounds.center;
                collider.radius = .085f;
                collider.height = size[axis];
            }
            else if (partKey == "Chair" || obj.name.StartsWith("Post_", StringComparison.Ordinal))
            {
                // Static accurate collision keeps chair legs/back open. Dynamic props need compound colliders later.
                Undo.AddComponent<MeshCollider>(obj).sharedMesh = mesh;
            }
            else
            {
                var collider = Undo.AddComponent<BoxCollider>(obj);
                collider.center = mesh.bounds.center;
                collider.size = mesh.bounds.size;
            }
        }

        private static Camera CreatePresentation()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.5f, .5f, .5f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            var sunObject = new GameObject("Sun");
            Undo.RegisterCreatedObjectUndo(sunObject, "Create map lighting");
            var sun = Undo.AddComponent<Light>(sunObject);
            sun.type = LightType.Directional;
            sun.color = Color.white;
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.sun = sun;
            var cameraObject = new GameObject("Map Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create map camera");
            cameraObject.tag = "MainCamera";
            var camera = Undo.AddComponent<Camera>(cameraObject);
            Undo.AddComponent<UniversalAdditionalCameraData>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.43f, .76f, .87f);
            camera.transform.position = new Vector3(22, 26, -31);
            camera.transform.LookAt(new Vector3(0, .6f, 0));
            camera.orthographic = true;
            camera.orthographicSize = 16;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 150;
            return camera;
        }

        private static void RenderPresentation()
        {
            // Presentation is temporary and is never serialized into the map scene.
            var camera = CreatePresentation();
            var sun = RenderSettings.sun;
            try { Render(camera); }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(sun.gameObject);
                RenderSettings.sun = null;
            }
        }

        private static void Render(Camera camera)
        {
            var target = new RenderTexture(1500, 1200, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(1500, 1200, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                target.Create();
                camera.aspect = 1.25f;
                // Initialize URP's render resources before reading the finished frame.
                for (var frame = 0; frame < 3; frame++)
                { RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target }); }
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1500, 1200), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Blender/Map/BoxingRing/Previews/BoxingRing_Unity.png", pixels.EncodeToPNG());
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        /// <summary>Capture the saved scene without reimporting or modifying assets.</summary>
        public static void CaptureSavedScene()
        {
            if (!Application.isBatchMode) { throw new InvalidOperationException("Batch capture only."); }
            EditorSceneManager.OpenScene(ScenePath);
            RenderPresentation();
            Debug.Log("BOXING_RING_SAVED_SCENE_RENDERED");
        }

        /// <summary>Finalize unit axes while keeping the saved mesh world poses and prefab identities.</summary>
        public static void FinalizeSavedMap()
        {
            if (!Application.isBatchMode) { throw new InvalidOperationException("Batch authoring only."); }
            var path = Prefabs + "PF_Map_BoxingRing.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                NormalizeMapTransforms(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var instance = scene.GetRootGameObjects().Single(obj => obj.name == "PF_Map_BoxingRing");
            Undo.RecordObject(instance.transform, "Normalize Boxing Ring scene root");
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            EditorSceneManager.SaveScene(scene);
            RenderPresentation();
            File.AppendAllText("Logs/BoxingRing-import.txt", "Finalized: identity map root and group rotations; spawn markers face ring centre.\n");
            Debug.Log("BOXING_RING_FINALIZED");
        }
    }
}
