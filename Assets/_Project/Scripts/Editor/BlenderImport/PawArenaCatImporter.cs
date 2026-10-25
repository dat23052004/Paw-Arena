using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PawArena.Editor.BlenderImport
{
    /// <summary>Imports the approved cat and 26 clips without changing gameplay or open scenes.</summary>
    public static class PawArenaCatImporter
    {
        private const string ModelFolder = "Assets/_Project/Models/Characters/Cat/";
        private const string ModelPath = ModelFolder + "SK_PawArena_Cat.fbx";
        private const string AnimationFolder = "Assets/_Project/Animations/Characters/Cat/";
        private const string MaterialFolder = "Assets/_Project/Materials/Characters/Cat/";
        private const string TexturePath = "Assets/_Project/Textures/Characters/Cat/Cat_BaseColor.png";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Cat/PF_Cat_Base.prefab";
        private const string ReportPath = "Blender/Validation/MainProjectCatImport.json";
        private const string RequestPath = "Library/PawArenaCatImport.request";
        private const int Fps = 30;
        private const int SampleCount = 21;
        private static bool _isImporting;

        private static readonly ClipSpec[] Clips =
        {
            new("Idle",72,true), new("Walk",30,true), new("Run",20,true),
            new("Jump_Start",12,false), new("Jump_Air",24,true), new("Fall",24,true),
            new("Land",14,false), new("Punch_Left",18,false), new("Punch_Right",18,false),
            new("Hit_React",16,false), new("Ability_Dash",18,false),
            new("Ability_NineLives_FaceUp",36,false), new("Ability_NineLives_FaceDown",36,false),
            new("Ability_Hiss",18,false), new("Ability_Pounce_Start",12,false),
            new("Ability_Pounce_Air",18,true), new("Ability_Pounce_Land",15,false),
            new("Ability_Anchor_Start",18,false), new("Ability_Anchor_Hold",48,true),
            new("Ability_Anchor_End",18,false), new("Ability_Stomp",24,false),
            new("Ability_Catnip",24,false), new("Ability_FluffUp",27,false),
            new("Ability_Hairball",24,false), new("Ability_SharkAttack",33,false),
            new("Ability_WindShurikens",33,false)
        };

        [InitializeOnLoadMethod]
        private static void CheckExplicitImportRequest()
        {
            EditorApplication.delayCall += RunPendingImport;
        }

        private static void RunPendingImport()
        {
            if (!File.Exists(RequestPath) || File.ReadAllText(RequestPath).Trim() != "import-base-and-26-clips") { return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunPendingImport;
                return;
            }
            File.WriteAllText(RequestPath,"running");
            try
            {
                ImportAll();
                File.WriteAllText(RequestPath,"complete");
            }
            catch (Exception)
            {
                File.WriteAllText(RequestPath,"failed - see Blender/Validation/MainProjectCatImport.json");
            }
        }

        /// <summary>Explicit import command; a one-use request can also invoke it after compilation.</summary>
        [MenuItem("Tools/Paw Arena/Import Cat Base and 26 Animations")]
        public static void ImportAll()
        {
            if (_isImporting) { return; }
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before importing.");
            _isImporting = true;
            var report = new ImportReport { unityVersion = Application.unityVersion, prefab = PrefabPath };
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Require(!File.Exists(PrefabPath), "Cat prefab already exists. This command does not overwrite a customized prefab.");
                foreach (var spec in Clips) { Require(File.Exists(spec.Path), "Missing source clip: " + spec.Path); }
                EnsureFolder(MaterialFolder.TrimEnd('/'));
                EnsureFolder(Path.GetDirectoryName(PrefabPath)?.Replace('\\','/'));
                ConfigureTexture();
                var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
                Require(importer != null, "Missing approved reference model.");
                var preset = AssetDatabase.LoadAssetAtPath<Preset>(ModelFolder + "Cat_Humanoid.preset");
                Require(preset != null, "Missing validated Humanoid preset.");
                Undo.RecordObject(importer, "Configure approved cat rig");
                preset.ApplyTo(importer);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.isReadable = true;
                importer.optimizeGameObjects = false;
                importer.preserveHierarchy = true;
                importer.SaveAndReimport();
                ConfigureMaterials(importer);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().Single();
                Require(avatar.isValid && avatar.isHuman, "Invalid Humanoid Avatar.");
                var mask = ConfigureMask(model);
                var importedClips = Clips.Select(spec => ConfigureClip(spec, avatar, mask)).ToArray();
                ValidateAndCreatePrefab(model, avatar, importedClips, report);
                AssetDatabase.SaveAssets();
                report.passed = true;
                report.sharedAvatar = ModelPath;
                report.clipCount = importedClips.Length;
                WriteReport(report);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                EditorGUIUtility.PingObject(Selection.activeObject);
                Debug.Log("[Paw Arena Cat] IMPORT PASS: approved base, one Humanoid Avatar, 26 clips, URP materials and visual prefab. No scene/gameplay changes.");
            }
            catch (Exception exception)
            {
                report.error = exception.ToString();
                WriteReport(report);
                Debug.LogException(exception);
                throw;
            }
            finally { _isImporting = false; }
        }

        private static void ConfigureTexture()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            Require(importer != null, "Missing cat atlas.");
            Undo.RecordObject(importer, "Configure cat color atlas");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureMaterials(ModelImporter importer)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "URP Lit shader is not available.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var materials = model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials;
            Require(materials.Length == 2 && materials.All(m => m != null), "Expected coat and eye material slots.");
            foreach (var source in materials)
            {
                var isEye = source.name.Contains("Eye", StringComparison.OrdinalIgnoreCase);
                var path = MaterialFolder + (isEye ? "M_Cat_Eyes.mat" : "M_Cat_Coat.mat");
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                    Undo.RegisterCreatedObjectUndo(material, "Create cat URP material");
                    material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Smoothness", isEye ? .70f : .25f);
                    AssetDatabase.CreateAsset(material, path);
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
            }
            importer.SaveAndReimport();
        }

        private static AvatarMask ConfigureMask(GameObject model)
        {
            var path = AnimationFolder + "Cat_AllBones.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            var isNew = mask == null;
            if (isNew) { mask = new AvatarMask { name = "Cat_AllBones" }; }
            Undo.RecordObject(mask, "Preserve cat ears and tail");
            for (var body = 0; body < (int)AvatarMaskBodyPart.LastBodyPart; body++)
            { mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)body,true); }
            var bones = model.GetComponentsInChildren<Transform>();
            mask.transformCount = bones.Length;
            for (var i = 0; i < bones.Length; i++)
            {
                mask.SetTransformPath(i,AnimationUtility.CalculateTransformPath(bones[i],model.transform));
                mask.SetTransformActive(i,true);
            }
            if (isNew) { AssetDatabase.CreateAsset(mask,path); }
            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssets();
            return mask;
        }

        private static AnimationClip ConfigureClip(ClipSpec spec, Avatar avatar, AvatarMask mask)
        {
            var importer = AssetImporter.GetAtPath(spec.Path) as ModelImporter;
            Require(importer != null,"Missing importer: " + spec.Path);
            Undo.RecordObject(importer,"Import cat animation");
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
            take.name = "Cat_" + spec.Name;
            take.lastFrame = take.firstFrame + spec.Frames;
            take.loopTime = spec.Loop;
            take.loopPose = false;
            take.lockRootRotation = true;
            take.keepOriginalOrientation = true;
            take.lockRootPositionXZ = true;
            take.keepOriginalPositionXZ = true;
            take.lockRootHeightY = true;
            take.keepOriginalPositionY = true;
            take.heightFromFeet = false;
            take.maskType = ClipAnimationMaskType.CopyFromOther;
            take.maskSource = mask;
            take.ConfigureClipFromMask(mask);
            importer.clipAnimations = new[] {take};
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAllAssetsAtPath(spec.Path).OfType<AnimationClip>()
                .Single(c => !c.name.StartsWith("__preview__",StringComparison.Ordinal));
            Require(clip.isHumanMotion && Mathf.Abs(clip.length - spec.Frames/(float)Fps) < .002f,
                "Invalid Humanoid clip/duration: " + spec.Name);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            Require(bindings.Any(b => b.path.Contains("Tail_")) && bindings.Any(b => b.path.Contains("LeftEar")),
                "Missing extra-bone curves: " + spec.Name);
            return clip;
        }

        private static void ValidateAndCreatePrefab(GameObject model, Avatar avatar, AnimationClip[] clips, ImportReport report)
        {
            var previewScene = EditorSceneManager.NewPreviewScene();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model,previewScene);
            Undo.RegisterCreatedObjectUndo(instance,"Create cat visual prefab");
            var baked = new Mesh();
            try
            {
                instance.name = "PF_Cat_Base";
                var animator = instance.GetComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = null;
                // Preview scenes have no camera; validation must not be visibility-culled.
                var originalCullingMode = animator.cullingMode;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                Require(skin != null && skin.sharedMesh != null,"Missing skinned mesh.");
                Require(skin.sharedMaterials.All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit"
                    && m.GetTexture("_BaseMap") != null),"Invalid URP materials/atlas.");
                Require(skin.sharedMaterials.Distinct().Count() == 2,"Coat and eye material assignments are not distinct.");
                var bones = instance.GetComponentsInChildren<Transform>();
                Require(bones.Any(t => t.name == "HeadGearSocket"),"Missing head gear socket.");
                var union = new Bounds();
                var hasBounds = false;
                for (var index = 0; index < clips.Length; index++)
                {
                    var graph = PlayableGraph.Create("Validate imported cat clip");
                    try
                    {
                        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        var playable = AnimationClipPlayable.Create(graph,clips[index]);
                        playable.SetApplyFootIK(false);
                        playable.SetApplyPlayableIK(false);
                        AnimationPlayableOutput.Create(graph,"Cat",animator).SetSourcePlayable(playable);
                        graph.Play();
                        Vector3[] initial = null;
                        Quaternion[] initialRotations = null;
                        var clipReport = new ClipReport { name=clips[index].name, seconds=clips[index].length,
                            loop=Clips[index].Loop, sampledPoses=SampleCount };
                        for (var sample = 0; sample < SampleCount; sample++)
                        {
                            var time = clips[index].length * sample/(SampleCount-1);
                            if (Clips[index].Loop && sample == SampleCount-1) { time = Mathf.Max(0,time-.00001f); }
                            playable.SetTime(time);
                            graph.Evaluate(0);
                            skin.BakeMesh(baked,true);
                            var vertices = baked.vertices;
                            var bounds = new Bounds(skin.transform.TransformPoint(vertices[0]),Vector3.zero);
                            foreach (var vertex in vertices)
                            {
                                Require(float.IsFinite(vertex.x) && float.IsFinite(vertex.y) && float.IsFinite(vertex.z),"Non-finite vertex.");
                                bounds.Encapsulate(skin.transform.TransformPoint(vertex));
                            }
                            Require(bounds.size.magnitude < 8f,"Unexpected skin deformation: " + clips[index].name);
                            Require(instance.transform.position.sqrMagnitude < .000001f,"Unexpected root travel.");
                            if (!hasBounds) { union=bounds; hasBounds=true; } else { union.Encapsulate(bounds); }
                            if (sample==0)
                            {
                                initial=bones.Select(b=>b.position).ToArray();
                                initialRotations=bones.Select(b=>b.rotation).ToArray();
                                clipReport.startHeightMetres=bounds.size.y;
                            }
                            for (var boneIndex=0; boneIndex<bones.Length; boneIndex++)
                            {
                                clipReport.maxBoneRotationDegrees=Mathf.Max(clipReport.maxBoneRotationDegrees,
                                    Quaternion.Angle(initialRotations[boneIndex],bones[boneIndex].rotation));
                            }
                            if (sample==SampleCount-1)
                            {
                                clipReport.endHeightMetres=bounds.size.y;
                                clipReport.endpointDifferenceMetres=bones.Select((b,i)=>Vector3.Distance(initial[i],b.position)).Max();
                            }
                        }
                        Require(clipReport.maxBoneRotationDegrees>.05f,"Static animation: " + clips[index].name);
                        if (Clips[index].Loop) { Require(clipReport.endpointDifferenceMetres<.005f,"Loop seam: " + clips[index].name); }
                        if (clips[index].name.Contains("NineLives"))
                        { Require(clipReport.startHeightMetres<1.5f && clipReport.endHeightMetres>1.9f,"Recovery pose lost."); }
                        report.clips.Add(clipReport);
                    }
                    finally { graph.Destroy(); }
                }
                // The reference FBX has no clips of its own; include all sampled poses in culling bounds.
                union.Expand(.2f);
                var local = new Bounds(skin.transform.InverseTransformPoint(union.center),Vector3.zero);
                for (var x=-1; x<=1; x+=2)
                for (var y=-1; y<=1; y+=2)
                for (var z=-1; z<=1; z+=2)
                { local.Encapsulate(skin.transform.InverseTransformPoint(union.center+Vector3.Scale(union.extents,new Vector3(x,y,z)))); }
                // Restore rest transforms before saving; no sampled pose may leak into the prefab.
                var original = model.GetComponentsInChildren<Transform>();
                foreach (var bone in bones)
                {
                    var path = AnimationUtility.CalculateTransformPath(bone,instance.transform);
                    var source = original.Single(t=>AnimationUtility.CalculateTransformPath(t,model.transform)==path);
                    bone.localPosition=source.localPosition;
                    bone.localRotation=source.localRotation;
                    bone.localScale=source.localScale;
                }
                skin.localBounds=local;
                animator.cullingMode=originalCullingMode;
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance,PrefabPath);
                Require(prefab != null && prefab.GetComponent<Animator>().avatar == avatar,"Prefab save failed.");
                report.worldCullingBounds=union;
                report.hasHeadGearSocket=true;
                report.urpMaterials=true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
                Undo.DestroyObjectImmediate(instance);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void EnsureFolder(string path)
        {
            Require(!string.IsNullOrEmpty(path),"Invalid folder path.");
            if (AssetDatabase.IsValidFolder(path)) { return; }
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\','/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path)?.Replace('\\','/'),Path.GetFileName(path));
        }

        private static void WriteReport(ImportReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));
        }

        private static void Require(bool condition,string message)
        { if (!condition) { throw new InvalidOperationException(message); } }

        private readonly struct ClipSpec
        {
            public ClipSpec(string name,int frames,bool loop) { Name=name; Frames=frames; Loop=loop; }
            public string Name { get; }
            public int Frames { get; }
            public bool Loop { get; }
            public string Path => AnimationFolder + (Name.StartsWith("Ability_",StringComparison.Ordinal)?"Abilities/":"Base/") + "AN_Cat_" + Name + ".fbx";
        }

        [Serializable] private sealed class ImportReport
        {
            public bool passed;
            public string unityVersion;
            public string prefab;
            public string sharedAvatar;
            public int clipCount;
            public bool urpMaterials;
            public bool hasHeadGearSocket;
            public Bounds worldCullingBounds;
            public string error;
            public List<ClipReport> clips = new();
        }

        [Serializable] private sealed class ClipReport
        {
            public string name;
            public float seconds;
            public bool loop;
            public int sampledPoses;
            public float endpointDifferenceMetres;
            public float maxBoneRotationDegrees;
            public float startHeightMetres;
            public float endHeightMetres;
        }
    }
}
