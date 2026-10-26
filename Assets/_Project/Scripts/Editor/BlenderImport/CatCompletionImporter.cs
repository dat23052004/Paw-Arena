using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PawArena.Core.Inventory;
using PawArena.Gameplay.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PawArena.Editor.BlenderImport
{
    /// <summary>Additively imports the deferred 18 clips and samples the approved cat without saving user scenes.</summary>
    public static class CatCompletionImporter
    {
        private const string Source = "Blender/Characters/Exports/CatCompletion/";
        private const string Destination = "Assets/_Project/Animations/Characters/Cat/Completion/";
        private const string Model = "Assets/_Project/Models/Characters/Cat/SK_PawArena_Cat.fbx";
        private const string Prefab = "Assets/_Project/Prefabs/Characters/Cat/PF_Cat_Base.prefab";
        private const string Request = "Library/CatCompletionImport.request";

        [Serializable] private sealed class Manifest { public int fps; public Entry[] clips; }
        [Serializable] private sealed class Entry { public string name; public int frames; public bool loop; public int representative; }
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string error;
            public string unityVersion;
            public int existingClipCount;
            public int totalClipCount;
            public bool basePrefabUnchanged;
            public List<ClipReport> clips = new();
        }
        [Serializable] private sealed class ClipReport
        {
            public string name;
            public float seconds;
            public bool loop;
            public float loopSeamMetres;
            public float maxBoneRotationDegrees;
            public float minimumBodyY = float.MaxValue;
            public float startHeight;
            public float endHeight;
            public int sampledPoses;
            public int gearChecked;
        }

        [InitializeOnLoadMethod]
        private static void Schedule() => EditorApplication.delayCall += RunRequest;

        private static void RunRequest()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "import") { return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += RunRequest; return; }
            File.WriteAllText(Request, "running");
            try { Import(); File.WriteAllText(Request, "complete"); }
            catch (Exception exception) { File.WriteAllText(Request, "failed: " + exception.Message); Debug.LogException(exception); }
        }

        /// <summary>Imports only completion clips, preserving model, existing clips, inventory and open scenes.</summary>
        [MenuItem("Tools/Paw Arena/Animations/Import Remaining 18 Clips")]
        public static void Import()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before importing clips.");
            var report = new Report { unityVersion = Application.unityVersion };
            var prefabBytes = File.ReadAllBytes(Prefab);
            try
            {
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Source + "Clip_Manifest.json"));
                Require(manifest.fps == 30 && manifest.clips.Length == 18, "Expected 18 clips at 30 fps.");
                var existingPaths = Directory.GetFiles("Assets/_Project/Animations/Characters/Cat", "AN_Cat_*.fbx", SearchOption.AllDirectories)
                    .Where(path => !path.Replace('\\', '/').StartsWith(Destination, StringComparison.Ordinal)).ToArray();
                var existingHashes = existingPaths.ToDictionary(path => path, path => Hash(path));
                report.existingClipCount = existingPaths.Length;
                var avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().Single();
                Require(avatar.isValid && avatar.isHuman, "The approved base Humanoid Avatar is invalid.");
                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/_Project/Animations/Characters/Cat/Cat_AllBones.mask");
                Require(mask != null, "All-bones mask missing.");
                EnsureFolder(Destination.TrimEnd('/'));
                var clips = new List<AnimationClip>();
                foreach (var entry in manifest.clips)
                {
                    var filename = "AN_Cat_" + entry.name + ".fbx";
                    var path = Destination + filename;
                    if (!File.Exists(path)) { File.Copy(Source + filename, path, false); }
                    Require(Hash(path) == Hash(Source + filename), "Existing clip differs; refusing to overwrite: " + path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    Undo.RecordObject(importer, "Configure completed cat animation");
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = avatar;
                    importer.importAnimation = true;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.optimizeGameObjects = false;
                    importer.preserveHierarchy = true;
                    importer.resampleCurves = true;
                    importer.animationCompression = ModelImporterAnimationCompression.Off;
                    importer.SaveAndReimport();
                    var take = importer.defaultClipAnimations.Single();
                    take.name = "Cat_" + entry.name;
                    take.lastFrame = take.firstFrame + entry.frames;
                    take.loopTime = entry.loop;
                    take.loopPose = false;
                    take.lockRootRotation = take.lockRootPositionXZ = take.lockRootHeightY = true;
                    take.keepOriginalOrientation = take.keepOriginalPositionXZ = take.keepOriginalPositionY = true;
                    take.heightFromFeet = false;
                    take.maskType = ClipAnimationMaskType.CopyFromOther;
                    take.maskSource = mask;
                    take.ConfigureClipFromMask(mask);
                    importer.clipAnimations = new[] { take };
                    importer.SaveAndReimport();
                    var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__"));
                    Require(clip.isHumanMotion && Mathf.Abs(clip.length - entry.frames / 30f) < .002f, "Invalid clip: " + entry.name);
                    var curves = AnimationUtility.GetCurveBindings(clip);
                    Require(curves.Any(c => c.path.Contains("Tail_")) && curves.Any(c => c.path.Contains("LeftEar")), "Missing ear/tail curves: " + entry.name);
                    clips.Add(clip);
                }
                Validate(manifest.clips, clips, report);
                foreach (var pair in existingHashes) { Require(Hash(pair.Key) == pair.Value, "Original clip changed: " + pair.Key); }
                report.basePrefabUnchanged = prefabBytes.SequenceEqual(File.ReadAllBytes(Prefab));
                Require(report.basePrefabUnchanged, "Base prefab was modified.");
                report.totalClipCount = Directory.GetFiles("Assets/_Project/Animations/Characters/Cat", "AN_Cat_*.fbx", SearchOption.AllDirectories).Length;
                Require(report.totalClipCount == 44, "Expected 44 total clips.");
                report.passed = true;
                Debug.Log("[Cat Completion] PASS: 18 new Humanoid clips, 44 total; base and existing clips unchanged.");
            }
            catch (Exception exception) { report.error = exception.ToString(); throw; }
            finally { File.WriteAllText(Source + "Unity_Validation.json", JsonUtility.ToJson(report, true)); }
        }

        private static void Validate(Entry[] entries, List<AnimationClip> clips, Report report)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var baked = new Mesh();
            try
            {
                var cat = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), scene);
                var animator = cat.GetComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
                var bones = skin.bones;
                var gearLibrary = AssetDatabase.LoadAssetAtPath<CatGearLibrary>("Assets/_Project/ScriptableObjects/Inventory/CatGear/CatGearLibrary.asset");
                var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/_Project/ScriptableObjects/Inventory/CatSkins/CatSkinCatalog.asset");
                var inventory = new InventoryService(catalog);
                foreach (var gear in gearLibrary.Gear) { inventory.TryGrant(gear.ItemId); }
                cat.AddComponent<CatGearController>().Initialize(gearLibrary, inventory);
                for (var index = 0; index < clips.Count; index++)
                {
                    var clip = clips[index];
                    var entry = entries[index];
                    var result = new ClipReport { name = clip.name, seconds = clip.length, loop = entry.loop };
                    var graph = PlayableGraph.Create("Cat completion verification");
                    try
                    {
                        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        var playable = AnimationClipPlayable.Create(graph, clip);
                        playable.SetApplyFootIK(false);
                        playable.SetApplyPlayableIK(false);
                        AnimationPlayableOutput.Create(graph, "Cat", animator).SetSourcePlayable(playable);
                        graph.Play();
                        Vector3[] startPositions = null;
                        Quaternion[] startRotations = null;
                        for (var sample = 0; sample <= entry.frames; sample++)
                        {
                            var time = Mathf.Min(sample / 30f, clip.length - (entry.loop ? .00001f : 0));
                            playable.SetTime(time);
                            graph.Evaluate(0);
                            var bounds = CheckMesh(skin, baked);
                            result.minimumBodyY = Mathf.Min(result.minimumBodyY, bounds.min.y);
                            Require(cat.transform.position.sqrMagnitude < 1e-6f, "Root travel: " + entry.name);
                            if (sample == 0)
                            {
                                startPositions = bones.Select(b => b.position).ToArray();
                                startRotations = bones.Select(b => b.rotation).ToArray();
                                result.startHeight = bounds.size.y;
                            }
                            for (var i = 0; i < bones.Length; i++)
                            { result.maxBoneRotationDegrees = Mathf.Max(result.maxBoneRotationDegrees, Quaternion.Angle(startRotations[i], bones[i].rotation)); }
                            result.sampledPoses++;
                            if (sample == entry.frames)
                            {
                                result.endHeight = bounds.size.y;
                                result.loopSeamMetres = bones.Select((b, i) => Vector3.Distance(startPositions[i], b.position)).Max();
                            }
                        }
                        Require(result.maxBoneRotationDegrees > .05f, "Static clip: " + entry.name);
                        if (entry.loop) { Require(result.loopSeamMetres < .005f, "Loop seam: " + entry.name); }
                        if (entry.name.StartsWith("GetUp_"))
                        { Require(result.startHeight < 1.5f && result.endHeight > 1.9f, "Recovery pose lost: " + entry.name); }
                        foreach (var gear in gearLibrary.Gear)
                        {
                            var slot = gear.BuildDescriptor().AllowedSlots[0];
                            inventory.TryEquip(gear.ItemId, slot);
                            var renderer = cat.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name == "EquippedGear_" + gear.ItemId);
                            foreach (var phase in new[] { 0f, .25f, .5f, .75f, 1f })
                            {
                                playable.SetTime(Mathf.Min(clip.length * phase, clip.length - .00001f));
                                graph.Evaluate(0);
                                CheckMesh(renderer, baked);
                            }
                            inventory.TryUnequip(slot);
                            result.gearChecked++;
                        }
                        report.clips.Add(result);
                    }
                    finally { graph.Destroy(); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Bounds CheckMesh(SkinnedMeshRenderer renderer, Mesh baked)
        {
            // Match the imported FBX's scaled renderer space before TransformPoint.
            renderer.BakeMesh(baked, true);
            var vertices = baked.vertices;
            Require(vertices.Length > 0, "Empty mesh: " + renderer.name);
            var bounds = new Bounds(renderer.transform.TransformPoint(vertices[0]), Vector3.zero);
            foreach (var vertex in vertices)
            {
                Require(float.IsFinite(vertex.x) && float.IsFinite(vertex.y) && float.IsFinite(vertex.z), "Non-finite mesh: " + renderer.name);
                bounds.Encapsulate(renderer.transform.TransformPoint(vertex));
            }
            Require(bounds.size.magnitude < 8, "Exploded mesh: " + renderer.name + " size=" + bounds.size.ToString("R") + " scale=" + renderer.transform.lossyScale.ToString("R") + " baked=" + baked.bounds.size.ToString("R"));
            return bounds;
        }

        private static string Hash(string path)
        {
            using var hash = System.Security.Cryptography.SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Require(bool condition, string message)
        { if (!condition) { throw new InvalidOperationException(message); } }
    }
}
