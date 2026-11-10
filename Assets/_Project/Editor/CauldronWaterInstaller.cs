using System;
using System.IO;
using System.Linq;
using System.Text;
using PawArena.Gameplay.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PawArena.Editor
{
    /// <summary>One-shot authoring upgrade, preserving tile instances and unsaved scene edits.</summary>
    public static class CauldronWaterInstaller
    {
        private const string RequestPath = "Logs/CauldronWater.install-request.txt";
        private const string MapPath = "Assets/_Project/Prefabs/Maps/Cauldron/PF_Map_Cauldron.prefab";
        private const string WaterPath = "Assets/_Project/Prefabs/Maps/Cauldron/PF_CauldronWater.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Maps/Cauldron_ScalePreview.unity";
        private const string MaterialFolder = "Assets/_Project/Materials/Maps/Cauldron";
        private const string MeshFolder = "Assets/_Project/Models/Maps/Cauldron/Meshes";
        private const string AudioFolder = "Assets/_Project/Audio/Maps/Cauldron";
        private const string BackupFolder = "Blender/Map/Cauldron/Backups/BeforeInteractiveWater";
        private const float Radius = 18;
        private const float Depth = 14;

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedInstall()
        {
            if (!File.Exists(RequestPath) || Application.isBatchMode) { return; }
            EditorApplication.update -= InstallWhenReady;
            EditorApplication.update += InstallWhenReady;
        }

        private static void InstallWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { return; }
            EditorApplication.update -= InstallWhenReady;
            try { Install(); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                File.WriteAllText("Logs/CauldronWater-install-error.txt", exception.ToString());
            }
        }

        /// <summary>Updates water inside the map prefab without adding player/camera scaffolding to map scenes.</summary>
        [MenuItem("Tools/Paw Arena/Maps/Cauldron/Install Interactive Water")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Stop Play Mode first."); }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && (stage.assetPath == MapPath || stage.assetPath == WaterPath))
            { throw new InvalidOperationException("Close Cauldron Prefab Mode, then use Install Interactive Water again."); }
            if (!File.Exists(MapPath)) { throw new FileNotFoundException("Import Cauldron first."); }
            var waterShader = Shader.Find("PawArena/Cauldron/Water");
            var particleShader = Shader.Find("PawArena/Cauldron/Water Particle");
            if (waterShader == null || particleShader == null || ShaderUtil.ShaderHasError(waterShader) || ShaderUtil.ShaderHasError(particleShader))
            { throw new InvalidOperationException("Cauldron shaders must import successfully before installation."); }

            Directory.CreateDirectory(BackupFolder);
            Backup(MapPath);
            if (File.Exists(ScenePath)) { Backup(ScenePath); }
            EnsureFolder(AudioFolder);
            var waterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPath);
            if (waterPrefab == null) { waterPrefab = CreateWater(waterShader, particleShader); }
            var map = PrefabUtility.LoadPrefabContents(MapPath);
            try
            {
                var vessel = map.transform.Find("Cauldron");
                var oldSurface = vessel == null ? null : vessel.Find("Potion_Surface");
                if (oldSurface == null) { throw new InvalidOperationException("Authored Potion_Surface was not found."); }
                if (vessel.Find("Interactive Water") == null)
                {
                    var bounds = oldSurface.GetComponent<MeshRenderer>().bounds;
                    var water = (GameObject)PrefabUtility.InstantiatePrefab(waterPrefab, vessel);
                    Undo.RegisterCreatedObjectUndo(water, "Install Cauldron water");
                    water.name = "Interactive Water";
                    water.transform.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(water);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(water.transform);
                }
                foreach (Transform child in vessel)
                {
                    if (!child.name.StartsWith("Potion_", StringComparison.Ordinal)) { continue; }
                    Undo.RecordObject(child.gameObject, "Hide static potion decoration");
                    // Retain authored objects for recovery instead of deleting source geometry.
                    child.gameObject.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(map, MapPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(map); }

            var report = "CAULDRON_WATER_INSTALLED\nMap: " + MapPath + "\nWater prefab: " + WaterPath + "\n" +
                "Map scenes remain content-only. No player, HUD, camera or preview session is added.\n" +
                "Gameplay scenes own player water interaction and camera depth/opaque-texture setup.\n" +
                "Original static potion objects retained inactive. Tiles and Main scene unchanged.\n" +
                "No automated or Play Mode tests were run.\n";
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/CauldronWater-install.txt", report);
            if (File.Exists(RequestPath)) { File.Delete(RequestPath); }
            Debug.Log(report);
            SceneView.RepaintAll();
        }

        private static GameObject CreateWater(Shader waterShader, Shader particleShader)
        {
            var waterMaterial = MaterialAsset(MaterialFolder + "/M_Cauldron_Water.mat", waterShader);
            var dropMaterial = MaterialAsset(MaterialFolder + "/M_Cauldron_Splash.mat", particleShader);
            var bubbleMaterial = MaterialAsset(MaterialFolder + "/M_Cauldron_Bubbles.mat", particleShader);
            Undo.RecordObject(bubbleMaterial, "Set bubble material");
            bubbleMaterial.SetFloat("_Ring", 1);
            AssetDatabase.SaveAssetIfDirty(bubbleMaterial);
            var mesh = BuildSurface();
            var volume = BuildVolume();
            var sound = BuildSplashSound();
            var root = new GameObject("PF_CauldronWater");
            Undo.RegisterCreatedObjectUndo(root, "Create water prefab");
            try
            {
                root.layer = LayerMask.NameToLayer("Water");
                var surface = Child(root.transform, "Water Surface");
                Undo.AddComponent<MeshFilter>(surface).sharedMesh = mesh;
                var renderer = Undo.AddComponent<MeshRenderer>(surface);
                renderer.sharedMaterial = waterMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                var trigger = Undo.AddComponent<MeshCollider>(root);
                trigger.sharedMesh = volume;
                trigger.convex = true;
                trigger.isTrigger = true;
                var drops = Particles(root.transform, "Splash Droplets", dropMaterial, 1);
                var bubbles = Particles(root.transform, "Rising Bubbles", bubbleMaterial, 0);
                var water = Undo.AddComponent<CauldronWaterSurface>(root);
                var serialized = new SerializedObject(water);
                serialized.FindProperty("_surface").objectReferenceValue = renderer;
                serialized.FindProperty("_droplets").objectReferenceValue = drops;
                serialized.FindProperty("_bubbles").objectReferenceValue = bubbles;
                serialized.FindProperty("_splashSound").objectReferenceValue = sound;
                serialized.FindProperty("_radius").floatValue = Radius;
                serialized.FindProperty("_depth").floatValue = Depth;
                var voices = serialized.FindProperty("_voices");
                voices.arraySize = 4;
                for (var i = 0; i < voices.arraySize; i++)
                {
                    var source = Undo.AddComponent<AudioSource>(Child(root.transform, "Splash Audio " + (i + 1)));
                    source.playOnAwake = false;
                    source.spatialBlend = 1;
                    source.minDistance = 3;
                    source.maxDistance = 65;
                    source.dopplerLevel = 0;
                    source.rolloffMode = AudioRolloffMode.Linear;
                    voices.GetArrayElementAtIndex(i).objectReferenceValue = source;
                }
                serialized.ApplyModifiedProperties();
                return PrefabUtility.SaveAsPrefabAsset(root, WaterPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Mesh BuildSurface()
        {
            const int rings = 56;
            const int segments = 128;
            var vertices = new Vector3[1 + rings * segments];
            var triangles = new int[(segments + (rings - 1) * segments * 2) * 3];
            var cursor = 0;
            for (var ring = 1; ring <= rings; ring++)
            {
                for (var j = 0; j < segments; j++)
                {
                    var angle = j * Mathf.PI * 2 / segments;
                    var index = 1 + (ring - 1) * segments + j;
                    vertices[index] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (Radius * ring / rings);
                    var next = 1 + (ring - 1) * segments + (j + 1) % segments;
                    if (ring == 1)
                    {
                        triangles[cursor++] = 0; triangles[cursor++] = next; triangles[cursor++] = index;
                    }
                    else
                    {
                        var previous = index - segments;
                        var previousNext = next - segments;
                        triangles[cursor++] = previous; triangles[cursor++] = next; triangles[cursor++] = index;
                        triangles[cursor++] = previous; triangles[cursor++] = previousNext; triangles[cursor++] = next;
                    }
                }
            }
            var mesh = new Mesh { name = "SM_CauldronWaterSurface", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(Radius * 2, 1, Radius * 2));
            return SaveMesh(mesh);
        }

        private static Mesh BuildVolume()
        {
            const int segments = 48;
            var vertices = new Vector3[segments * 2 + 2];
            var triangles = new int[segments * 12];
            vertices[segments * 2] = Vector3.up * .12f;
            vertices[segments * 2 + 1] = Vector3.down * Depth;
            var cursor = 0;
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var point = new Vector3(Mathf.Cos(angle) * Radius, .12f, Mathf.Sin(angle) * Radius);
                vertices[i] = point;
                point.y = -Depth;
                vertices[i + segments] = point;
                var next = (i + 1) % segments;
                triangles[cursor++] = segments * 2; triangles[cursor++] = next; triangles[cursor++] = i;
                triangles[cursor++] = segments * 2 + 1; triangles[cursor++] = i + segments; triangles[cursor++] = next + segments;
                triangles[cursor++] = i; triangles[cursor++] = next; triangles[cursor++] = i + segments;
                triangles[cursor++] = next; triangles[cursor++] = next + segments; triangles[cursor++] = i + segments;
            }
            var mesh = new Mesh { name = "SM_CauldronWaterVolume", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            var path = MeshFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { Object.DestroyImmediate(mesh); return existing; }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static ParticleSystem Particles(Transform parent, string name, Material material, float gravity)
        {
            var system = Undo.AddComponent<ParticleSystem>(Child(parent, name));
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 512;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSpeed = 0;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.9f, .6f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static AudioClip BuildSplashSound()
        {
            var path = AudioFolder + "/Cauldron_Splash.wav";
            if (!File.Exists(path))
            {
                const int sampleRate = 22050;
                const int samples = sampleRate;
                var random = new System.Random(1827);
                var filtered = 0f;
                var phase = 0f;
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                    writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
                    writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                    for (var i = 0; i < samples; i++)
                    {
                        var t = (float)i / sampleRate;
                        var noise = (float)random.NextDouble() * 2 - 1;
                        filtered = Mathf.Lerp(filtered, noise, .17f);
                        phase += Mathf.PI * 2 * Mathf.Lerp(240, 65, Mathf.Clamp01(t * 3)) / sampleRate;
                        var attack = Mathf.Clamp01(t / .006f);
                        var body = Mathf.Sin(phase) * Mathf.Exp(-t * 13) * .4f;
                        var spray = filtered * Mathf.Exp(-t * 5) * 1.8f;
                        var bubbles = Mathf.Sin(t * 850 + Mathf.Sin(t * 55) * 8) * Mathf.Exp(-t * 6) * .07f;
                        writer.Write((short)(Mathf.Clamp((body + spray + bubbles) * attack, -.95f, .95f) * short.MaxValue));
                    }
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static Material MaterialAsset(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) { return material; }
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create water presentation");
            child.layer = LayerMask.NameToLayer("Water");
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void Backup(string path)
        {
            var destination = BackupFolder + "/" + Path.GetFileName(path);
            if (!File.Exists(destination)) { File.Copy(path, destination); }
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
