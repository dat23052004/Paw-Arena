using System;
using System.Collections.Generic;
using System.IO;
using PawArena.Core.Flow;
using PawArena.Gameplay.Bots;
using PawArena.Gameplay.Characters;
using PawArena.Gameplay.Combat;
using PawArena.Gameplay.Flow;
using PawArena.Gameplay.Maps;
using PawArena.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PawArena.Editor.Characters
{
    /// <summary>Real Play Mode input/clip/UI checks. Only runs in the disposable validation project.</summary>
    public static class CharacterFoundationPlayCheck
    {
        private const string Active = "PawArena.CharacterFoundation.PlayCheck";
        private static readonly List<string> Errors = new();
        private static readonly List<string> EditorIssues = new();
        private static CatPlayerController _player;
        private static CatLocomotion _motor;
        private static CatAnimationController _animation;
        private static Keyboard _keyboard;
        private static Mouse _mouse;
        private static int _stage, _clip;
        private static float _stageTime;
        private static double _started;
        private static Vector3 _start;
        private static bool _airSeen;
        private static readonly HashSet<string> Seen = new();
        private static Camera _camera;
        private static CharacterReviewCamera _orbit;
        private static float _zoomBefore;
        private static Vector3 _viewBefore;
        private static Quaternion _headDown;
        private static float _turnDeflection;
        private static bool _smoothTurnObserved;
        private static float _partyTargetHealth;
        private static readonly List<string> TurnSamples = new();

        [InitializeOnLoadMethod] private static void Register()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        public static void Run()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').EndsWith("/Library/CombatValidationProject/Assets", StringComparison.Ordinal))
            { throw new InvalidOperationException("Only run in the disposable validation copy."); }
            SessionState.SetBool(Active, true);
            EditorSceneManager.OpenScene(CharacterFoundationBuilder.ScenePath); EditorApplication.EnterPlaymode();
        }
        private static void Log(string message, string trace, LogType type)
        {
            if (!SessionState.GetBool(Active, false) || type is not (LogType.Error or LogType.Exception or LogType.Assert)) { return; }
            if (trace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")) { EditorIssues.Add(message + "\n" + trace); }
            else { Errors.Add(message + "\n" + trace); }
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) { return; }
            try
            {
                if (_player == null)
                {
                    _player = Object.FindAnyObjectByType<CatPlayerController>(); if (_player == null) { return; }
                    _started = EditorApplication.timeSinceStartup; _motor = _player.GetComponent<CatLocomotion>();
                    var combatTarget = Object.FindAnyObjectByType<CatBotController>()?.GetComponent<CatCombatant>();
                    if (combatTarget != null)
                    {
                        combatTarget.SetSpawn(new Vector3(8, .03f, 8), Quaternion.Euler(0, 180, 0));
                        combatTarget.Respawn();
                    }
                    // Batch rendering can stall for shader compilation; keep input stages observable across frames.
                    Time.maximumDeltaTime = 1f / 30;
                    _animation = _player.GetComponent<CatAnimationController>(); _stageTime = Time.time;
                    _camera = Camera.main; _orbit = _camera.GetComponent<CharacterReviewCamera>();
                    _player.SetControlMode(CatPlayerControlMode.Lobby);
                    InputSystem.settings = Object.Instantiate(InputSystem.settings);
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    _keyboard = InputSystem.AddDevice<Keyboard>(); _mouse = InputSystem.AddDevice<Mouse>();
                    InputSystem.QueueStateEvent(_mouse, new MouseState { position = new Vector2(Screen.width * .8f, Screen.height * .5f) });
                }
                if (Errors.Count > 0) { Finish(false); return; }
                Require(EditorApplication.timeSinceStartup - _started < 100, "Timed out");
                _player.SendMessage("OnApplicationFocus", true);
                _orbit.SendMessage("OnApplicationFocus", true);
                Seen.Add(_animation.CurrentClip);
                Require(_animation.CurrentClip != "Cat_Walk", "Walk is still reachable");
                Require(SceneManager.sceneCount == 1, "Unexpected runtime scene load");
                Require(Object.FindObjectsByType<CatPlayerController>().Length == 1, "Player was duplicated");
                Require(Object.FindObjectsByType<CatBotController>().Length == 1, "Expected one idle combat target");
                Require(Object.FindObjectsByType<UIDocument>().Length == 0, "HUD must not exist");
                var elapsed = Time.time - _stageTime;
                var cameraOffset = Vector3.ProjectOnPlane(_camera.transform.position - _player.transform.position, Vector3.up).normalized;
                if (_player.ControlMode == CatPlayerControlMode.Lobby)
                { Require(Vector3.Dot(cameraOffset, _player.transform.forward) < -.999f, "Camera left the rear of the player"); }
                if (_stage == 0 && elapsed > .6f)
                {
                    Require(_motor.Grounded, "Player failed to settle on test map");
                    Require(Object.FindObjectsByType<MapSpawnPoints>().Length == 1, "Expected exactly one map prefab");
                    _start = _player.transform.position; InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W)); Next();
                }
                else if (_stage == 1 && elapsed > .7f)
                {
                    Require(Vector3.Distance(_start, _player.transform.position) > .7f, "W input did not move player");
                    Require(_motor.PlanarSpeed > 5.8f && _animation.CurrentClip == "Cat_Run", "W did not auto-run without Shift");
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W)); Next();
                }
                else if (_stage == 2 && elapsed > .7f)
                {
                    Require(_motor.PlanarSpeed > 5.8f && _motor.PlanarSpeed < 6.1f && _animation.CurrentClip == "Cat_Run", "Auto-run speed changed unexpectedly");
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); Next();
                }
                else if (_stage == 3 && elapsed > .5f)
                {
                    Require(_motor.PlanarSpeed < .05f && _animation.CurrentClip == "Cat_Idle", "Stop/idle failed");
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space)); Next();
                }
                else if (_stage == 4)
                {
                    if (elapsed > .05f) { InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); }
                    if (!_motor.Grounded && _player.transform.position.y > .2f) { _airSeen = true; }
                    if (elapsed < 1.7f) { return; }
                    Require(_airSeen && _motor.Grounded, "Space jump failed");
                    Require(Seen.Contains("Cat_Jump_Air"), "Missing minimal airborne pose");
                    _player.PreviewClip(0); _start = _player.transform.position; Next();
                }
                else if (_stage == 5 && elapsed > .12f)
                {
                    Require(_motor.Suspended && _motor.Body.isKinematic, "Preview did not lock travel");
                    Require(Vector3.Distance(_start, _player.transform.position) < .01f, "In-place preview drifted");
                    _player.Seek(.45f); var time = _player.NormalizedTime;
                    Require(_player.IsPaused && Mathf.Abs(time - .45f) < .001f, "Scrub failed");
                    _clip++;
                    if (_clip < CatAnimationController.PreviewClipCount) { _player.PreviewClip(_clip); _stageTime = Time.time; }
                    else { _player.ResetPlayer(); Next(); }
                }
                else if (_stage == 6 && elapsed > .5f)
                {
                    Require(!_player.IsPreviewing && !_motor.Suspended && _motor.Grounded, "Reset did not restore live movement");
                    Require(_player.GetComponent<CatSecondaryMotion>().IsReady, "Missing soft motion rig");
                    _start = _player.transform.position; DragToPitch(61); Next();
                }
                else if (_stage == 7 && elapsed > 1)
                {
                    _headDown = _animation.Animator.GetBoneTransform(HumanBodyBones.Head).rotation;
                    Require(_orbit.Pitch > 55, "RMB drag did not tilt camera down");
                    Require(Vector3.Distance(_start, _player.transform.position) < .02f, "Camera orbit moved the actor");
                    Capture("Character_LookDown");
                    DragToPitch(-20); Next();
                }
                else if (_stage == 8 && elapsed > 1)
                {
                    Require(_orbit.Pitch < -15, "RMB drag did not tilt camera up");
                    Require(_camera.transform.position.y > .12f, "Camera clipped through test floor");
                    Require(Quaternion.Angle(_headDown, _animation.Animator.GetBoneTransform(HumanBodyBones.Head).rotation) > 30, "Look/lean did not follow camera pitch");
                    Capture("Character_LookUp");
                    _zoomBefore = _orbit.Distance;
                    // The authored review scene starts at the near limit, so exercise zoom-out first.
                    InputSystem.QueueStateEvent(_mouse, new MouseState { scroll = new Vector2(0, -240) }); Next();
                }
                else if (_stage == 9 && elapsed > .8f)
                {
                    Require(_orbit.Distance > _zoomBefore + .9f, "Scroll did not zoom out");
                    Require(Mathf.Abs(_orbit.Pitch + 20) < .1f, "Releasing RMB reset the viewing angle");
                    Require(UnityEngine.Cursor.lockState != CursorLockMode.Locked, "Cursor was not released with RMB");
                    Capture("CharacterFoundation"); _start = _player.transform.position; DragToPitch(16);
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W)); Next();
                }
                else if (_stage == 10 && elapsed > .65f)
                {
                    Require(Vector3.Distance(_start, _player.transform.position) > .7f, "Holding RMB blocked WASD movement");
                    Require(_animation.CurrentClip == "Cat_Carry_Move", "Moving with RMB did not use the dedicated grab movement animation");
                    Capture("Character_Run");
                    InputSystem.QueueStateEvent(_mouse, new MouseState());
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.D)); Next();
                }
                else if (_stage == 11)
                {
                    var lean = _player.GetComponent<CatSecondaryMotion>().LeanDegrees;
                    Require(Mathf.Abs(lean.y) < .001f && Mathf.Abs(lean.z) < .001f, "Strafe bent or twisted the torso");
                    _turnDeflection = Mathf.Max(_turnDeflection, _player.GetComponent<CatSecondaryMotion>().ArmDeflection.magnitude);
                    if (TurnSamples.Count == 0 || elapsed > TurnSamples.Count * .05f)
                    { TurnSamples.Add($"t={elapsed:F3} dt={Time.deltaTime:F4} velocity={_motor.Body.linearVelocity} yaw={_player.transform.eulerAngles.y:F1} arms={_player.GetComponent<CatSecondaryMotion>().ArmDeflection}"); }
                    if (elapsed < .65f) { return; }
                    Capture("Character_Turn");
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); Next();
                }
                else if (_stage == 12 && elapsed > 1.5f)
                {
                    _zoomBefore = _orbit.Distance; _viewBefore = _camera.transform.forward;
                    InputSystem.QueueStateEvent(_mouse, new MouseState { delta = new Vector2(500, -200), scroll = new Vector2(0, 240) }); Next();
                }
                else if (_stage == 13 && elapsed > .3f)
                {
                    Require(_orbit.Distance < _zoomBefore - .9f, "Scroll did not zoom in");
                    Require(Vector3.Angle(_viewBefore, _camera.transform.forward) < .1f, "Mouse movement without RMB rotated camera");
                    InputSystem.QueueStateEvent(_mouse, new MouseState { scroll = new Vector2(0, 30000) }); Next();
                }
                else if (_stage == 14 && elapsed > .3f)
                {
                    Require(Mathf.Abs(_orbit.Distance - 2) < .01f, "Near zoom limit failed");
                    InputSystem.QueueStateEvent(_mouse, new MouseState { scroll = new Vector2(0, -30000) }); Next();
                }
                else if (_stage == 15 && elapsed > .3f)
                {
                    Require(Mathf.Abs(_orbit.Distance - 18) < .01f, "Far zoom limit failed");
                    _start = _player.transform.position;
                    InputSystem.QueueStateEvent(_mouse, new MouseState { buttons = 2, delta = new Vector2(500, 0) }); Next();
                }
                else if (_stage == 16)
                {
                    var turned = Mathf.Abs(Mathf.DeltaAngle(0, _player.transform.eulerAngles.y));
                    if (elapsed > .02f && elapsed < .2f && turned > .1f && turned < 70) { _smoothTurnObserved = true; }
                    if (elapsed < 1.1f) { return; }
                    Require(_smoothTurnObserved, "RMB heading snapped instead of turning progressively");
                    Require(Vector3.Distance(_start, _player.transform.position) < .02f, "Idle camera turn moved the actor");
                    Require(Vector3.Angle(_player.transform.forward, Vector3.right) < 1, "Player did not turn with RMB camera heading");
                    InputSystem.QueueStateEvent(_mouse, new MouseState());
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.S)); Next();
                }
                else if (_stage == 17 && elapsed > .7f)
                {
                    Require(Vector3.Dot(_motor.Body.linearVelocity, _player.transform.forward) < -1.8f, "S did not backpedal");
                    Require(Vector3.Angle(_player.transform.forward, Vector3.right) < 1, "Backpedalling turned actor toward camera");
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.A)); Next();
                }
                else if (_stage == 18 && elapsed > .7f)
                {
                    Require(Vector3.Dot(_motor.Body.linearVelocity, _player.transform.right) < -1.8f, "A did not strafe");
                    Require(Vector3.Angle(_player.transform.forward, Vector3.right) < 1, "Strafing changed actor heading");
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); _player.ResetPlayer(); Next();
                }
                else if (_stage == 19 && elapsed > .5f)
                {
                    Require(Vector3.Angle(_orbit.PlanarForward, _player.transform.forward) < 1, "Reset left a stale camera heading");
                    Capture("Character_RearCamera");
                    DragToPitch(200); Next();
                }
                else if (_stage == 20 && elapsed > .3f)
                {
                    Require(Mathf.Abs(_orbit.Pitch - 80) < .01f, "Upper pitch limit failed");
                    DragToPitch(-200); Next();
                }
                else if (_stage == 21 && elapsed > .3f)
                {
                    Require(Mathf.Abs(_orbit.Pitch + 35) < .01f, "Lower pitch limit failed");
                    var partyTarget = Object.FindAnyObjectByType<CatBotController>()?.GetComponent<CatCombatant>();
                    Require(partyTarget != null, "Party mouse-look test lost its combat target");
                    var targetPosition = _player.transform.position + Vector3.right * 1.2f;
                    partyTarget.Motor.Warp(targetPosition, Quaternion.Euler(0, -90, 0));
                    partyTarget.SetSpawn(targetPosition, Quaternion.Euler(0, -90, 0));
                    partyTarget.SetPhase(CatCombatPhase.Free);
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.L));
                    Next();
                }
                else if (_stage == 22 && elapsed > .3f)
                {
                    Require(_player.ControlMode == CatPlayerControlMode.PartyMatch && _orbit.IsPartyMatchMode,
                        "L did not switch Lobby to Party Match");
                    Require(Object.FindAnyObjectByType<GameFlowController>()?.CurrentState == GameFlowState.Playing,
                        "L did not advance the game flow from Lobby to Playing");
                    Require(!_mouse.leftButton.isPressed && !_mouse.rightButton.isPressed, "Party mouse-look unexpectedly held a mouse button");
                    Require(UnityEngine.Cursor.lockState == CursorLockMode.Locked, "Party Match did not own the pointer for continuous mouse-look");
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    InputSystem.QueueStateEvent(_mouse, new MouseState { delta = new Vector2(500, 0) });
                    Next();
                }
                else if (_stage == 23 && elapsed > 1f)
                {
                    Require(Vector3.Angle(_player.transform.forward, Vector3.right) < 2, "Player did not automatically follow Party mouse movement");
                    var partyCameraOffset = Vector3.ProjectOnPlane(_camera.transform.position - _player.transform.position, Vector3.up).normalized;
                    Require(Vector3.Dot(partyCameraOffset, _player.transform.forward) < -.995f, "Party camera did not rotate behind the player");
                    var partyTarget = Object.FindAnyObjectByType<CatBotController>()?.GetComponent<CatCombatant>();
                    Require(partyTarget != null, "Party attack-direction test lost its combat target");
                    _partyTargetHealth = partyTarget.Health;
                    InputSystem.QueueStateEvent(_mouse, new MouseState { buttons = 1 });
                    Next();
                }
                else if (_stage == 24 && elapsed > .4f)
                {
                    var partyTarget = Object.FindAnyObjectByType<CatBotController>()?.GetComponent<CatCombatant>();
                    Require(partyTarget != null && partyTarget.Health < _partyTargetHealth, "Party attack did not follow the automatic mouse direction");
                    InputSystem.QueueStateEvent(_mouse, new MouseState());
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.L));
                    Next();
                }
                else if (_stage == 25 && elapsed > .3f)
                {
                    Require(_player.ControlMode == CatPlayerControlMode.Lobby && !_orbit.IsPartyMatchMode,
                        "Second L press did not return Party Match to Lobby");
                    Require(Object.FindAnyObjectByType<GameFlowController>()?.CurrentState == GameFlowState.Lobby,
                        "Second L press did not return the game flow to Lobby");
                    Require(UnityEngine.Cursor.lockState != CursorLockMode.Locked, "Returning to Lobby did not release the pointer");
                    Finish(true);
                }
            }
            catch (Exception error) { Errors.Add(error.ToString()); Finish(false); }
        }
        private static void Next() { _stage++; _stageTime = Time.time; }
        private static void DragToPitch(float pitch)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { buttons = 2, delta = new Vector2(0, (_orbit.Pitch - pitch) / .18f) });
        }
        private static void ValidateArm()
        {
            var rig = _animation.Animator;
            var arm = (rig.GetBoneTransform(HumanBodyBones.LeftHand).position - rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position).normalized;
            Require(Vector3.Dot(arm, Vector3.down) > .75f, "Arms did not hang toward gravity");
        }
        private static void Capture(string name)
        {
            _camera = Camera.main; var target = new RenderTexture(1280, 800, 24);
            var old = RenderTexture.active; var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(_camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = old; Object.Destroy(pixels); target.Release(); Object.Destroy(target); }
        }
        private static void Require(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
        private static void Finish(bool passed)
        {
            SessionState.SetBool(Active, false); Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/CharacterFoundationPlayCheck.txt", (passed ? "PASS" : "FAIL") +
                "\nSingle scene / one player plus idle combat target / W auto-run without Shift / minimal Idle-Run-JumpAir locomotion / Space jump / 3 in-place previews / scrub / reset / camera-driven look / RMB orbit and release / scroll zoom both directions and limits / pitch limits -35..80 / floor collision.\n" +
                "Rear camera: checked in Lobby and Party Match. Lobby uses RMB orbit; L toggles Party continuous mouse-look, automatic player facing and camera rotation behind the player without a held mouse button.\n" +
                "Observed clips: " + string.Join(", ", Seen) + "\n" + string.Join("\n", Errors) + "\nTurn samples:\n" + string.Join("\n", TurnSamples) + "\nSeparate Editor issues: " + EditorIssues.Count + "\n" + string.Join("\n", EditorIssues));
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
