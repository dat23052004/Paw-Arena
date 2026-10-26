using System.IO;
using System.Linq;
using NUnit.Framework;
using PawArena.Core.Flow;
using PawArena.Gameplay.Characters;
using PawArena.Gameplay.Combat;
using PawArena.Gameplay.Flow;
using PawArena.Gameplay.Player;
using PawArena.Gameplay.Bots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PawArena.Tests.Characters
{
    /// <summary>Standalone character prefabs in their own physics scene, with no combat world.</summary>
    public sealed class CharacterFoundationTests
    {
        private Scene _scene;
        private GameObject _player, _floor;
        private CatLocomotion _motor;
        private CatPlayerController _input;
        private CatAnimationController _animation;
        private CatCombatant _combatant;
        private CatPlayerCombat _combat;
        [SetUp] public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(_floor, _scene);
            _floor.transform.position = new Vector3(0, -.25f, 0); _floor.transform.localScale = new Vector3(40, .5f, 40);
            _player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PF_Player.prefab"), _scene);
            _motor = _player.GetComponent<CatLocomotion>(); _motor.Initialize();
            _input = _player.GetComponent<CatPlayerController>(); _input.enabled = false;
            _animation = _player.GetComponent<CatAnimationController>(); _animation.Initialize();
            _combatant = _player.GetComponent<CatCombatant>(); _combatant.Initialize();
            _combat = _player.GetComponent<CatPlayerCombat>(); _combat.Initialize();
            _motor.Warp(Vector3.up * .03f, Quaternion.identity); _input.SetHome(Vector3.up * .03f, Quaternion.identity); Tick(.3f);
        }
        [TearDown] public void TearDown() { if (_scene.IsValid()) { EditorSceneManager.ClosePreviewScene(_scene); } }
        private void Tick(float seconds)
        {
            for (var i = 0; i < Mathf.CeilToInt(seconds / .02f); i++)
            { _motor.Step(.02f); _scene.GetPhysicsScene().Simulate(.02f); _animation.Present(.02f); _animation.Animator.Update(.02f); }
        }
        [Test] public void Prefabs_PlayerAndBotHaveSeparateOwnersAndSharedCombatTargetRules()
        {
            var bot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Bots/PF_Bot.prefab"), _scene);
            Assert.That(bot.GetComponent<CatBotController>(), Is.Not.Null); Assert.That(bot.GetComponent<CatPlayerController>(), Is.Null);
            Assert.That(_player.GetComponent<CatPlayerController>(), Is.Not.Null); Assert.That(_player.GetComponent<CatBotController>(), Is.Null);
            foreach (var root in new[] { _player, bot })
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                { Assert.That(component, Is.Not.Null); }
                Assert.That(root.GetComponent<CatCombatant>(), Is.Not.Null);
                Assert.That(root.GetComponentInChildren<Animator>().isHuman, Is.True);
            }
        }
        [Test] public void IndependentFacing_StrafeAndReverseDoNotTurnAwayFromView()
        {
            _motor.SubmitFacing(Vector3.forward); _motor.Submit(Vector3.right, false); Tick(.5f);
            Assert.That(_motor.Body.linearVelocity.x, Is.GreaterThan(1.8f));
            Assert.That(Quaternion.Angle(_motor.Body.rotation, Quaternion.identity), Is.LessThan(.1f));
            _motor.Submit(Vector3.back, false); Tick(.5f);
            Assert.That(_motor.Body.linearVelocity.z, Is.LessThan(-1.8f));
            Assert.That(Quaternion.Angle(_motor.Body.rotation, Quaternion.identity), Is.LessThan(.1f));
        }
        [Test] public void IndependentFacing_IdleTurnDoesNotTranslateAndClearRestoresMovementFacing()
        {
            var position = _motor.Body.position;
            _motor.SubmitFacing(Vector3.right); Tick(1.5f);
            Assert.That(Vector3.Distance(position, _motor.Body.position), Is.LessThan(.02f));
            Assert.That(Vector3.Angle(_motor.Body.rotation * Vector3.forward, Vector3.right), Is.LessThan(.1f));
            _motor.ClearInput(); _motor.Submit(Vector3.back, false); Tick(1.5f);
            Assert.That(Vector3.Angle(_motor.Body.rotation * Vector3.forward, Vector3.back), Is.LessThan(.1f));
        }
        [Test] public void Facing_SmallMouseTurnEasesOverSeveralStepsWithoutOvershoot()
        {
            var target = Quaternion.Euler(0, 5, 0);
            _motor.SubmitFacing(target * Vector3.forward); Tick(.06f);
            var first = Quaternion.Angle(Quaternion.identity, _motor.Body.rotation);
            Assert.That(first, Is.GreaterThan(0).And.LessThan(5), "Small mouse input must build angular speed instead of snapping.");
            var previousError = Quaternion.Angle(_motor.Body.rotation, target);
            for (var i = 0; i < 50; i++)
            {
                Tick(.02f);
                var error = Quaternion.Angle(_motor.Body.rotation, target);
                Assert.That(error, Is.LessThanOrEqualTo(previousError + .01f));
                Assert.That(Mathf.DeltaAngle(0, _motor.Body.rotation.eulerAngles.y), Is.InRange(0, 5.01f));
                previousError = error;
            }
            Assert.That(previousError, Is.LessThan(.1f));
        }
        [Test] public void Facing_SlowMouseTurnBuildsAngularSpeedWithWeight()
        {
            var previousYaw = 0f; var firstStep = 0f; var maximumStep = 0f;
            for (var i = 1; i <= 30; i++)
            {
                var targetYaw = i * 2f;
                _motor.SubmitFacing(Quaternion.Euler(0, targetYaw, 0) * Vector3.forward, 100);
                Tick(.02f);
                var yaw = _motor.Body.rotation.eulerAngles.y;
                var step = Mathf.DeltaAngle(previousYaw, yaw);
                if (i == 1) { firstStep = step; }
                maximumStep = Mathf.Max(maximumStep, step);
                previousYaw = yaw;
            }
            Assert.That(firstStep, Is.GreaterThan(0).And.LessThan(1), "Turn must build angular speed instead of snapping.");
            Assert.That(maximumStep, Is.GreaterThan(firstStep + 1), "Turn acceleration must be visible over successive physics steps.");
            Assert.That(Mathf.DeltaAngle(previousYaw, 60), Is.Zero.Within(.1f));
        }
        [Test] public void Facing_FastMouseTurnAcceleratesUntilItCatchesLatestTarget()
        {
            const float targetYaw = 120;
            _motor.SubmitFacing(Quaternion.Euler(0, targetYaw, 0) * Vector3.forward, 1000);
            _motor.Step(.02f); _scene.GetPhysicsScene().Simulate(.02f);
            var firstYaw = _motor.Body.rotation.eulerAngles.y;
            Assert.That(firstYaw, Is.GreaterThan(0).And.LessThan(targetYaw));
            _motor.SubmitFacing(Quaternion.Euler(0, targetYaw, 0) * Vector3.forward, 0);
            var previousYaw = firstYaw;
            var accelerated = false;
            for (var i = 0; i < 75; i++)
            {
                _motor.Step(.02f); _scene.GetPhysicsScene().Simulate(.02f);
                var yaw = _motor.Body.rotation.eulerAngles.y;
                if (Mathf.DeltaAngle(previousYaw, yaw) > _motor.Settings.TurnSpeed * .02f + .05f) { accelerated = true; }
                previousYaw = yaw;
            }
            Assert.That(accelerated, Is.True, "A fast RMB change must unlock the higher catch-up speed.");
            Assert.That(Mathf.DeltaAngle(previousYaw, targetYaw), Is.Zero.Within(.1f));
        }
        [Test] public void Facing_WrapsByShortestPathAndWarpClearsAngularMomentum()
        {
            _motor.Warp(Vector3.up * .03f, Quaternion.Euler(0, 350, 0));
            _motor.SubmitFacing(Quaternion.Euler(0, 10, 0) * Vector3.forward); Tick(.1f);
            Assert.That(Mathf.DeltaAngle(350, _motor.Body.rotation.eulerAngles.y), Is.GreaterThan(0).And.LessThan(20));
            _motor.Warp(Vector3.up * .03f, Quaternion.identity);
            _motor.SubmitFacing(Vector3.forward); Tick(.1f);
            Assert.That(Quaternion.Angle(_motor.Body.rotation, Quaternion.identity), Is.LessThan(.01f));
        }
        [Test] public void SoftMotion_LateralAccelerationAndTurningDoNotBendOrTwistSpine()
        {
            var motion = _player.GetComponent<CatSecondaryMotion>(); motion.Initialize(); motion.SamplePhysics(.02f);
            _motor.Body.linearVelocity = Vector3.right * 6;
            _motor.Body.rotation = Quaternion.Euler(0, 8, 0); motion.SamplePhysics(.02f);
            for (var i = 0; i < 30; i++) { motion.Present(.02f); }
            Assert.That(motion.LeanDegrees.y, Is.Zero.Within(.0001f), "Turning must not twist the spine.");
            Assert.That(motion.LeanDegrees.z, Is.Zero.Within(.0001f), "Strafing must not bend the spine sideways.");
            Assert.That(motion.ArmDeflection.magnitude, Is.GreaterThan(.08f), "Limb inertia should remain active.");
        }
        [Test] public void SoftMotion_StrafeRemovesAuthoredTorsoSwayAndRestorePreservesClip()
        {
            var motion = _player.GetComponent<CatSecondaryMotion>(); motion.Initialize();
            var spine = _animation.Animator.GetBoneTransform(HumanBodyBones.Spine);
            var chest = _animation.Animator.GetBoneTransform(HumanBodyBones.Chest);
            var spineReference = spine.rotation; var chestReference = chest.rotation;
            spine.rotation = Quaternion.Euler(0, 10, 15) * spine.rotation;
            var authoredSpine = spine.rotation; var authoredChest = chest.rotation;
            _motor.Body.linearVelocity = Vector3.right * 6;
            for (var i = 0; i < 30; i++) { motion.Present(.02f); }
            var spineOffset = spine.rotation * Quaternion.Inverse(spineReference);
            var chestOffset = chest.rotation * Quaternion.Inverse(chestReference);
            Assert.That(Mathf.Abs((spineOffset * Vector3.up).x), Is.LessThan(.001f));
            Assert.That(Mathf.Abs((spineOffset * Vector3.forward).x), Is.LessThan(.001f));
            Assert.That(Mathf.Abs((chestOffset * Vector3.up).x), Is.LessThan(.001f));
            motion.RestorePose();
            Assert.That(Quaternion.Angle(spine.rotation, authoredSpine), Is.LessThan(.05f));
            Assert.That(Quaternion.Angle(chest.rotation, authoredChest), Is.LessThan(.05f));
        }
        [Test] public void GroundProbe_DoesNotTreatOwnCapsuleAsGround()
        {
            Assert.That(_motor.Grounded, Is.True);
            _motor.Warp(Vector3.up * 3, Quaternion.identity); _motor.Step(.02f);
            Assert.That(_motor.Grounded, Is.False);
        }
        [Test] public void Movement_AlwaysUsesRunSpeedAndStopsInIdle()
        {
            _motor.Submit(Vector3.forward, false); Tick(1);
            Assert.That(_motor.PlanarSpeed, Is.EqualTo(6).Within(.1f)); Assert.That(_animation.CurrentClip, Is.EqualTo("Cat_Run"));
            _motor.ClearInput(); Tick(.5f);
            Assert.That(_motor.PlanarSpeed, Is.LessThan(.01f)); Assert.That(_animation.CurrentClip, Is.EqualTo("Cat_Idle"));
        }
        [Test] public void DiagonalInput_DoesNotExceedRunSpeed()
        {
            _motor.Submit(new Vector3(1, 0, 1), false); Tick(1);
            Assert.That(_motor.PlanarSpeed, Is.EqualTo(6).Within(.1f));
        }
        [Test] public void Jump_FliesThenLandsAndCannotDoubleJump()
        {
            _motor.Submit(Vector3.zero, true); Tick(.12f);
            Assert.That(_motor.Grounded, Is.False); Assert.That(_motor.Body.position.y, Is.GreaterThan(.3f));
            Assert.That(_animation.CurrentClip, Is.EqualTo("Cat_Jump_Air"));
            var speed = _motor.Body.linearVelocity.y;
            _motor.Submit(Vector3.zero, true); Tick(.08f);
            Assert.That(_motor.Body.linearVelocity.y, Is.LessThan(speed));
            Tick(1.5f); Assert.That(_motor.Grounded, Is.True); Assert.That(_motor.Body.position.y, Is.LessThan(.1f));
            Assert.That(_animation.CurrentClip, Is.EqualTo("Cat_Idle"));
        }
        [Test] public void Preview_ThreeCoreClipsStayInPlaceAndExitRestoresMovement()
        {
            var names = new[] { "Cat_Idle", "Cat_Run", "Cat_Jump_Air" };
            for (var i = 0; i < names.Length; i++)
            {
                _input.PreviewClip(i); var position = _motor.Body.position;
                _motor.Submit(Vector3.forward, true); Tick(.3f);
                Assert.That(_animation.CurrentClip, Is.EqualTo(names[i])); Assert.That(_motor.Body.position, Is.EqualTo(position));
                Assert.That(_motor.Body.isKinematic, Is.True);
            }
            _input.ResumeMovement(); _motor.Submit(Vector3.forward, false); Tick(.5f);
            Assert.That(_motor.Body.isKinematic, Is.False); Assert.That(_motor.PlanarSpeed, Is.GreaterThan(1));
        }
        [Test] public void Preview_PauseScrubRestartAndSpeedControl()
        {
            _input.PreviewClip(1); _input.PlaybackSpeed = .5f; _animation.Present(.1f);
            var time = _input.NormalizedTime; _input.TogglePause(); _animation.Present(1);
            Assert.That(_input.NormalizedTime, Is.EqualTo(time));
            _input.Seek(.7f); Assert.That(_input.NormalizedTime, Is.EqualTo(.7f)); Assert.That(_input.IsPaused, Is.True);
            _input.RestartClip(); Assert.That(_input.NormalizedTime, Is.Zero); Assert.That(_input.IsPaused, Is.False);
            _animation.Present(.1f); Assert.That(_input.NormalizedTime, Is.EqualTo(time).Within(.0001f));
        }
        [Test] public void Preview_CoreClipsRemainLoopable()
        {
            for (var i = 0; i < CatAnimationController.PreviewClipCount; i++)
            {
                _input.PreviewClip(i); _animation.Present(10);
                Assert.That(_input.NormalizedTime, Is.LessThan(1)); Assert.That(_input.IsPaused, Is.False);
            }
        }
        [Test] public void Reset_ClearsPreviewVelocityAndQueuedJump()
        {
            _input.PreviewClip(2); _input.ResetPlayer(); Tick(.4f);
            Assert.That(_input.IsPreviewing, Is.False); Assert.That(_motor.Body.isKinematic, Is.False);
            Assert.That(_motor.PlanarSpeed, Is.Zero); Assert.That(_motor.Grounded, Is.True);
        }
        [Test] public void SoftMotion_LookAndRestoreDoNotMoveRootOrFeet()
        {
            var motion = _player.GetComponent<CatSecondaryMotion>(); motion.Initialize();
            Assert.That(motion.IsReady, Is.True);
            var head = _animation.Animator.GetBoneTransform(HumanBodyBones.Head);
            var foot = _animation.Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rootPosition = _player.transform.position; var footPosition = foot.position; var original = head.rotation;
            motion.SetLookPitch(30);
            for (var i = 0; i < 60; i++) { motion.Present(1f / 60); }
            Assert.That(Quaternion.Angle(original, head.rotation), Is.GreaterThan(20));
            Assert.That(Vector3.Distance(rootPosition, _player.transform.position), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(footPosition, foot.position), Is.LessThan(.0001f));
            motion.RestorePose(); Assert.That(Quaternion.Angle(original, head.rotation), Is.LessThan(.05f));
        }
        [Test] public void SoftMotion_ResetAndPreviewRemoveAllOffsets()
        {
            var motion = _player.GetComponent<CatSecondaryMotion>(); motion.Initialize();
            motion.SamplePhysics(.02f); motion.Present(.02f); _motor.Body.linearVelocity = Vector3.forward * 6; motion.SamplePhysics(.02f); motion.Present(.02f);
            Assert.That(motion.ArmDeflection.magnitude, Is.GreaterThan(0));
            motion.ResetMotion(); Assert.That(motion.ArmDeflection, Is.EqualTo(Vector3.zero));
            Assert.That(motion.LeanDegrees, Is.EqualTo(Vector3.zero));
            _input.PreviewClip(1); var head = _animation.Animator.GetBoneTransform(HumanBodyBones.Head);
            var pose = head.rotation; motion.SetLookPitch(30); motion.Present(.1f);
            Assert.That(Quaternion.Angle(pose, head.rotation), Is.LessThan(.05f));
        }
        [Test] public void SoftMotion_HighRenderRatePreservesPhysicsImpulse()
        {
            var motion = _player.GetComponent<CatSecondaryMotion>(); motion.Initialize(); motion.SamplePhysics(.02f);
            _motor.Body.linearVelocity = Vector3.right * .48f; motion.SamplePhysics(.02f);
            for (var i = 0; i < 20; i++) { motion.Present(.001f); }
            var fast = motion.ArmDeflection;
            motion.ResetMotion(); _motor.Body.linearVelocity = Vector3.zero; motion.SamplePhysics(.02f);
            _motor.Body.linearVelocity = Vector3.right * .48f; motion.SamplePhysics(.02f); motion.Present(.02f);
            Assert.That(Vector3.Distance(fast, motion.ArmDeflection), Is.LessThan(.0001f));
            Assert.That(fast.magnitude, Is.GreaterThan(.005f));
        }
        [Test] public void Spring_VaryingFrameRateMatchesAndSettlesWithoutDrift()
        {
            var fast = new CatMotionSpring(); var slow = new CatMotionSpring(); var target = new Vector3(12, 3, -4);
            for (var i = 0; i < 120; i++) { fast.Step(target, 2, .7f, 1f / 120); }
            for (var i = 0; i < 30; i++) { slow.Step(target, 2, .7f, 1f / 30); }
            Assert.That(Vector3.Distance(fast.Value, slow.Value), Is.LessThan(.001f));
            for (var i = 0; i < 120; i++) { fast.Step(Vector3.zero, 2, .7f, 1f / 60); }
            Assert.That(fast.Value.magnitude, Is.LessThan(.001f));
            var before = fast.Value; fast.Step(target, 2, .7f, float.NaN);
            Assert.That(fast.Value, Is.EqualTo(before));
        }
        [Test] public void MinimalController_ContainsCoreLocomotionGrabStancesAndRetainedAbilityClips()
        {
            var clips = _animation.Animator.runtimeAnimatorController.animationClips.Select(c => c.name).ToArray();
            foreach (var core in new[] { "Cat_Idle", "Cat_Run", "Cat_Jump_Air" }) { Assert.That(clips, Does.Contain(core)); }
            foreach (var stance in new[] { "Cat_Grab_Hold", "Cat_Carry_Move" }) { Assert.That(clips, Does.Contain(stance)); }
            Assert.That(clips.Count(name => name.StartsWith("Cat_Ability_")), Is.EqualTo(16));
            Assert.That(clips.Any(name => name is "Cat_Walk" or "Cat_Jump_Start" or "Cat_Fall" or "Cat_Land"), Is.False);
            Assert.That(CatAnimationController.PreviewClipCount, Is.EqualTo(3));
            _input.PreviewClip(3);
            Assert.That(_motor.Suspended, Is.False); Assert.That(_animation.IsPreviewing, Is.False);
        }
        [Test] public void Combat_PunchHitsTargetOnceInsideContactWindow()
        {
            var target = SpawnTarget(new Vector3(0, .03f, 1.2f));
            _combat.RequestAttack(); _combat.Step(.01f); _combat.Step(.15f);
            Assert.That(target.Health, Is.EqualTo(target.Settings.MaxHealth - _combatant.Settings.PunchDamage).Within(.01f));
            var health = target.Health; _combat.Step(.05f);
            Assert.That(target.Health, Is.EqualTo(health), "One action must not hit the same target twice.");
        }
        [Test] public void Combat_GrabAllowsTravelAndKeepsStableCameraFacing()
        {
            _motor.SubmitFacing(Vector3.right, 1000);
            _motor.Submit(Vector3.forward, false);
            _combat.SetGrabHeld(true);
            _combat.Step(.01f);
            Assert.That(_combatant.Phase, Is.EqualTo(CatCombatPhase.Grab));
            Assert.That(_combat.CanMove, Is.True);
            Assert.That(_combat.CanTurn, Is.True);
            _combat.Step(_combatant.Settings.GrabWindupSeconds + .1f);
            Assert.That(_combatant.Phase, Is.EqualTo(CatCombatPhase.Grab), "Holding RMB without a target must not flicker back to Free.");
            Tick(.3f);
            _combat.Step(.02f);
            _animation.Present(.02f);
            _animation.Animator.Update(.02f);
            Assert.That(Vector3.Angle(Vector3.forward, _motor.Body.rotation * Vector3.forward), Is.GreaterThan(1));
            Assert.That(_motor.Body.linearVelocity.z, Is.GreaterThan(1));
            Assert.That(_animation.CurrentClip, Is.EqualTo("Cat_Carry_Move"), "Moving grab must use its dedicated stance instead of alternating Idle and Run.");
            _combat.SetGrabHeld(false);
            _combat.Step(.01f);
            Assert.That(_combatant.Phase, Is.EqualTo(CatCombatPhase.Free));
        }
        [Test] public void Combat_GrabLiftThrowReleasesAndLaunchesTarget()
        {
            var target = SpawnTarget(new Vector3(0, .03f, 1.2f));
            _combat.SetGrabHeld(true); _combat.Step(.01f); _combat.Step(_combatant.Settings.GrabWindupSeconds);
            Assert.That(_combatant.Held, Is.EqualTo(target)); Assert.That(target.Holder, Is.EqualTo(_combatant));
            _combat.RequestLift(); _combat.Step(.01f); _combat.Step(_combatant.Settings.LiftSeconds);
            Assert.That(_combatant.Phase, Is.EqualTo(CatCombatPhase.Carrying));
            _combat.RequestAttack(); _combat.Step(.01f); _combat.Step(.21f);
            Assert.That(_combatant.Held, Is.Null); Assert.That(target.Holder, Is.Null);
            _scene.GetPhysicsScene().Simulate(.02f);
            Assert.That(target.Body.linearVelocity.magnitude, Is.GreaterThan(2));
        }
        [Test] public void Combat_KeyboardMouseBindingsMatchReferenceLayout()
        {
            var json = File.ReadAllText("Assets/_Project/Settings/Input/PawArena.inputactions");
            const string grabPath = "\"path\": \"<Mouse>/rightButton\"";
            const string shiftPath = "\"path\": \"<Keyboard>/leftShift\"";
            var grabIndex = json.IndexOf(grabPath);
            var shiftIndex = json.IndexOf(shiftPath);
            Assert.That(grabIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(shiftIndex, Is.GreaterThanOrEqualTo(0));
            var grabBinding = json.Substring(grabIndex, Mathf.Min(300, json.Length - grabIndex));
            var shiftBinding = json.Substring(shiftIndex, Mathf.Min(300, json.Length - shiftIndex));
            Assert.That(grabBinding, Does.Contain("\"action\": \"Grab\""));
            Assert.That(shiftBinding, Does.Contain("\"action\": \"Lift\""));
            Assert.That(json.IndexOf(grabPath, grabIndex + grabPath.Length), Is.EqualTo(-1));
            Assert.That(json.IndexOf(shiftPath, shiftIndex + shiftPath.Length), Is.EqualTo(-1));
            Assert.That(json, Does.Not.Contain("<Keyboard>/e"));
        }
        [Test] public void Combat_KnockoutRecoversAndRingOutRespawnsSameObject()
        {
            var target = SpawnTarget(new Vector3(0, .03f, 1.2f));
            for (var i = 0; i < 5; i++)
            {
                target.ReceiveHit(_combatant, 24, Vector3.zero);
                target.Step(target.Settings.HitImmunitySeconds + .01f);
            }
            Assert.That(target.IsKnockedOut, Is.True);
            target.Step(target.Settings.KnockoutSeconds + .01f);
            Assert.That(target.Phase, Is.EqualTo(CatCombatPhase.Free));
            var instance = target.GetEntityId(); target.transform.position = Vector3.down * 5; target.Step(.02f);
            Assert.That(target.GetEntityId(), Is.EqualTo(instance));
            Assert.That(target.Health, Is.EqualTo(target.Settings.MaxHealth));
        }
        [Test] public void Combat_DashConsumesCooldownAndCannotBeSpamRestarted()
        {
            _combat.RequestAbility(); _combat.Step(.01f);
            Assert.That(_combatant.Phase, Is.EqualTo(CatCombatPhase.Ability));
            var cooldown = _combat.SelectedCooldown;
            Assert.That(cooldown, Is.GreaterThan(7.9f));
            _combat.RequestAbility(); _combat.Step(.02f);
            Assert.That(_combat.SelectedCooldown, Is.LessThan(cooldown));
            _combat.Step(.13f);
            Assert.That(_combatant.Body.linearVelocity.z, Is.GreaterThan(10));
        }
        [Test] public void Combat_PlayerPrefabOwnsCompleteCompositionAndMainHasTarget()
        {
            Assert.That(_player.GetComponent<CatCombatant>(), Is.Not.Null);
            Assert.That(_player.GetComponent<CatPlayerCombat>(), Is.Not.Null);
            Assert.That(_player.GetComponent<CatCombatEffects>(), Is.Not.Null);
            Assert.That(_player.GetComponent<CatCombatPose>(), Is.Not.Null);
            Assert.That(_player.GetComponent<CatCombatHud>(), Is.Not.Null);
            var main = SceneManager.GetSceneByPath("Assets/_Project/Scenes/Main.unity");
            var openedForTest = !main.IsValid() || !main.isLoaded;
            if (openedForTest) { main = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity", OpenSceneMode.Additive); }
            try
            {
                var roots = main.GetRootGameObjects();
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Any(t => t.name == "CombatTarget"), Is.True);
                var flow = roots.SelectMany(root => root.GetComponentsInChildren<GameFlowController>(true)).Single();
                Assert.That(flow.CurrentState, Is.EqualTo(GameFlowState.Lobby));
            }
            finally { if (openedForTest) { EditorSceneManager.CloseScene(main, true); } }
        }
        [Test] public void ControlModes_SwitchLobbyOrbitAndPartyContinuousMouseLook()
        {
            var cameraObject = new GameObject("ControlModeCamera", typeof(Camera), typeof(CharacterReviewCamera));
            SceneManager.MoveGameObjectToScene(cameraObject, _scene);
            var orbit = cameraObject.GetComponent<CharacterReviewCamera>();
            orbit.Follow(_player.transform);
            _input.BindCamera(cameraObject.GetComponent<Camera>());

            _input.SetControlMode(CatPlayerControlMode.PartyMatch);
            Assert.That(_input.ControlMode, Is.EqualTo(CatPlayerControlMode.PartyMatch));
            Assert.That(orbit.IsPartyMatchMode, Is.True);

            _input.SetControlMode(CatPlayerControlMode.Lobby);
            Assert.That(_input.ControlMode, Is.EqualTo(CatPlayerControlMode.Lobby));
            Assert.That(orbit.IsPartyMatchMode, Is.False);
        }
        [Test] public void Combat_AllElevenAbilitiesHaveValidTuningAndSkillClips()
        {
            var clips = _animation.Animator.runtimeAnimatorController.animationClips.Select(clip => clip.name).ToArray();
            var abilities = (CatCombatAbility[])System.Enum.GetValues(typeof(CatCombatAbility));
            Assert.That(abilities.Length, Is.EqualTo(11));
            foreach (var ability in abilities)
            {
                var tuning = _combatant.Settings.Ability(ability);
                Assert.That(tuning, Is.Not.Null); Assert.That(tuning.Cooldown, Is.GreaterThan(0));
                Assert.That(tuning.CastSeconds, Is.GreaterThan(0));
            }
            Assert.That(clips.Count(name => name.StartsWith("Cat_Ability_")), Is.EqualTo(16));
        }
        [Test] public void Build_ContainsOnlyMainAndSourceModelHasNoGameplay()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            Assert.That(scenes.Length, Is.EqualTo(1)); Assert.That(scenes[0].path, Is.EqualTo("Assets/_Project/Scenes/Main.unity"));
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/Cat/PF_Cat_Base.prefab");
            Assert.That(visual.GetComponent<Rigidbody>(), Is.Null); Assert.That(visual.GetComponent<CatLocomotion>(), Is.Null);
        }

        private CatCombatant SpawnTarget(Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Bots/PF_Bot.prefab");
            var targetObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
            var motor = targetObject.GetComponent<CatLocomotion>(); motor.Initialize(); motor.Warp(position, Quaternion.Euler(0, 180, 0));
            var target = targetObject.GetComponent<CatCombatant>(); target.Initialize(); target.SetSpawn(position, Quaternion.Euler(0, 180, 0));
            return target;
        }
    }
}
