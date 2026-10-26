using System;
using System.IO;
using PawArena.Gameplay.Bots;
using PawArena.Gameplay.Combat;
using PawArena.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PawArena.Editor.Characters
{
    /// <summary>Short live-physics smoke check for the configured player combat slice.</summary>
    public static class PlayerCombatPlayCheck
    {
        private const string Request = "Library/PlayerCombatPlayCheck.request";
        private const string ActiveKey = "PawArena.PlayerCombatPlayCheck.Active";
        private static CatCombatant _player;
        private static CatCombatant _target;
        private static CatPlayerCombat _combat;
        private static int _stage;
        private static float _stageTime;
        private static float _initialHealth;

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>Runs the live combat smoke check inside the disposable validation project.</summary>
        public static void Run()
        {
            if (!Application.isBatchMode ||
                !Application.dataPath.Replace('\\', '/').EndsWith("/Library/CombatValidationProject/Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Only run in the disposable validation copy.");
            }
            EditorSceneManager.OpenScene(CharacterFoundationBuilder.ScenePath);
            SessionState.SetBool(ActiveKey, true);
            _stage = 0;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            var requestState = File.Exists(Request) ? File.ReadAllText(Request).Trim() : string.Empty;
            var requestIsStale = File.Exists(Request) &&
                DateTime.UtcNow - File.GetLastWriteTimeUtc(Request) > TimeSpan.FromSeconds(20);
            if (!SessionState.GetBool(ActiveKey, false) && requestState == "running")
            {
                if (EditorApplication.isPlaying)
                {
                    SessionState.SetBool(ActiveKey, true);
                    _stage = 0;
                }
                else if (!EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                    (!EditorApplication.isPlayingOrWillChangePlaymode || requestIsStale))
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.ExitPlaymode(); }
                    File.WriteAllText(Request, "test");
                }
                return;
            }
            if (SessionState.GetBool(ActiveKey, false) &&
                !EditorApplication.isPlaying &&
                !EditorApplication.isCompiling &&
                (!EditorApplication.isPlayingOrWillChangePlaymode || requestIsStale))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.ExitPlaymode(); }
                SessionState.SetBool(ActiveKey, false);
                if (requestState == "running")
                { File.WriteAllText(Request, "test"); }
                return;
            }
            if (!SessionState.GetBool(ActiveKey, false))
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                    !File.Exists(Request) || File.ReadAllText(Request).Trim() != "test") { return; }
                if (EditorSceneManager.GetActiveScene().isDirty)
                { File.WriteAllText(Request, "BLOCKED: save the active scene before Play Mode smoke testing"); return; }
                if (EditorSceneManager.GetActiveScene().path != CharacterFoundationBuilder.ScenePath)
                { EditorSceneManager.OpenScene(CharacterFoundationBuilder.ScenePath); }
                File.WriteAllText(Request, "running");
                SessionState.SetBool(ActiveKey, true);
                _stage = 0;
                EditorApplication.EnterPlaymode();
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) { return; }
            try
            {
                if (_player == null) { Initialize(); }
                var elapsed = Time.time - _stageTime;
                // Respawn intentionally grants one second of protection; test contact after it expires.
                if (_stage == 0 && elapsed > 1.1f)
                {
                    _combat.RequestAttack();
                    Next();
                }
                else if (_stage == 1 && elapsed > .35f)
                {
                    Require(_target.Health < _initialHealth, "Punch did not damage the live target.");
                    ResetPair();
                    _combat.SetGrabHeld(true);
                    Next();
                }
                else if (_stage == 2 && elapsed > .4f)
                {
                    Require(_player.Held == _target && _target.Holder == _player, "Grab did not create a live link.");
                    _combat.RequestLift();
                    Next();
                }
                else if (_stage == 3 && elapsed > .75f)
                {
                    Require(_player.Phase == CatCombatPhase.Carrying, "Lift did not enter Carrying.");
                    _combat.RequestAttack();
                    Next();
                }
                else if (_stage == 4 && elapsed > .3f)
                {
                    Require(_player.Held == null && _target.Holder == null, "Throw did not release the target.");
                    Require(_target.Body.linearVelocity.magnitude > 2, "Throw produced no live physics impulse.");
                    _combat.SetGrabHeld(false);
                    ResetPair();
                    _target.SetSpawn(new Vector3(4, .03f, 4), Quaternion.Euler(0, 180, 0));
                    _target.Respawn();
                    _combat.RequestAbility();
                    Next();
                }
                else if (_stage == 5 && elapsed > .25f)
                {
                    Require(_combat.SelectedCooldown > 0, "Dash did not consume cooldown.");
                    var forwardSpeed = Vector3.Dot(_player.Body.linearVelocity, _player.transform.forward);
                    Require(forwardSpeed > 8,
                        $"Dash produced no forward burst: velocity={_player.Body.linearVelocity}, " +
                        $"forward={_player.transform.forward}, forwardSpeed={forwardSpeed:F2}, " +
                        $"phase={_player.Phase}, action={_combat.ActionNormalizedTime:F2}, " +
                        $"movementControl={_player.Motor.MovementControlEnabled}.");
                    Finish("PASS: punch, grab, lift, throw, live impulse, Dash and cooldown");
                }
            }
            catch (Exception exception) { Finish("FAIL: " + exception); }
        }

        private static void Initialize()
        {
            foreach (var actor in UnityEngine.Object.FindObjectsByType<CatCombatant>())
            {
                if (actor.IsPlayer) { _player = actor; }
                else { _target = actor; }
            }
            Require(_player != null && _target != null, "Main must contain one player and one combat target.");
            _combat = _player.GetComponent<CatPlayerCombat>();
            Require(_combat != null, "Player combat component is missing.");
            var input = _player.GetComponent<CatPlayerController>(); if (input != null) { input.enabled = false; }
            var bot = _target.GetComponent<CatBotController>(); if (bot != null) { bot.enabled = false; }
            ResetPair();
            _initialHealth = _target.Health;
            _stageTime = Time.time;
        }

        private static void ResetPair()
        {
            _player.SetSpawn(new Vector3(0, .03f, 0), Quaternion.identity);
            _target.SetSpawn(new Vector3(0, .03f, 1.2f), Quaternion.Euler(0, 180, 0));
            _player.Respawn();
            _target.Respawn();
        }

        private static void Next() { _stage++; _stageTime = Time.time; }

        private static void Require(bool condition, string message)
        {
            if (!condition) { throw new InvalidOperationException(message); }
        }

        private static void Finish(string result)
        {
            SessionState.SetBool(ActiveKey, false);
            _player = _target = null;
            _combat = null;
            if (Application.isBatchMode)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/PlayerCombatPlayCheck.txt", result);
                EditorApplication.Exit(result.StartsWith("PASS", StringComparison.Ordinal) ? 0 : 1);
                return;
            }
            File.WriteAllText(Request, result);
            if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); }
        }
    }
}
