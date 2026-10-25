using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace PawArena.Editor.BlenderImport
{
    /// <summary>Creates material-only cat skin variants without modifying the base or open scenes.</summary>
    public static class PawArenaCatSkinImporter
    {
        private const string BasePath = "Assets/_Project/Prefabs/Characters/Cat/PF_Cat_Base.prefab";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Characters/Cat/Skins";
        private const string MaterialFolder = "Assets/_Project/Materials/Characters/Cat/Skins";
        private const string TextureFolder = "Assets/_Project/Textures/Characters/Cat/Skins";
        private const string ManifestPath = "Blender/Characters/Exports/CatSkins/Skin_Manifest.json";
        private const string RequestPath = "Library/PawArenaCatSkinsImport.request";
        private const string ReportPath = "Blender/Validation/UnityCatSkinsImport.json";
        private const string ShaderName = "Universal Render Pipeline/Complex Lit";

        [InitializeOnLoadMethod]
        private static void ScheduleRequest() => EditorApplication.delayCall += RunRequest;

        private static void RunRequest()
        {
            if (!File.Exists(RequestPath) || File.ReadAllText(RequestPath).Trim() != "import-19-cat-skins") { return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunRequest;
                return;
            }
            File.WriteAllText(RequestPath, "running");
            try { ImportAll(); File.WriteAllText(RequestPath, "complete"); }
            catch (Exception exception)
            {
                File.WriteAllText(RequestPath, "failed - " + exception.Message);
                Debug.LogException(exception);
            }
        }

        /// <summary>Imports the nineteen Blender surface variants once; existing outputs are never overwritten.</summary>
        [MenuItem("Tools/Paw Arena/Import 19 Cat Skins")]
        public static void ImportAll()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode first.");
            var report = new ImportReport();
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
                Require(manifest.passed && manifest.skins.Length == 19, "Expected the audited nineteen-skin manifest.");
                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
                Require(basePrefab != null, "Approved base prefab missing.");
                var baseSkin = basePrefab.GetComponentInChildren<SkinnedMeshRenderer>();
                var baseAnimator = basePrefab.GetComponent<Animator>();
                Require(baseSkin != null && baseSkin.sharedMaterials.Length == 2, "Expected Coat/Eyes slots.");
                Require(baseAnimator.avatar.isValid && baseAnimator.avatar.isHuman, "Base Avatar is invalid.");
                var shader = Shader.Find(ShaderName);
                Require(shader != null && shader.isSupported, "URP Complex Lit is unavailable on this device.");
                foreach (var skin in manifest.skins)
                {
                    var slug = skin.name.Replace(' ', '_');
                    Require(File.Exists(TextureFolder + "/" + skin.texture), "Missing texture: " + skin.name);
                    Require(!File.Exists(PrefabFolder + "/PF_Cat_" + slug + ".prefab"), "Skin already exists; not overwriting: " + skin.name);
                    foreach (var suffix in new[] { "Coat", "Eyes" })
                    { Require(!File.Exists(MaterialFolder + "/M_Cat_" + slug + "_" + suffix + ".mat"), "Material already exists: " + skin.name); }
                }
                var protectedPaths = AssetDatabase.GetDependencies(BasePath, true)
                    .Where(File.Exists).Concat(Directory.GetFiles("Assets/_Project/Animations/Characters/Cat", "*", SearchOption.AllDirectories)).Distinct().ToArray();
                var hashes = protectedPaths.ToDictionary(p => p, HashFile);
                var sceneState = OpenSceneState();
                EnsureFolder(PrefabFolder);
                EnsureFolder(MaterialFolder);
                foreach (var skin in manifest.skins)
                {
                    var slug = skin.name.Replace(' ', '_');
                    var texturePath = TextureFolder + "/" + skin.texture;
                    var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                    Undo.RecordObject(importer, "Configure cat skin texture");
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.maxTextureSize = skin.size[0];
                    importer.mipmapEnabled = true;
                    importer.isReadable = false;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.SaveAndReimport();
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    Require(texture.width == skin.size[0], "Texture resolution changed unexpectedly.");
                    var materials = new[] { CreateMaterial(slug, texture, skin.settings, shader, false), CreateMaterial(slug, texture, skin.settings, shader, true) };
                    var path = PrefabFolder + "/PF_Cat_" + slug + ".prefab";
                    var scene = EditorSceneManager.NewPreviewScene();
                    try
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, scene);
                        instance.name = "PF_Cat_" + slug;
                        var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                        renderer.sharedMaterials = materials;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
                        Require(prefab != null && PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant, "Not a prefab variant: " + skin.name);
                        var imported = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
                        Require(imported.sharedMesh == baseSkin.sharedMesh, "Shared mesh changed.");
                        Require(imported.localBounds == baseSkin.localBounds, "Culling bounds changed.");
                        Require(imported.sharedMaterials.SequenceEqual(materials), "Material override was not saved.");
                        Require(prefab.GetComponent<Animator>().avatar == baseAnimator.avatar, "Shared Avatar changed.");
                        Require(prefab.GetComponent<Animator>().runtimeAnimatorController == baseAnimator.runtimeAnimatorController, "Animator Controller changed.");
                        Require(Structure(prefab) == Structure(basePrefab), "Rig or components changed.");
                        ValidatePlayback(instance);
                        report.skins.Add(new SkinResult { name = skin.name, prefab = path, sharedMeshAndAvatar = true, animationPlayback = true });
                    }
                    finally { EditorSceneManager.ClosePreviewScene(scene); }
                }
                AssetDatabase.SaveAssets();
                Require(hashes.All(p => HashFile(p.Key) == p.Value), "A protected base/animation asset changed.");
                Require(OpenSceneState() == sceneState, "Open scene state changed.");
                report.passed = true;
                report.baseAndAnimationsUnchanged = true;
                report.openScenesUnchanged = true;
                report.materialCount = 38;
                report.textureCount = 19;
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/PF_Cat_Calico.prefab");
                EditorGUIUtility.PingObject(Selection.activeObject);
                Debug.Log("[Paw Arena Skins] PASS: 19 material-only prefab variants, 38 materials, 19 textures. Shared mesh/Avatar and playback verified. Base, animations and open scenes unchanged.");
            }
            catch (Exception exception) { report.error = exception.ToString(); throw; }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            }
        }

        private static Material CreateMaterial(string slug, Texture2D texture, Settings settings, Shader shader, bool eye)
        {
            var material = new Material(shader) { name = "M_Cat_" + slug + (eye ? "_Eyes" : "_Coat") };
            Undo.RegisterCreatedObjectUndo(material, "Create cat skin material");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", settings.metallic);
            material.SetFloat("_Smoothness", 1f - (eye ? .25f : settings.roughness));
            material.SetFloat("_ClearCoat", 1f);
            material.SetFloat("_ClearCoatMask", settings.coat);
            material.SetFloat("_ClearCoatSmoothness", .97f);
            material.EnableKeyword("_CLEARCOAT");
            material.DisableKeyword("_CLEARCOATMAP");
            if (settings.emission > 0f)
            {
                material.SetTexture("_EmissionMap", texture);
                material.SetColor("_EmissionColor", Color.white * settings.emission);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            material.SetFloat("_Surface", 0f);
            material.renderQueue = 2000;
            AssetDatabase.CreateAsset(material, MaterialFolder + "/" + material.name + ".mat");
            return material;
        }

        private static void ValidatePlayback(GameObject instance)
        {
            var animator = instance.GetComponent<Animator>();
            var previous = animator.cullingMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Animations/Characters/Cat/Base/AN_Cat_Run.fbx")
                .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            var graph = PlayableGraph.Create("Validate cat skin playback");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(graph, "Cat", animator).SetSourcePlayable(playable);
                graph.Play(); playable.SetTime(0); graph.Evaluate(0);
                var bones = instance.GetComponentsInChildren<Transform>();
                var first = bones.Select(b => b.position).ToArray();
                playable.SetTime(clip.length * .35f); graph.Evaluate(0);
                Require(bones.Select((b, i) => Vector3.Distance(first[i], b.position)).Max() > .01f, "Run animation is not moving the skin.");
            }
            finally { graph.Destroy(); animator.cullingMode = previous; }
        }

        private static string Structure(GameObject root) => string.Join("\n", root.GetComponentsInChildren<Transform>(true).Select(t =>
            AnimationUtility.CalculateTransformPath(t, root.transform) + "|" + t.localPosition.ToString("R") + "|" + t.localRotation.ToString("R") + "|" + t.localScale.ToString("R") + "|" + string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().FullName))));

        private static string OpenSceneState() => string.Join(";", Enumerable.Range(0, SceneManager.sceneCount).Select(i =>
        {
            var scene = SceneManager.GetSceneAt(i);
            return scene.handle + ":" + scene.path + ":" + scene.isDirty + ":" + string.Join(",", scene.GetRootGameObjects().Select(o => o.GetEntityId()));
        }));

        private static string HashFile(string path)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            Require(!string.IsNullOrEmpty(parent), "Invalid asset folder.");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Require(bool condition, string message)
        { if (!condition) { throw new InvalidOperationException(message); } }

        [Serializable] private sealed class Manifest { public bool passed; public Skin[] skins; }
        [Serializable] private sealed class Skin { public string name; public string texture; public int[] size; public Settings settings; }
        [Serializable] private sealed class Settings { public float roughness; public float metallic; public float emission; public float coat; }
        [Serializable] private sealed class SkinResult { public string name; public string prefab; public bool sharedMeshAndAvatar; public bool animationPlayback; }
        [Serializable] private sealed class ImportReport
        {
            public bool passed;
            public bool baseAndAnimationsUnchanged;
            public bool openScenesUnchanged;
            public int textureCount;
            public int materialCount;
            public string error;
            public List<SkinResult> skins = new();
        }
    }
}
