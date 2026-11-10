using PawArena.Gameplay.Cameras;
using PawArena.Gameplay.Flow;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PawArena.Editor
{
    /// <summary>Creates the standard four-view Cinemachine rig for scene authoring tools.</summary>
    internal static class CinemachineMainCameraFactory
    {
        private const string CameraConfigFolder = "Assets/_Project/Settings/Cameras";
        private const string CameraConfigPath = CameraConfigFolder + "/CameraSystemConfig.asset";

        public static CinemachineCameraDirector Create(
            Scene scene,
            Camera output,
            Transform player,
            GameFlowController gameFlow)
        {
            var config = LoadOrCreateConfig();
            var brain = output.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = output.gameObject.AddComponent<CinemachineBrain>();
            }

            brain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                .35f);

            var root = new GameObject("Camera System");
            SceneManager.MoveGameObjectToScene(root, scene);
            var pivot = new GameObject("Camera Aim Pivot").transform;
            pivot.SetParent(root.transform, false);
            var aim = root.AddComponent<CameraAimController>();
            Assign(aim, "_pivot", pivot);
            aim.Follow(player);
            aim.ApplySystemConfig(config);
            aim.ApplyPreset(config.Lobby);

            var lobby = CreateThirdPerson(
                root.transform, "CM_LobbyFollow", pivot, 45, 4.5f,
                new Vector3(.15f, .35f, .25f), new Vector3(.35f, .15f, 0), .25f, .55f, false);
            var match = CreateThirdPerson(
                root.transform, "CM_MatchFollow", pivot, 58, 6.5f,
                new Vector3(.08f, .18f, .12f), Vector3.zero, .2f, .5f, true);
            var spectator = CreateThirdPerson(
                root.transform, "CM_SpectatorFollow", pivot, 60, 7,
                new Vector3(.25f, .45f, .35f), Vector3.zero, .3f, .5f, true);
            var results = CreateCamera(root.transform, "CM_ResultsWinner", 40);
            results.Camera.Follow = player;
            results.Camera.LookAt = player;
            var orbit = results.GameObject.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = 4.25f;
            orbit.TargetOffset = Vector3.up * 1.05f;
            var verticalAxis = orbit.VerticalAxis;
            verticalAxis.Value = 14;
            verticalAxis.Range = new Vector2(-10, 45);
            verticalAxis.Wrap = false;
            orbit.VerticalAxis = verticalAxis;
            var composer = results.GameObject.AddComponent<CinemachineRotationComposer>();
            composer.TargetOffset = Vector3.up * 1.05f;
            composer.Damping = new Vector2(.35f, .35f);

            var director = root.AddComponent<CinemachineCameraDirector>();
            Assign(director, "_config", config);
            Assign(director, "_outputCamera", output);
            Assign(director, "_aim", aim);
            Assign(director, "_gameFlow", gameFlow);
            Assign(director, "_lobbyCamera", lobby.Camera);
            Assign(director, "_matchCamera", match.Camera);
            Assign(director, "_spectatorCamera", spectator.Camera);
            Assign(director, "_resultsCamera", results.Camera);
            Assign(director, "_lobbyFollow", lobby.Follow);
            Assign(director, "_matchFollow", match.Follow);
            Assign(director, "_spectatorFollow", spectator.Follow);
            Assign(director, "_resultsOrbit", orbit);

            lobby.GameObject.SetActive(true);
            match.GameObject.SetActive(false);
            spectator.GameObject.SetActive(false);
            results.GameObject.SetActive(false);
            return director;
        }

        private static CameraSystemConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<CameraSystemConfig>(CameraConfigPath);
            if (config != null)
            {
                return config;
            }

            if (!AssetDatabase.IsValidFolder(CameraConfigFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Settings", "Cameras");
            }

            config = ScriptableObject.CreateInstance<CameraSystemConfig>();
            AssetDatabase.CreateAsset(config, CameraConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static CameraSetup CreateThirdPerson(
            Transform parent,
            string name,
            Transform pivot,
            float fieldOfView,
            float distance,
            Vector3 damping,
            Vector3 shoulderOffset,
            float verticalArmLength,
            float cameraSide,
            bool addImpulseListener)
        {
            var setup = CreateCamera(parent, name, fieldOfView);
            setup.Camera.Follow = pivot;
            var follow = setup.GameObject.AddComponent<CinemachineThirdPersonFollow>();
            follow.Damping = damping;
            follow.ShoulderOffset = shoulderOffset;
            follow.VerticalArmLength = verticalArmLength;
            follow.CameraSide = cameraSide;
            follow.CameraDistance = distance;
            var obstacles = follow.AvoidObstacles;
            obstacles.Enabled = true;
            obstacles.CollisionFilter = ResolveEnvironmentMask();
            obstacles.IgnoreTag = string.Empty;
            obstacles.CameraRadius = .12f;
            obstacles.DampingIntoCollision = 0;
            obstacles.DampingFromCollision = .35f;
            follow.AvoidObstacles = obstacles;
            if (addImpulseListener)
            {
                setup.GameObject.AddComponent<CinemachineImpulseListener>();
            }

            setup.Follow = follow;
            return setup;
        }

        private static CameraSetup CreateCamera(Transform parent, string name, float fieldOfView)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var camera = gameObject.AddComponent<CinemachineCamera>();
            camera.Priority = 10;
            var lens = camera.Lens;
            lens.FieldOfView = fieldOfView;
            lens.NearClipPlane = .05f;
            lens.FarClipPlane = 150;
            camera.Lens = lens;
            return new CameraSetup(gameObject, camera);
        }

        private static LayerMask ResolveEnvironmentMask()
        {
            var mask = LayerMask.GetMask("Default", "Ground", "Environment");
            return mask != 0 ? mask : 1;
        }

        private static void Assign(Object target, string fieldName, Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(fieldName).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private sealed class CameraSetup
        {
            public CameraSetup(GameObject gameObject, CinemachineCamera camera)
            {
                GameObject = gameObject;
                Camera = camera;
            }

            public GameObject GameObject { get; }
            public CinemachineCamera Camera { get; }
            public CinemachineThirdPersonFollow Follow { get; set; }
        }
    }
}
