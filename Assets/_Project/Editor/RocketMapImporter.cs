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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PawArena.Editor
{
    /// <summary>Imports the supported Rocket map without replacing or saving any open user scene.</summary>
    public static class RocketMapImporter
    {
        private const string Source = "Blender/Map/Rocket/Exports/";
        private const string Models = "Assets/_Project/Models/Maps/Rocket/";
        private const string Materials = "Assets/_Project/Materials/Maps/Rocket/";
        private const string Prefabs = "Assets/_Project/Prefabs/Maps/Rocket/";
        private const string ScenePath = "Assets/_Project/Scenes/Maps/Rocket.unity";
        private const string Request = "Logs/Rocket.import-request.txt";
        private static readonly Dictionary<string, Material> Palette = new();

        [InitializeOnLoadMethod]
        private static void Schedule()
        {
            if (!Application.isBatchMode && File.Exists(Request)) { EditorApplication.update += ImportWhenReady; }
        }

        private static void ImportWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetCurrentPrefabStage() != null) { return; }
            EditorApplication.update -= ImportWhenReady;
            // Consume this explicit one-shot request before work, so a domain reload cannot repeat the import.
            if (File.Exists(Request)) { File.Delete(Request); }
            try { Import(); }
            catch (Exception exception)
            {
                File.WriteAllText("Logs/Rocket-import-error.txt", exception.ToString());
                Debug.LogException(exception);
            }
        }

        /// <summary>Creates a fresh Rocket map and component prefabs, preserving all existing scene edits.</summary>
        [MenuItem("Tools/Paw Arena/Maps/Rocket/Import Authored Map")]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            { throw new InvalidOperationException("Exit Play Mode and Prefab Mode before importing Rocket."); }
            if (File.Exists(ScenePath) || File.Exists(Prefabs + "PF_Map_Rocket.prefab"))
            { throw new InvalidOperationException("Rocket already exists; refusing to overwrite user map assets."); }
            foreach (var directory in new[] { Models + "Meshes", Models + "Textures", Materials, Prefabs + "Parts", "Logs", "Assets/_Project/Scenes/Maps" })
            { Directory.CreateDirectory(directory); }
            foreach (var filename in new[] { "Rocket_Map.fbx", "Rocket_Authoring.json", "Rocket_Materials.json", "Rocket_SupportUpdate.json" })
            {
                File.Copy(Source + filename, Models + filename, true);
                AssetDatabase.ImportAsset(Models + filename, ImportAssetOptions.ForceSynchronousImport);
            }
            ConfigureModel();
            CreateMaterials();
            var previous = SceneManager.GetActiveScene();
            // Batch startup has an untitled scene; only that isolated process may replace it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "Rocket_Map.fbx");
                if (model == null) { throw new InvalidOperationException("Rocket FBX did not import."); }
                var root = CopyNode(model.transform, null);
                NormalizeGroup(root.transform);
                root.name = "PF_Map_Rocket";
                var markers = root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Spawn_", StringComparison.Ordinal))
                    .OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
                foreach (var marker in markers) { marker.rotation = Quaternion.LookRotation(Vector3.forward); }
                var spawn = Undo.AddComponent<MapSpawnPoints>(root);
                var data = new SerializedObject(spawn);
                data.Update();
                var points = data.FindProperty("_points");
                points.arraySize = markers.Length;
                for (var i = 0; i < markers.Length; i++) { points.GetArrayElementAtIndex(i).objectReferenceValue = markers[i]; }
                data.ApplyModifiedProperties();

                foreach (var name in new[] { "Rocket_Assembly", "Rear_Service_Tower", "Access_Gantry", "Boarding_Gangway", "Ground" })
                {
                    var part = root.GetComponentsInChildren<Transform>().Single(t => t.name == name);
                    NormalizeGroup(part);
                    SavePart(part, Prefabs + "Parts/PF_" + name + ".prefab");
                }
                Validate(root, markers.Length);
                var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, Prefabs + "PF_Map_Rocket.prefab", InteractionMode.AutomatedAction);
                if (saved == null) { throw new InvalidOperationException("Could not save Rocket map prefab."); }
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) { throw new InvalidOperationException("Could not save Rocket scene."); }
                AssetDatabase.SaveAssets();
                // Capture helpers exist only after the map-only scene has been saved, then are destroyed.
                RenderPresentation();
                var report = $"ROCKET_IMPORTED\nScene: {ScenePath}\nPrefab: {Prefabs}PF_Map_Rocket.prefab\n" +
                    $"Root count: {scene.rootCount}\nMeshes: {root.GetComponentsInChildren<MeshFilter>().Length}\n" +
                    $"Colliders: {root.GetComponentsInChildren<Collider>().Length}\nSpawns: {markers.Length}\n" +
                    "Six rear hinge pivots, three seat anchors, four ramps and supported cabin retained.\n" +
                    "Five nested part prefabs; all transform scales are 1.\n" +
                    "No camera/light/HUD/player in saved scene. No launch, seating interaction, joints or Rigidbody simulation.\n" +
                    "No tests or Play Mode run; existing open scenes preserved, not saved or closed.\n";
                File.WriteAllText("Logs/Rocket-import.txt", report);
                Debug.Log(report);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) { SceneManager.SetActiveScene(previous); }
                if (!Application.isBatchMode) { EditorSceneManager.CloseScene(scene, true); }
            }
        }

        /// <summary>Applies only the three-sided cabin revision to the existing vehicle prefab.</summary>
        [MenuItem("Tools/Paw Arena/Maps/Rocket/Update Open Cabin")]
        public static void UpdateOpenCabin() => ApplyCabinRevision(false);

        /// <summary>Updates the corner-framed cabin and its direct gantry approach in place.</summary>
        [MenuItem("Tools/Paw Arena/Maps/Rocket/Update Cage Cabin")]
        public static void UpdateCageCabin() => ApplyCabinRevision(true);

        /// <summary>Updates the continuous walking route without replacing the scene or vehicle.</summary>
        [MenuItem("Tools/Paw Arena/Maps/Rocket/Update Connected Walkway")]
        public static void UpdateWalkway() => ApplyCabinRevision(true, true);

        private static void ApplyCabinRevision(bool cage, bool route = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            { throw new InvalidOperationException("Exit Play Mode and Prefab Mode first."); }
            var reportFile = route ? "Rocket_Route.json" : cage ? "Rocket_CageCabin.json" : "Rocket_OpenCabin.json";
            var changes = JsonUtility.FromJson<CabinRevision>(File.ReadAllText(Source + reportFile));
            var groups = cage ? changes.prefab_groups : new[] { "Rocket_Assembly" };
            var backup = "Blender/Map/Rocket/Backups/UnityBeforeCabin_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup);
            var backupAssets = groups.SelectMany(g => new[] { Prefabs + "Parts/PF_" + g + ".prefab", Prefabs + "Parts/PF_" + g + ".prefab.meta" })
                .Concat(new[] { Models + "Rocket_Map.fbx", Models + "Rocket_Map.fbx.meta" });
            foreach (var asset in backupAssets)
            { File.Copy(asset, backup + "/" + Path.GetFileName(asset)); }
            foreach (var file in new[] { "Rocket_Map.fbx", "Rocket_Authoring.json", reportFile })
            {
                File.Copy(Source + file, Models + file, true);
                AssetDatabase.ImportAsset(Models + file, ImportAssetOptions.ForceSynchronousImport);
            }
            Palette.Clear();
            foreach (var file in Directory.GetFiles(Materials, "*.mat"))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(file.Replace('\\', '/'));
                Palette.Add(material.name, material);
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "Rocket_Map.fbx");
            foreach (var group in groups)
            {
            var path = Prefabs + "Parts/PF_" + group + ".prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var name in changes.removed_names)
                {
                    var target = contents.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == name);
                    if (target != null) { Undo.DestroyObjectImmediate(target.gameObject); }
                }
                foreach (var name in changes.added_names)
                {
                    if (contents.GetComponentsInChildren<Transform>(true).Any(t => t.name == name)) { continue; }
                    var source = model.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
                    var parent = source.parent.name == group ? contents.transform :
                        contents.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == source.parent.name);
                    if (parent != null) { CopyNode(source, parent); }
                }
                foreach (var name in changes.changed_names ?? Array.Empty<string>())
                {
                    var target = contents.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == name);
                    if (target == null) { continue; }
                    var source = model.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
                    Undo.RecordObject(target, "Align Rocket seats and entry");
                    target.SetPositionAndRotation(source.position, source.rotation);
                    EditorUtility.SetDirty(target);
                }
                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                { throw new InvalidOperationException("Failed to save the updated Rocket assembly."); }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            AssetDatabase.SaveAssets();
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root = scene.GetRootGameObjects().Single();
                Validate(root, 8);
                if (root.GetComponentsInChildren<Transform>(true).Any(t => changes.removed_names.Contains(t.name)))
                { throw new InvalidOperationException("A cabin obstruction remains in the map."); }
                if (cage && !route)
                {
                    var deck = root.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Boarding_StraightDeck").bounds;
                    var threshold = root.GetComponentsInChildren<Transform>().Single(t => t.name == "BoardingThreshold");
                    if (Mathf.Abs(deck.size.z - 3.2f) > .01f || Mathf.Abs(deck.max.y - threshold.position.y) > .01f)
                    { throw new InvalidOperationException("Boarding bridge width or floor alignment changed."); }
                }
                var routeReport = route ? ValidateWalkRoute(root) : "";
                RenderPresentation(true, cage, route);
                File.WriteAllText(route ? "Logs/Rocket-walkway.txt" : cage ? "Logs/Rocket-cage-cabin.txt" : "Logs/Rocket-open-cabin.txt", "ROCKET_UPDATED\n" +
                    routeReport +
                    (cage ? "Corner-framed open cabin; 3.2m-wide direct entry and three seats against the rear wall.\n" : "Three-sided open cabin.\n") +
                    $"Meshes: {root.GetComponentsInChildren<MeshFilter>().Length}; Colliders: {root.GetComponentsInChildren<Collider>().Length}\n" +
                    "Preserved existing scene, prefab GUIDs, seats, hinges and unit scales. No Play Mode or tests.\nBackup: " + backup);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) { SceneManager.SetActiveScene(previous); }
                if (!Application.isBatchMode) { EditorSceneManager.CloseScene(scene, true); }
            }
        }

        private static string ValidateWalkRoute(GameObject root)
        {
            Physics.SyncTransforms();
            var colliders = root.GetComponentsInChildren<Collider>();
            var samples = new List<Vector3>();
            // Blender coordinates are converted to the imported Unity world axes.
            Vector3 World(float x, float y, float z) => new(x, z, y);
            for (var flight = 1; flight <= 4; flight++)
            {
                var x = flight % 2 == 1 ? -8.25f : -4.75f;
                var y0 = flight % 2 == 1 ? -5.5f : 4.5f;
                var y1 = flight == 4 ? -1.95f : flight % 2 == 1 ? 4.5f : -5.5f;
                for (var i = 0; i <= 24; i++)
                {
                    var t = i / 24f;
                    foreach (var offset in new[] { -.8f, 0, .8f })
                    { samples.Add(World(x + offset, Mathf.Lerp(y0, y1, t), (flight - 1 + t) * 3.4f)); }
                }
                if (flight < 4)
                {
                    var nextX = flight % 2 == 1 ? -4.75f : -8.25f;
                    var landY = y1 > 0 ? 5.1f : -6.1f;
                    for (var i = 0; i <= 12; i++) { samples.Add(World(Mathf.Lerp(x, nextX, i / 12f), landY, flight * 3.4f)); }
                }
            }
            for (var i = 0; i <= 15; i++)
            {
                samples.Add(World(-4.75f, Mathf.Lerp(-1.95f, -3.55f, i / 15f), 13.6f));
                foreach (var offset in new[] { -.8f, 0, .8f })
                { samples.Add(World(Mathf.Lerp(-4.75f, .3f, i / 15f), -3.55f + offset, 13.6f)); }
            }
            foreach (var point in samples)
            {
                var floorRay = new Ray(point + Vector3.up * .35f, Vector3.down);
                var nearest = float.PositiveInfinity;
                foreach (var collider in colliders)
                { if (collider.Raycast(floorRay, out var hit, .8f)) { nearest = Mathf.Min(nearest, hit.distance); } }
                if (Mathf.Abs(nearest - .35f) > .035f)
                { throw new InvalidOperationException("Walk surface gap/step at " + point + ": distance " + nearest); }
                var clearance = new Ray(point + Vector3.up * .08f, Vector3.up);
                foreach (var collider in colliders)
                {
                    if (collider.Raycast(clearance, out var hit, 1.92f))
                    { throw new InvalidOperationException("Walk route blocked by " + collider.name + " at " + hit.point); }
                }
            }
            return $"Connected route: {samples.Count} floor and 2m headroom samples passed; no Play Mode or test assets.\n";
        }

        private static void ConfigureModel()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Models + "Rocket_Map.fbx");
            Undo.RecordObject(importer, "Configure Rocket FBX");
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }

        private static void CreateMaterials()
        {
            Palette.Clear();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { throw new InvalidOperationException("URP Lit is missing."); }
            var grass = CreateGrassTexture();
            var values = JsonUtility.FromJson<MaterialList>(File.ReadAllText(Models + "Rocket_Materials.json"));
            foreach (var entry in values.materials)
            {
                if (!ColorUtility.TryParseHtmlString("#" + entry.hexColor, out var color))
                { throw new InvalidOperationException("Invalid material colour: " + entry.name); }
                var path = Materials + entry.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var isNew = material == null;
                if (isNew) { material = new Material(shader) { name = entry.name }; }
                else { Undo.RecordObject(material, "Finish Rocket material import"); }
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", entry.metallic);
                material.SetFloat("_Smoothness", entry.smoothness);
                if (entry.name == "M_Rocket_Grass")
                {
                    material.SetTexture("_BaseMap", grass);
                    material.SetColor("_BaseColor", Color.white);
                }
                if (entry.emission > 0)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color.linear * entry.emission);
                }
                if (entry.name == "M_Rocket_Window")
                {
                    color.a = .36f;
                    material.SetColor("_BaseColor", color);
                    material.SetFloat("_Surface", 1);
                    material.SetFloat("_Blend", 0);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                }
                if (isNew) { AssetDatabase.CreateAsset(material, path); }
                else { EditorUtility.SetDirty(material); }
                Palette.Add(material.name, material);
            }
        }

        private static Texture2D CreateGrassTexture()
        {
            const int size = 576;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    pixels[y * size + x] = ((x/16 + y/16) & 1) == 0 ?
                        new Color32(103, 141, 32, 255) : new Color32(116, 153, 37, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            var path = Models + "Textures/Rocket_Grass_Albedo.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Undo.RecordObject(importer, "Configure Rocket grass texture");
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static GameObject CopyNode(Transform source, Transform parent)
        {
            var obj = new GameObject(source.name);
            Undo.RegisterCreatedObjectUndo(obj, "Import Rocket geometry");
            obj.transform.SetParent(parent, false);
            obj.transform.SetPositionAndRotation(source.position, source.rotation);
            obj.transform.localScale = Vector3.one;
            var filter = source.GetComponent<MeshFilter>();
            if (filter != null)
            {
                var mesh = BakeMesh(filter);
                Undo.AddComponent<MeshFilter>(obj).sharedMesh = mesh;
                var renderer = Undo.AddComponent<MeshRenderer>(obj);
                renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials.Select(m =>
                    m != null && Palette.TryGetValue(m.name, out var mapped) ? mapped :
                    throw new InvalidOperationException("Unknown Rocket material: " + m?.name)).ToArray();
                if (source.name == "Ground_GrassSurface")
                {
                    mesh.uv = mesh.vertices.Select(v =>
                    {
                        var world = source.position + source.rotation * v;
                        return new Vector2((world.x+13)/26, (world.z+12)/24);
                    }).ToArray();
                    mesh.RecalculateTangents();
                    EditorUtility.SetDirty(mesh);
                }
                if (!source.name.Contains("Beacon") && !source.name.Contains("Arrow") && !source.name.Contains("Face_") &&
                    !source.name.Contains("Trim_") && !source.name.Contains("EdgeCap_") && !source.name.Contains("HingePin"))
                {
                    obj.layer = LayerMask.NameToLayer("Ground");
                    if (source.name.StartsWith("Ground_", StringComparison.Ordinal))
                    {
                        var collider = Undo.AddComponent<BoxCollider>(obj);
                        collider.center = mesh.bounds.center;
                        collider.size = mesh.bounds.size;
                    }
                    else
                    {
                        // Concave static geometry preserves openings; no giant collider seals the cabin or gantry.
                        Undo.AddComponent<MeshCollider>(obj).sharedMesh = mesh;
                    }
                }
            }
            foreach (Transform child in source) { CopyNode(child, obj.transform); }
            return obj;
        }

        private static Mesh BakeMesh(MeshFilter source)
        {
            var mesh = Object.Instantiate(source.sharedMesh);
            mesh.name = "SM_Rocket_" + source.name;
            var matrix = Matrix4x4.Rotate(Quaternion.Inverse(source.transform.rotation)) * source.transform.localToWorldMatrix;
            var normalMatrix = matrix.inverse.transpose;
            var vertices = mesh.vertices.Select(matrix.MultiplyVector).ToArray();
            mesh.vertices = vertices;
            mesh.normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var indices = mesh.GetTriangles(submesh);
                var clean = new List<int>(indices.Length);
                for (var i = 0; i < indices.Length; i += 3)
                {
                    var a = indices[i];
                    var b = indices[i+1];
                    var c = indices[i+2];
                    if (Vector3.Cross(vertices[b]-vertices[a], vertices[c]-vertices[a]).sqrMagnitude < 1e-12f) { continue; }
                    clean.Add(a);
                    clean.Add(matrix.determinant < 0 ? c : b);
                    clean.Add(matrix.determinant < 0 ? b : c);
                }
                mesh.SetTriangles(clean, submesh);
            }
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Models + "Meshes/" + mesh.name + ".asset");
            return mesh;
        }

        private static void NormalizeGroup(Transform group)
        {
            var poses = group.GetComponentsInChildren<Transform>().Where(t => t != group)
                .Select(t => new { Transform = t, Position = t.position, Rotation = t.rotation }).ToArray();
            group.rotation = Quaternion.identity;
            group.localScale = Vector3.one;
            foreach (var pose in poses) { pose.Transform.SetPositionAndRotation(pose.Position, pose.Rotation); }
        }

        private static void SavePart(Transform part, string path)
        {
            var parent = part.parent;
            var position = part.position;
            var rotation = part.rotation;
            part.SetParent(null, true);
            part.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(part.gameObject, path, InteractionMode.AutomatedAction);
            part.SetPositionAndRotation(position, rotation);
            part.SetParent(parent, true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(part);
            if (saved == null) { throw new InvalidOperationException("Could not save part: " + path); }
        }

        private static void Validate(GameObject root, int spawns)
        {
            var transforms = root.GetComponentsInChildren<Transform>();
            if (spawns != 8 || transforms.Count(t => t.name.StartsWith("Rear_Hinge_", StringComparison.Ordinal)) != 6 ||
                transforms.Count(t => t.name.StartsWith("SeatAnchor_", StringComparison.Ordinal)) != 3 ||
                transforms.Count(t => t.name.StartsWith("WalkRamp_", StringComparison.Ordinal)) != 4)
            { throw new InvalidOperationException("Missing Rocket markers, hinges or ramps."); }
            if (transforms.Any(t => (t.localScale-Vector3.one).sqrMagnitude > .000001f))
            { throw new InvalidOperationException("Rocket contains non-unit transform scales."); }
            var ground = root.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Ground_GrassSurface").bounds;
            if (Mathf.Abs(ground.size.x-26) > .01f || Mathf.Abs(ground.size.z-24) > .01f || Mathf.Abs(ground.max.y) > .01f)
            { throw new InvalidOperationException("Incorrect Rocket units or axes: " + ground); }
            if (root.GetComponentsInChildren<Camera>().Length + root.GetComponentsInChildren<Light>().Length + root.GetComponentsInChildren<Canvas>().Length != 0)
            { throw new InvalidOperationException("Map-only prefab must not contain presentation components."); }
            var boarding = transforms.Single(t => t.name == "BoardingThreshold");
            if (Mathf.Abs(boarding.position.y-13.6f) > .01f) { throw new InvalidOperationException("Cabin floor height changed during import."); }
        }

        private static void RenderPresentation(bool cabinDetail = false, bool cage = false, bool route = false)
        {
            var cameraObject = new GameObject("Temporary Rocket Capture");
            var sunObject = new GameObject("Temporary Rocket Light");
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = SceneManager.GetActiveScene();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.43f, .76f, .87f);
            camera.orthographic = true;
            camera.orthographicSize = 21.5f;
            camera.transform.position = new Vector3(39, 34, 53);
            camera.transform.LookAt(new Vector3(0, 11, 0));
            if (cabinDetail)
            {
                camera.orthographicSize = 5.8f;
                camera.transform.position = new Vector3(-13, 21, 24);
                camera.transform.LookAt(new Vector3(-3, 15, 3.6f));
            }
            if (cage)
            {
                camera.orthographicSize = 6.5f;
                camera.transform.position = new Vector3(-13, 23, -9);
                camera.transform.LookAt(new Vector3(1, 15.5f, -3.55f));
            }
            if (route)
            {
                camera.orthographicSize = 8;
                camera.transform.position = new Vector3(-18, 25, -18);
                camera.transform.LookAt(new Vector3(-3.5f, 13, -1.5f));
            }
            camera.farClipPlane = 200;
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40, -30, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.5f, .5f, .5f);
            RenderSettings.skybox = null;
            var target = new RenderTexture(1400, 1400, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(1400, 1400, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                target.Create();
                camera.aspect = 1;
                for (var i = 0; i < 3; i++)
                { RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target }); }
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1400, 1400), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Blender/Map/Rocket/Previews/" +
                    (route ? "Rocket_Walkway_Unity.png" : cage ? "Rocket_CageCabin_Unity.png" : cabinDetail ? "Rocket_OpenCabin_Unity.png" : "Rocket_Unity.png"), pixels.EncodeToPNG());
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

        // Transfer-only records read by JsonUtility; not Inspector configuration.
        [Serializable]
        private sealed class CabinRevision
        {
            public string[] removed_names;
            public string[] added_names;
            public string[] changed_names;
            public string[] prefab_groups;
        }
        [Serializable]
        private sealed class MaterialList { public MaterialEntry[] materials; }
        [Serializable]
        private sealed class MaterialEntry
        {
            public string name;
            public string hexColor;
            public float metallic;
            public float smoothness;
            public float emission;
        }
    }
}
