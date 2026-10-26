using System;
using System.IO;
using System.Linq;
using PawArena.Bootstrap;
using PawArena.Gameplay.Bots;
using PawArena.Gameplay.Characters;
using PawArena.Gameplay.Combat;
using PawArena.Gameplay.Inventory;
using PawArena.Gameplay.Maps;
using PawArena.Gameplay.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PawArena.Editor.Characters
{
    /// <summary>Builds the movement-only foundation without overwriting authored assets or saving open user scenes.</summary>
    public static class CharacterFoundationBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        public const string PlayerPath = "Assets/_Project/Prefabs/Player/PF_Player.prefab";
        public const string BotPath = "Assets/_Project/Prefabs/Bots/PF_Bot.prefab";
        private const string DataPath = "Assets/_Project/ScriptableObjects/Characters";
        private const string Request = "Library/CharacterFoundation.request";
        private static double _nextPoll;
        [InitializeOnLoadMethod] private static void Register()
        { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) { return; } _nextPoll = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) { return; }
            var command = File.ReadAllText(Request).Trim();
            if (command != "build" && command != "open" && command != "tune" && command != "combat") { return; }
            File.WriteAllText(Request, "running");
            try
            {
                if (command == "build") { Build(); }
                else if (command == "tune") { ConfigureCharacterFriction(); }
                else if (command == "combat") { ConfigurePlayerCombat(); }
                else
                {
                    for (var i = 0; i < SceneManager.sceneCount; i++)
                    { if (SceneManager.GetSceneAt(i).isDirty) { File.WriteAllText(Request, "blocked: save your open scene before switching to Main"); return; } }
                    EditorSceneManager.OpenScene(ScenePath);
                }
                File.WriteAllText(Request, "complete: " + command + "\nOpen scenes: " +
                    string.Join("; ", Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path + " dirty=" + SceneManager.GetSceneAt(i).isDirty)));
            }
            catch (Exception exception) { File.WriteAllText(Request, exception.ToString()); Debug.LogException(exception); }
        }

        [MenuItem("Tools/Paw Arena/Characters/Create Movement Foundation")]
        public static void Build()
        {
            if (File.Exists(ScenePath) || File.Exists(PlayerPath) || File.Exists(BotPath))
            { throw new IOException("Character foundation already exists. Refusing to overwrite authored assets."); }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Exit Play Mode first."); }
            Folder(DataPath); Folder("Assets/_Project/Prefabs/Player"); Folder("Assets/_Project/Prefabs/Bots"); Folder("Assets/_Project/Prefabs/Maps");
            var settings = ScriptableObject.CreateInstance<CatLocomotionSettings>();
            AssetDatabase.CreateAsset(settings, DataPath + "/CatLocomotion.asset"); Undo.RegisterCreatedObjectUndo(settings, "Create movement settings");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(DataPath + "/CatLocomotion.controller");
            var names = new[] { "Idle", "Run", "Jump_Air" };
            foreach (var name in names)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Animations/Characters/Cat/Base/AN_Cat_" + name + ".fbx")
                    .OfType<AnimationClip>().First(c => c.name == "Cat_" + name);
                var state = controller.layers[0].stateMachine.AddState(clip.name);
                state.motion = clip; state.writeDefaultValues = true; state.iKOnFeet = false;
                if (name == "Idle") { controller.layers[0].stateMachine.defaultState = state; }
            }
            var floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMaterial.SetColor("_BaseColor", new Color(.22f, .31f, .34f)); floorMaterial.SetFloat("_Smoothness", .15f);
            AssetDatabase.CreateAsset(floorMaterial, DataPath + "/M_ReviewFloor.mat");
            var savedActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var playerPrefab = BuildCharacter("PF_Player", PlayerPath, false);
                BuildCharacter("PF_Bot", BotPath, true);
                var systems = Root("Systems");
                var mapAnchor = Root("MapAnchor"); // Fixed world location; later cycles instantiate map prefabs here.
                var map = Root("PF_Map_TestFloor");
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(floor, scene);
                floor.name = "Floor"; floor.transform.SetParent(map.transform, false); floor.transform.localPosition = new Vector3(0, -.25f, 0);
                floor.transform.localScale = new Vector3(40, .5f, 40); floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                Undo.RegisterCreatedObjectUndo(floor, "Create test floor");
                var points = new Transform[8];
                for (var i = 0; i < points.Length; i++)
                { var point = Child(map, "Spawn_" + i); point.transform.localPosition = new Vector3(i == 0 ? 0 : (i % 4 - 1.5f) * 3, .03f, i == 0 ? 0 : 4 + (i / 4) * 3); points[i] = point.transform; }
                var spawnPoints = Undo.AddComponent<MapSpawnPoints>(map); var spawnData = new SerializedObject(spawnPoints);
                var array = spawnData.FindProperty("_points"); array.arraySize = points.Length;
                for (var i = 0; i < points.Length; i++) { array.GetArrayElementAtIndex(i).objectReferenceValue = points[i]; }
                spawnData.ApplyModifiedProperties();
                var mapPrefab = PrefabUtility.SaveAsPrefabAsset(map, "Assets/_Project/Prefabs/Maps/PF_Map_TestFloor.prefab");
                Object.DestroyImmediate(map);
                var mapInstance = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, scene); mapInstance.transform.SetParent(mapAnchor.transform, false);
                var actors = Root("Actors");
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene); player.name = "Player"; player.transform.SetParent(actors.transform, false);
                player.transform.position = new Vector3(0, .03f, 0);
                var cameraObject = Root("MainCamera"); cameraObject.tag = "MainCamera";
                var camera = Undo.AddComponent<Camera>(cameraObject); Undo.AddComponent<AudioListener>(cameraObject);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .08f, .12f);
                camera.fieldOfView = 45; camera.nearClipPlane = .05f; camera.farClipPlane = 150;
                camera.transform.position = new Vector3(3, 2.5f, 5); camera.transform.LookAt(Vector3.up * 1.05f);
                var follow = Undo.AddComponent<CharacterReviewCamera>(cameraObject); Set(follow, "_target", player.transform);
                var key = Undo.AddComponent<Light>(Root("KeyLight")); key.type = LightType.Directional; key.intensity = 2.2f;
                key.transform.rotation = Quaternion.Euler(45, -35, 0); key.shadows = LightShadows.Soft;
                var fill = Undo.AddComponent<Light>(Root("FillLight")); fill.type = LightType.Directional; fill.intensity = 1;
                fill.color = new Color(.65f, .8f, 1); fill.transform.rotation = Quaternion.Euler(30, 150, 0);
                var session = Undo.AddComponent<CharacterReviewSession>(systems);
                Set(session, "_player", player.GetComponent<CatPlayerController>()); Set(session, "_camera", follow);
                EditorSceneManager.SaveScene(scene, ScenePath);

                GameObject BuildCharacter(string name, string path, bool isBot)
                {
                    var root = Root(name); var motor = Undo.AddComponent<CatLocomotion>(root); Set(motor, "_settings", settings);
                    var body = root.GetComponent<Rigidbody>(); body.constraints = RigidbodyConstraints.FreezeRotation;
                    body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    var capsule = root.GetComponent<CapsuleCollider>(); capsule.radius = .4f; capsule.height = 1.8f; capsule.center = Vector3.up * .9f;
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/Cat/PF_Cat_Base.prefab"), scene);
                    visual.name = "Visual"; visual.transform.SetParent(root.transform, false);
                    var animator = visual.GetComponent<Animator>(); Undo.RecordObject(animator, "Assign locomotion Animator");
                    animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                    var skin = Undo.AddComponent<CatSkinController>(visual);
                    skin.TryApply(AssetDatabase.LoadAssetAtPath<CatSkinDefinition>("Assets/_Project/ScriptableObjects/Inventory/CatSkins/Skin_" + (isBot ? "Grey" : "Ginger") + ".asset"));
                    foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) { PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); }
                    var animation = Undo.AddComponent<CatAnimationController>(root); Set(animation, "_motor", motor); Set(animation, "_animator", animator);
                    var motion = Undo.AddComponent<CatSecondaryMotion>(root); Set(motion, "_motor", motor); Set(motion, "_animator", animator);
                    if (isBot) { var bot = Undo.AddComponent<CatBotController>(root); Set(bot, "_motor", motor); }
                    else { var input = Undo.AddComponent<CatPlayerController>(root); Set(input, "_motor", motor); Set(input, "_animation", animation); }
                    var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return prefab;
                }
                GameObject Root(string name)
                { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); Undo.RegisterCreatedObjectUndo(go, "Create character foundation"); return go; }
                GameObject Child(GameObject parent, string name)
                { var go = Root(name); go.transform.SetParent(parent.transform, false); return go; }
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (savedActive.IsValid()) { SceneManager.SetActiveScene(savedActive); } }
            AssetDatabase.SaveAssets();
            ConfigureCharacterFriction();
            // One runtime scene. Inventory/asset-authoring scenes remain in the project, not in the player build.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.Refresh();
        }
        private static void Folder(string path)
        { if (AssetDatabase.IsValidFolder(path)) { return; } var parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
        private static void Set(Object target, string name, Object value)
        { var data = new SerializedObject(target); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedProperties(); }
        private static void Set(Object target, string name, bool value)
        { var data = new SerializedObject(target); data.FindProperty(name).boolValue = value; data.ApplyModifiedProperties(); }

        /// <summary>The motor owns braking; floor friction must not subtract unpredictable speed after each step.</summary>
        public static void ConfigureCharacterFriction()
        {
            var path = DataPath + "/CatMovement.physicsMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial("CatMovement") { staticFriction = 0, dynamicFriction = 0,
                    frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0, bounceCombine = PhysicsMaterialCombine.Minimum };
                AssetDatabase.CreateAsset(material, path); Undo.RegisterCreatedObjectUndo(material, "Create cat movement surface");
            }
            foreach (var prefabPath in new[] { PlayerPath, BotPath })
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var collider = root.GetComponent<CapsuleCollider>(); Undo.RecordObject(collider, "Assign movement surface");
                    collider.sharedMaterial = material;
                    var motion = root.GetComponent<CatSecondaryMotion>();
                    if (motion == null) { motion = Undo.AddComponent<CatSecondaryMotion>(root); }
                    Set(motion, "_motor", root.GetComponent<CatLocomotion>()); Set(motion, "_animator", root.GetComponentInChildren<Animator>());
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            var main = SceneManager.GetSceneByPath(ScenePath);
            var alreadyOpen = main.IsValid() && main.isLoaded;
            if (alreadyOpen && main.isDirty) { throw new InvalidOperationException("Main contains unsaved changes; save before removing the review UI."); }
            if (!alreadyOpen) { main = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); }
            try
            {
                foreach (var root in main.GetRootGameObjects())
                { if (root.name == "CharacterReviewUI") { Undo.DestroyObjectImmediate(root); } }
                EditorSceneManager.SaveScene(main);
            }
            finally { if (!alreadyOpen) { EditorSceneManager.CloseScene(main, true); } }
        }

        [MenuItem("Tools/Paw Arena/Combat/Configure Player Combat")]
        public static void ConfigurePlayerCombat()
        {
            var settingsPath = DataPath + "/CatCombat.asset";
            var settings = AssetDatabase.LoadAssetAtPath<CatCombatSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CatCombatSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            AddAbilityStates();
            ConfigurePrefab(PlayerPath, true);
            ConfigurePrefab(BotPath, false);
            AssetDatabase.SaveAssets();

            var main = SceneManager.GetSceneByPath(ScenePath);
            var alreadyOpen = main.IsValid() && main.isLoaded;
            if (alreadyOpen && main.isDirty)
            { throw new InvalidOperationException("Main contains unsaved changes; save before configuring combat."); }
            if (!alreadyOpen) { main = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); }
            try
            {
                var actors = main.GetRootGameObjects().FirstOrDefault(root => root.name == "Actors");
                if (actors == null) { throw new InvalidOperationException("Main is missing its Actors root."); }
                if (actors.transform.Find("CombatTarget") == null)
                {
                    var botPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BotPath);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(botPrefab, main);
                    instance.name = "CombatTarget";
                    instance.transform.SetParent(actors.transform, false);
                    instance.transform.SetPositionAndRotation(new Vector3(0, .03f, 3), Quaternion.Euler(0, 180, 0));
                }
                EditorSceneManager.SaveScene(main);
            }
            finally { if (!alreadyOpen) { EditorSceneManager.CloseScene(main, true); } }

            void ConfigurePrefab(string path, bool isPlayer)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var motor = root.GetComponent<CatLocomotion>();
                    var animation = root.GetComponent<CatAnimationController>();
                    var animator = root.GetComponentInChildren<Animator>();
                    var combatant = root.GetComponent<CatCombatant>() ?? Undo.AddComponent<CatCombatant>(root);
                    Set(combatant, "_settings", settings); Set(combatant, "_motor", motor); Set(combatant, "_isPlayer", isPlayer);
                    CatPlayerCombat playerCombat = null;
                    if (isPlayer)
                    {
                        var effects = root.GetComponent<CatCombatEffects>() ?? Undo.AddComponent<CatCombatEffects>(root);
                        Set(effects, "_settings", settings);
                        playerCombat = root.GetComponent<CatPlayerCombat>() ?? Undo.AddComponent<CatPlayerCombat>(root);
                        Set(playerCombat, "_settings", settings); Set(playerCombat, "_actor", combatant);
                        Set(playerCombat, "_animation", animation); Set(playerCombat, "_effects", effects);
                        var input = root.GetComponent<CatPlayerController>(); Set(input, "_combat", playerCombat);
                        var hud = root.GetComponent<CatCombatHud>() ?? Undo.AddComponent<CatCombatHud>(root);
                        Set(hud, "_actor", combatant); Set(hud, "_combat", playerCombat);
                    }
                    var pose = root.GetComponent<CatCombatPose>() ?? Undo.AddComponent<CatCombatPose>(root);
                    Set(pose, "_actor", combatant); Set(pose, "_playerCombat", playerCombat); Set(pose, "_animator", animator);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void AddAbilityStates()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DataPath + "/CatLocomotion.controller");
            if (controller == null) { throw new InvalidOperationException("Missing CatLocomotion.controller."); }
            var names = new[]
            {
                "Dash", "NineLives_FaceUp", "NineLives_FaceDown", "Hiss", "Pounce_Start", "Pounce_Air", "Pounce_Land",
                "Anchor_Start", "Anchor_Hold", "Anchor_End", "Stomp", "Catnip", "FluffUp", "Hairball", "SharkAttack", "WindShurikens"
            };
            var stateMachine = controller.layers[0].stateMachine;
            for (var i = 0; i < names.Length; i++)
            {
                var clipName = "Cat_Ability_" + names[i];
                if (stateMachine.states.Any(item => item.state.name == clipName)) { continue; }
                var path = "Assets/_Project/Animations/Characters/Cat/Abilities/AN_Cat_Ability_" + names[i] + ".fbx";
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(item => item.name == clipName);
                if (clip == null) { throw new InvalidOperationException("Missing ability clip: " + clipName); }
                var state = stateMachine.AddState(clipName, new Vector3(520 + i % 4 * 180, i / 4 * 80, 0));
                state.motion = clip; state.writeDefaultValues = true; state.iKOnFeet = false;
            }
            EditorUtility.SetDirty(controller);
        }
    }
}
