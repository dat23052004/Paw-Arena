using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PawArena.Editor.BlenderImport
{
    [InitializeOnLoad]
    public static class PawArenaBlenderPrefabBuilder
    {
        private const string GeneratorVersion = "2026.10.24.8";
        private const string ModelFolder = "Assets/_Game/Models/Environment/BlenderExports";
        private const string MaterialFolder = "Assets/_Game/Materials/Environment/Blender";
        private const string PrefabFolder = "Assets/_Game/Prefabs/Environment/Blender";
        private const string MarkerPath = ModelFolder + "/PawArenaBlenderImport.version.txt";
        private const string LayoutPath = PrefabFolder + "/PF_PawArenaLobbyLayout.prefab";

        private static readonly ModelSpec[] ModelSpecs =
        {
            new("PF_BlockGround", ColliderMode.Box, true),
            new("PF_LeaderboardBoard", ColliderMode.Box, true),
            new("PF_WoodCrate", ColliderMode.Box, true),
            new("PF_LikePillar", ColliderMode.Box, true),
            new("PF_AlienWheel", ColliderMode.Box, false),
            new("PF_ArenaDirtPatch", ColliderMode.Mesh, true),
            new("PF_AlienEventPortal", ColliderMode.Portal, false),
            new("PF_AlienEggPedestal", ColliderMode.Box, true),
            new("PF_PlayerCapsuleReference", ColliderMode.Capsule, false),
        };

        private static bool _isBuilding;
        private static int _autoBuildAttempts;

        static PawArenaBlenderPrefabBuilder()
        {
            EditorApplication.delayCall += TryAutoBuild;
        }

        [MenuItem("Tools/Paw Arena/Rebuild Blender Prefabs")]
        public static void BuildAll()
        {
            if (_isBuilding)
            {
                return;
            }

            _isBuilding = true;
            try
            {
                EnsureFolder("Assets/_Game");
                EnsureFolder("Assets/_Game/Materials");
                EnsureFolder("Assets/_Game/Materials/Environment");
                EnsureFolder(MaterialFolder);
                EnsureFolder("Assets/_Game/Prefabs");
                EnsureFolder("Assets/_Game/Prefabs/Environment");
                EnsureFolder(PrefabFolder);

                ApplyModelImporterSettings();

                var prefabAssets = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                foreach (var spec in ModelSpecs)
                {
                    prefabAssets.Add(spec.Name, CreateModelPrefab(spec));
                }

                CreateLayoutPrefab(prefabAssets);
                File.WriteAllText(Path.GetFullPath(MarkerPath), GeneratorVersion);
                AssetDatabase.ImportAsset(MarkerPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[PawArena Blender Import] Generated {ModelSpecs.Length} editable prefabs and lobby layout at {PrefabFolder}.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                _isBuilding = false;
            }
        }

        private static void TryAutoBuild()
        {
            if (_isBuilding || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                RetryAutoBuild();
                return;
            }

            if (File.Exists(Path.GetFullPath(MarkerPath)) &&
                File.ReadAllText(Path.GetFullPath(MarkerPath)).Trim() == GeneratorVersion &&
                AssetDatabase.LoadAssetAtPath<GameObject>(LayoutPath) != null)
            {
                return;
            }

            foreach (var spec in ModelSpecs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(GetModelPath(spec.Name)) == null)
                {
                    RetryAutoBuild();
                    return;
                }
            }

            BuildAll();
        }

        private static void RetryAutoBuild()
        {
            _autoBuildAttempts++;
            if (_autoBuildAttempts <= 120)
            {
                EditorApplication.delayCall += TryAutoBuild;
            }
        }

        private static void ApplyModelImporterSettings()
        {
            foreach (var spec in ModelSpecs)
            {
                var modelPath = GetModelPath(spec.Name);
                if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
                {
                    throw new InvalidOperationException($"FBX model importer was not found: {modelPath}");
                }

                if (PawArenaBlenderModelPostprocessor.ApplySettings(importer))
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static GameObject CreateModelPrefab(ModelSpec spec)
        {
            var modelPath = GetModelPath(spec.Name);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                throw new InvalidOperationException($"FBX model was not imported: {modelPath}");
            }

            var root = new GameObject(spec.Name);
            try
            {
                var importedInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                PrefabUtility.UnpackPrefabInstance(
                    importedInstance,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);

                importedInstance.transform.SetParent(root.transform, false);
                var modelInstance = FlattenImportedRootTransform(importedInstance, root.transform);

                AssignEditableMaterials(modelInstance);
                AddCollider(root, modelInstance, spec.ColliderMode);
                if (spec.MarkStatic)
                {
                    SetStaticRecursively(root);
                }

                root.SetActive(true);
                var prefabPath = GetPrefabPath(spec.Name);
                var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefabAsset == null)
                {
                    throw new InvalidOperationException($"Failed to save prefab: {prefabPath}");
                }

                return prefabAsset;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject FlattenImportedRootTransform(GameObject importedInstance, Transform prefabRoot)
        {
            if (importedInstance.GetComponent<Renderer>() != null ||
                importedInstance.GetComponent<MeshFilter>() != null)
            {
                throw new InvalidOperationException(
                    $"Expected an empty FBX root for {importedInstance.name}. The root contains mesh components.");
            }

            var modelContainer = new GameObject("Model");
            modelContainer.transform.SetParent(prefabRoot, false);
            modelContainer.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            modelContainer.transform.localScale = Vector3.one;

            var children = new List<Transform>(importedInstance.transform.childCount);
            for (var index = 0; index < importedInstance.transform.childCount; index++)
            {
                children.Add(importedInstance.transform.GetChild(index));
            }

            foreach (var child in children)
            {
                // Preserve the imported world matrix while removing the FBX root's
                // axis-correction transform. Model and prefab roots remain identity.
                child.SetParent(modelContainer.transform, true);
            }

            UnityEngine.Object.DestroyImmediate(importedInstance);
            return modelContainer;
        }

        private static void AssignEditableMaterials(GameObject modelInstance)
        {
            foreach (var renderer in modelInstance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var index = 0; index < materials.Length; index++)
                {
                    var source = materials[index];
                    if (source == null)
                    {
                        continue;
                    }

                    materials[index] = GetOrCreateMaterial(source);
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private static Material GetOrCreateMaterial(Material source)
        {
            var safeName = SanitizeFileName(source.name);
            var path = $"{MaterialFolder}/{safeName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var created = material == null;
            if (created)
            {
                material = new Material(shader) { name = safeName };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else
            {
                material.color = color;
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.22f);
            }

            ConfigureSpecialMaterial(material, safeName, color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureSpecialMaterial(Material material, string materialName, Color baseColor)
        {
            var isBeam = materialName.Contains("Beam", StringComparison.OrdinalIgnoreCase);
            var isGlow = materialName.Contains("Green", StringComparison.OrdinalIgnoreCase) ||
                         materialName.Contains("Crystal", StringComparison.OrdinalIgnoreCase);

            if (isGlow && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", baseColor * (isBeam ? 0.65f : 1.35f));
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (!isBeam)
            {
                return;
            }

            var transparentColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.18f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", transparentColor);
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
        }

        private static void AddCollider(GameObject root, GameObject model, ColliderMode mode)
        {
            switch (mode)
            {
                case ColliderMode.Box:
                    AddBoundsCollider(root, model);
                    break;
                case ColliderMode.Mesh:
                    AddMeshColliders(model, "DirtPatch");
                    break;
                case ColliderMode.Portal:
                    AddMeshColliders(model, "PortalPad_Base", "CrystalPedestals");
                    break;
                case ColliderMode.Capsule:
                    var capsule = root.AddComponent<CapsuleCollider>();
                    capsule.center = new Vector3(0f, 1.02f, 0f);
                    capsule.height = 2.04f;
                    capsule.radius = 0.58f;
                    break;
                case ColliderMode.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private static void AddBoundsCollider(GameObject root, GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = root.transform.InverseTransformVector(bounds.size);
        }

        private static void AddMeshColliders(GameObject model, params string[] allowedNames)
        {
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var allowed = false;
                foreach (var allowedName in allowedNames)
                {
                    if (filter.name.Contains(allowedName, StringComparison.OrdinalIgnoreCase))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed || filter.sharedMesh == null)
                {
                    continue;
                }

                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
            }
        }

        private static void SetStaticRecursively(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(
                    transform.gameObject,
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ContributeGI);
            }
        }

        private static void CreateLayoutPrefab(IReadOnlyDictionary<string, GameObject> prefabs)
        {
            var root = new GameObject("PF_PawArenaLobbyLayout");
            try
            {
                AddLayoutInstance(root.transform, prefabs["PF_BlockGround"], "BlockGround_20x20", Vector3.zero, 0f, 1f);
                AddLayoutInstance(root.transform, prefabs["PF_ArenaDirtPatch"], "ArenaDirtCircle", new Vector3(-2.8f, 0.04f, -2.2f), 0f, 1f);

                AddLayoutInstance(root.transform, prefabs["PF_LeaderboardBoard"], "Leaderboard_FriendsSmall", new Vector3(-15.85f, 0.05f, 13.50f), 13f, 0.74f);
                AddLayoutInstance(root.transform, prefabs["PF_LeaderboardBoard"], "Leaderboard_MostKOs", new Vector3(-10.85f, 0.05f, 15.30f), 8f, 1.02f);
                AddLayoutInstance(root.transform, prefabs["PF_LeaderboardBoard"], "Leaderboard_MostWins", new Vector3(-5.20f, 0.05f, 16.10f), 0f, 1.06f);
                AddLayoutInstance(root.transform, prefabs["PF_LeaderboardBoard"], "Leaderboard_WinStreak", new Vector3(0.55f, 0.05f, 15.45f), -8f, 1.02f);

                AddLayoutInstance(root.transform, prefabs["PF_WoodCrate"], "WoodCrate_Left01", new Vector3(-17.35f, 0.05f, 11.15f), -12f, 1f);
                AddLayoutInstance(root.transform, prefabs["PF_WoodCrate"], "WoodCrate_Left02", new Vector3(-16.10f, 0.05f, 11.30f), 8f, 0.96f);
                AddLayoutInstance(root.transform, prefabs["PF_WoodCrate"], "WoodCrate_Left03_Stacked", new Vector3(-16.78f, 1.18f, 11.28f), -7f, 0.94f);
                AddLayoutInstance(root.transform, prefabs["PF_WoodCrate"], "WoodCrate_Left04_Back", new Vector3(-17.48f, 0.05f, 12.42f), 14f, 0.90f);
                AddLayoutInstance(root.transform, prefabs["PF_WoodCrate"], "WoodCrate_Right01", new Vector3(10.55f, 0.05f, 11.45f), -18f, 0.92f);

                AddLayoutInstance(root.transform, prefabs["PF_LikePillar"], "LikePillar", new Vector3(7.55f, 0f, 15.15f), 6f, 0.90f);
                AddLayoutInstance(root.transform, prefabs["PF_AlienWheel"], "AlienWheel", new Vector3(14.20f, 0f, 13.00f), -5f, 0.92f);
                AddLayoutInstance(root.transform, prefabs["PF_AlienEventPortal"], "AlienEventPortal", new Vector3(10.20f, 0f, -1.00f), 4f, 1f);
                AddLayoutInstance(root.transform, prefabs["PF_AlienEggPedestal"], "AlienEggPedestal", new Vector3(-13.40f, 0f, -5.60f), -8f, 1f);

                var players = new[]
                {
                    new LayoutTransform("PlayerPreview_01", new Vector3(-6.0f, 0.06f, -1.5f), -22f),
                    new LayoutTransform("PlayerPreview_02", new Vector3(-3.8f, 0.06f, -0.2f), 18f),
                    new LayoutTransform("PlayerPreview_03", new Vector3(-1.2f, 0.06f, -0.8f), -36f),
                    new LayoutTransform("PlayerPreview_04", new Vector3(0.3f, 0.06f, -3.1f), 32f),
                    new LayoutTransform("PlayerPreview_05", new Vector3(-5.1f, 0.06f, -4.5f), -10f),
                    new LayoutTransform("PlayerPreview_06", new Vector3(-2.2f, 0.06f, -4.9f), 12f),
                };
                foreach (var player in players)
                {
                    AddLayoutInstance(root.transform, prefabs["PF_PlayerCapsuleReference"], player.Name, player.Position, player.Yaw, 1f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, LayoutPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject AddLayoutInstance(
            Transform parent,
            GameObject prefab,
            string name,
            Vector3 position,
            float yaw,
            float uniformScale)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * uniformScale;
            return instance;
        }

        private static string GetModelPath(string name) => $"{ModelFolder}/{name}.fbx";

        private static string GetPrefabPath(string name) => $"{PrefabFolder}/{name}.prefab";

        private static string SanitizeFileName(string value)
        {
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidCharacter, '_');
            }

            return value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"Invalid Unity folder path: {path}");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private readonly struct ModelSpec
        {
            public ModelSpec(string name, ColliderMode colliderMode, bool markStatic)
            {
                Name = name;
                ColliderMode = colliderMode;
                MarkStatic = markStatic;
            }

            public string Name { get; }
            public ColliderMode ColliderMode { get; }
            public bool MarkStatic { get; }
        }

        private readonly struct LayoutTransform
        {
            public LayoutTransform(string name, Vector3 position, float yaw)
            {
                Name = name;
                Position = position;
                Yaw = yaw;
            }

            public string Name { get; }
            public Vector3 Position { get; }
            public float Yaw { get; }
        }

        private enum ColliderMode
        {
            None,
            Box,
            Mesh,
            Portal,
            Capsule,
        }
    }
}
