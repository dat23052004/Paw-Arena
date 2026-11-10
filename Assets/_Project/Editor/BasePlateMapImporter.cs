using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PawArena.Editor
{
    /// <summary>Updates BasePlate as one textured slab without replacing existing scene references.</summary>
    public static class BasePlateMapImporter
    {
        private const string Source = "Blender/Map/BasePlate/Exports/";
        private const string Models = "Assets/_Project/Models/Maps/BasePlate/";
        private const string Materials = "Assets/_Project/Materials/Maps/BasePlate/";
        private const string MapPath = "Assets/_Project/Prefabs/Maps/BasePlate/PF_Map_BasePlate.prefab";
        private const string RequestPath = "Logs/BasePlate.import-request.txt";

        [InitializeOnLoadMethod]
        private static void ScheduleImport()
        {
            if (!Application.isBatchMode && File.Exists(RequestPath)) { EditorApplication.update += ImportWhenReady; }
        }

        private static void ImportWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { return; }
            EditorApplication.update -= ImportWhenReady;
            try { Import(); }
            catch (Exception exception)
            {
                File.WriteAllText("Logs/BasePlate-import-error.txt", exception.ToString());
                Debug.LogException(exception);
            }
        }

        /// <summary>Reimports one slab, preserving spawn markers and all open scenes.</summary>
        [MenuItem("Tools/Paw Arena/Maps/BasePlate/Reimport Slab")]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            { throw new InvalidOperationException("Exit Play Mode and Prefab Mode before reimporting BasePlate."); }
            if (!File.Exists(MapPath)) { throw new FileNotFoundException("Existing BasePlate map is required.", MapPath); }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { throw new InvalidOperationException("URP Lit is unavailable."); }
            foreach (var file in new[] { "BasePlate_Map.fbx", "BasePlate_Albedo.png" })
            {
                File.Copy(Source + file, Models + file, true);
                AssetDatabase.ImportAsset(Models + file, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(Models + "BasePlate_Map.fbx");
            Undo.RecordObject(importer, "Configure BasePlate import");
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Models + "BasePlate_Albedo.png");
            Undo.RecordObject(textureImporter, "Configure BasePlate texture");
            textureImporter.sRGBTexture = true;
            textureImporter.mipmapEnabled = true;
            textureImporter.filterMode = FilterMode.Trilinear;
            textureImporter.anisoLevel = 8;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "BasePlate_Map.fbx");
            var source = model.GetComponentsInChildren<MeshFilter>().Single();
            var mesh = Object.Instantiate(source.sharedMesh);
            mesh.name = "SM_BasePlate";
            var matrix = source.transform.localToWorldMatrix;
            mesh.vertices = mesh.vertices.Select(matrix.MultiplyVector).ToArray();
            mesh.normals = mesh.normals.Select(n => matrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
            if (matrix.determinant < 0)
            {
                var triangles = mesh.triangles;
                for (var i = 0; i < triangles.Length; i += 3)
                { (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]); }
                mesh.triangles = triangles;
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            var meshPath = Models + "Meshes/SM_BasePlate.asset";
            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (savedMesh == null) { AssetDatabase.CreateAsset(mesh, meshPath); savedMesh = mesh; }
            else
            {
                Undo.RecordObject(savedMesh, "Update BasePlate mesh");
                EditorUtility.CopySerialized(mesh, savedMesh);
                Object.DestroyImmediate(mesh);
                AssetDatabase.SaveAssetIfDirty(savedMesh);
            }
            var materialPath = Materials + "M_BasePlate.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_BasePlate" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Undo.RecordObject(material, "Update BasePlate surface");
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "BasePlate_Albedo.png"));
            material.SetFloat("_Smoothness", .14f);
            material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);

            var map = PrefabUtility.LoadPrefabContents(MapPath);
            try
            {
                var oldTiles = map.transform.Find("Tiles");
                if (oldTiles != null) { Undo.DestroyObjectImmediate(oldTiles.gameObject); }
                var surface = map.transform.Find("Surface");
                if (surface == null)
                {
                    var obj = new GameObject("Surface");
                    Undo.RegisterCreatedObjectUndo(obj, "Create BasePlate slab");
                    obj.transform.SetParent(map.transform, false);
                    surface = obj.transform;
                }
                Undo.RecordObject(surface.gameObject, "Set BasePlate ground layer");
                surface.gameObject.layer = LayerMask.NameToLayer("Ground");
                var filter = surface.GetComponent<MeshFilter>();
                if (filter == null) { filter = Undo.AddComponent<MeshFilter>(surface.gameObject); }
                var renderer = surface.GetComponent<MeshRenderer>();
                if (renderer == null) { renderer = Undo.AddComponent<MeshRenderer>(surface.gameObject); }
                var collider = surface.GetComponent<BoxCollider>();
                if (collider == null) { collider = Undo.AddComponent<BoxCollider>(surface.gameObject); }
                Undo.RecordObjects(new Object[] { filter, renderer, collider }, "Update BasePlate slab");
                filter.sharedMesh = savedMesh;
                renderer.sharedMaterial = material;
                collider.center = savedMesh.bounds.center;
                collider.size = savedMesh.bounds.size;
                PrefabUtility.SaveAsPrefabAsset(map, MapPath);
                File.WriteAllText("Logs/BasePlate-import.txt",
                    $"BASEPLATE_SINGLE_SLAB\nMeshes: {map.GetComponentsInChildren<MeshFilter>().Length}\n" +
                    $"Colliders: {map.GetComponentsInChildren<Collider>().Length}\nTriangles: {savedMesh.triangles.Length / 3}\n" +
                    $"Size: {savedMesh.bounds.size}\nOne material and texture; no individual tile prefabs.\n" +
                    "Existing scenes and spawn references preserved. No tests, commit or push.\n");
            }
            finally { PrefabUtility.UnloadPrefabContents(map); }
            // Keep the superseded assets recoverable outside Unity, not in the active map folders.
            var obsolete = new[]
            {
                "Assets/_Project/Prefabs/Maps/BasePlate/Tiles/PF_BasePlateTile_Light.prefab",
                "Assets/_Project/Prefabs/Maps/BasePlate/Tiles/PF_BasePlateTile_Dark.prefab",
                Models + "Meshes/SM_BasePlateTile.asset",
                Materials + "M_BasePlate_Grid.mat", Materials + "M_BasePlate_SandDark.mat",
                Materials + "M_BasePlate_SandLight.mat", Materials + "M_BasePlate_Sides.mat"
            };
            const string backup = "Blender/Map/BasePlate/Backups/BeforeSingleSlab/";
            Directory.CreateDirectory(backup);
            foreach (var path in obsolete)
            {
                if (!File.Exists(path)) { continue; }
                foreach (var suffix in new[] { "", ".meta" })
                {
                    var destination = backup + Path.GetFileName(path) + suffix;
                    if (!File.Exists(destination)) { File.Copy(path + suffix, destination); }
                }
                AssetDatabase.DeleteAsset(path);
            }
            if (File.Exists(RequestPath)) { File.Delete(RequestPath); }
            Debug.Log("BasePlate updated: one textured slab, one BoxCollider.");
        }

        /// <summary>Batch authoring only: applies neutral lighting and renders the saved map without Play Mode.</summary>
        public static void ImportAndRender()
        {
            if (!Application.isBatchMode) { throw new InvalidOperationException("This capture is for isolated batch authoring only."); }
            Import();
            const string scenePath = "Assets/_Project/Scenes/Maps/BasePlate.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);
            // Capture helpers are temporary: saved map scenes contain only map content.
            var cameraObject = new GameObject("Temporary BasePlate Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.43f, .76f, .87f);
            camera.fieldOfView = 42;
            camera.farClipPlane = 200;
            camera.transform.position = new Vector3(10, 13, -18);
            camera.transform.LookAt(Vector3.zero);
            var sunObject = new GameObject("Temporary BasePlate Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.5f, .5f, .5f);
            Undo.RecordObject(RenderSettings.sun, "Neutral BasePlate lighting");
            RenderSettings.sun.color = Color.white;
            RenderSettings.sun.intensity = 1.35f;
            if (camera.GetComponent<UniversalAdditionalCameraData>() == null)
            { Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject); }
            var target = new RenderTexture(1400, 1000, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                camera.aspect = 1.4f;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Blender/Map/BasePlate/Previews/BasePlate_Unity.png", pixels.EncodeToPNG());
                Debug.Log("BASEPLATE_RENDERED: saved Unity appearance, no Play Mode.");
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(sunObject);
                RenderSettings.sun = null;
            }
        }
    }
}
