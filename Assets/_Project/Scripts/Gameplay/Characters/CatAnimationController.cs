using System.Collections.Generic;
using UnityEngine;

namespace PawArena.Gameplay.Characters
{
    /// <summary>Six authored states; gameplay and ragdoll retain authority over movement.</summary>
    [DisallowMultipleComponent]
    public sealed class CatAnimationController : MonoBehaviour
    {
        private const float StanceBlendSeconds = .08f;
        public const string PunchLayerName = "Combat Upper Body";
        public const string RunSpeedParameter = "LocomotionSpeed";
        public const string PunchSpeedParameter = "PunchSpeed";
        private static readonly int RunSpeedHash = Animator.StringToHash(RunSpeedParameter);
        private static readonly int PunchSpeedHash = Animator.StringToHash(PunchSpeedParameter);
        private static readonly int PunchStateHash = Animator.StringToHash(PunchLayerName + ".Cat_Punch");
        private static readonly string[] Clips = { "Cat_Idle", "Cat_Run", "Cat_Punch", "Cat_Grab", "Cat_Lift", "Cat_Jump" };
        public static int PreviewClipCount => Clips.Length;
        [SerializeField] private CatLocomotion _motor;
        [SerializeField] private Animator _animator;
        private readonly Dictionary<string, float> _lengths = new();
        private readonly Dictionary<string, bool> _loops = new();
        private readonly Dictionary<string, int> _stateHashes = new();
        private readonly Dictionary<string, int> _stanceHashes = new();
        private bool _ready;
        private float _previewTime, _actionRemaining;
        private float _actionSpeed = 1;
        private string _actionClip, _stanceClip;
        private string _locomotionClip;
        private string _presentedStanceClip;
        private float _stanceWeight;
        private int _combatLayer = -1;
        private int _punchLayer = -1;
        private float _punchWeight;
        private bool _hasRunSpeedParameter;
        private int _preview = -1;
        public string CurrentClip { get; private set; } = "Cat_Idle";
        public bool IsPreviewing => _preview >= 0;
        public bool IsPlayingAction => _actionRemaining > 0;
        private bool IsUpperBodyAction => _punchLayer >= 0 && _actionClip == "Cat_Punch";
        public string StanceClip => _stanceClip;
        public bool IsPaused { get; private set; }
        public float PlaybackSpeed { get; set; } = 1;
        public float NormalizedTime => _previewTime;
        public Animator Animator => _animator;
        private void Awake() => Initialize();
        private void OnEnable() { if (_motor != null) { _motor.Warped += ResetPresentation; _motor.Jumped += PresentJump; } }
        private void OnDisable() { if (_motor != null) { _motor.Warped -= ResetPresentation; _motor.Jumped -= PresentJump; } }
        private void Update() => Present(Time.deltaTime);

        public void Initialize()
        {
            if (_ready || _animator == null || _motor == null || _animator.runtimeAnimatorController == null) { return; }
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                _lengths[clip.name] = clip.length;
                _loops[clip.name] = clip.isLooping;
                _stateHashes[clip.name] = Animator.StringToHash("Base Layer." + clip.name);
                _stanceHashes[clip.name] = Animator.StringToHash("Combat Arms." + clip.name);
            }
            _animator.applyRootMotion = false;
            _combatLayer = _animator.GetLayerIndex("Combat Arms");
            _punchLayer = _animator.GetLayerIndex(PunchLayerName);
            foreach (var parameter in _animator.parameters)
            {
                if (parameter.nameHash == RunSpeedHash && parameter.type == AnimatorControllerParameterType.Float)
                { _hasRunSpeedParameter = true; }
            }
            // Keep the state machine advancing for authoritative action timing, but skip bone writes off-screen.
            _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            _ready = true;
        }
        private void ResetPresentation()
        {
            _actionRemaining = 0;
            _actionClip = _stanceClip = _locomotionClip = _presentedStanceClip = null;
            _stanceWeight = 0;
            _punchWeight = 0;
            if (_combatLayer >= 0) { _animator.SetLayerWeight(_combatLayer, 0); }
            if (_punchLayer >= 0) { _animator.SetLayerWeight(_punchLayer, 0); }
            CurrentClip = "";
        }
        private void PresentJump()
        {
            Initialize();
            if (!IsPlayingAction && string.IsNullOrEmpty(_stanceClip) && _lengths.TryGetValue("Cat_Jump", out var duration))
            { PlayAction("Cat_Jump", duration, false); }
        }

        public void Preview(int index)
        {
            Initialize(); if (!_ready || index < 0 || index >= Clips.Length || !_lengths.ContainsKey(Clips[index])) { return; }
            _preview = index; IsPaused = false; _previewTime = 0; CurrentClip = Clips[index];
            _locomotionClip = null;
            PresentStanceLayer(0);
            PresentPunchLayer(0);
            Sample();
        }
        public void ResumeMovement()
        { _preview = -1; IsPaused = false; ResetPresentation(); if (_animator != null) { _animator.speed = 1; } }
        public void TogglePause() { if (IsPreviewing) { IsPaused = !IsPaused; } }
        public void RestartClip() { if (IsPreviewing) { _previewTime = 0; IsPaused = false; Sample(); } }
        public void Seek(float time) { if (IsPreviewing) { _previewTime = Mathf.Clamp01(time); IsPaused = true; Sample(); } }

        /// <summary>Temporarily presents a combat skill state without adding locomotion phases.</summary>
        public bool PlayAction(string clip, float duration, bool loop)
        {
            Initialize();
            if (!_ready || IsPreviewing || string.IsNullOrEmpty(clip) || !_lengths.ContainsKey(clip) || duration <= 0) { return false; }
            _actionClip = clip;
            _actionRemaining = duration;
            CurrentClip = clip;
            _actionSpeed = loop ? 1 : _lengths[clip] / duration;
            if (IsUpperBodyAction)
            {
                _animator.speed = 1;
                _animator.SetFloat(PunchSpeedHash, _actionSpeed);
                _animator.Play(PunchStateHash, _punchLayer, 0);
                PresentLocomotion();
            }
            else
            {
                // Any full-body interruption invalidates the cached base state.
                _locomotionClip = null;
                _animator.speed = _actionSpeed;
                _animator.CrossFadeInFixedTime(_stateHashes[clip], .04f, 0, 0);
                PresentStanceLayer(0);
                PresentPunchLayer(0);
            }
            return true;
        }

        /// <summary>Returns immediately to state-driven locomotion.</summary>
        public void ClearAction()
        {
            if (_actionRemaining <= 0 && string.IsNullOrEmpty(_actionClip)) { return; }
            _actionRemaining = 0; _actionClip = null; CurrentClip = "";
        }

        /// <summary>Sets a persistent arm stance while Idle/Run continues to drive locomotion.</summary>
        public bool SetStance(string clip)
        {
            Initialize();
            if (!_ready || string.IsNullOrEmpty(clip) || !_lengths.ContainsKey(clip)) { return false; }
            if (_stanceClip == clip) { return true; }
            _stanceClip = clip;
            if (!IsPlayingAction) { CurrentClip = ""; }
            return true;
        }

        /// <summary>Returns from a persistent combat stance to regular locomotion selection.</summary>
        public void ClearStance()
        {
            if (string.IsNullOrEmpty(_stanceClip)) { return; }
            _stanceClip = null;
            if (!IsPlayingAction) { CurrentClip = ""; }
        }

        /// <summary>Runs the preview clock or selects movement from actual body speed and grounded state.</summary>
        public void Present(float delta)
        {
            Initialize(); if (!_ready || !float.IsFinite(delta) || delta < 0) { return; }
            if (IsPreviewing)
            {
                PresentStanceLayer(delta);
                PresentPunchLayer(delta);
                if (!IsPaused)
                {
                    _previewTime += delta * Mathf.Clamp(PlaybackSpeed, .1f, 2) / Mathf.Max(.01f, _lengths[CurrentClip]);
                    if (_loops[CurrentClip]) { _previewTime %= 1; }
                    else if (_previewTime >= 1) { _previewTime = 1; IsPaused = true; }
                }
                Sample(); return;
            }
            if (_actionRemaining > 0)
            {
                _actionRemaining = Mathf.Max(0, _actionRemaining - delta);
                if (_actionRemaining == 0) { _actionClip = null; }
            }
            PresentStanceLayer(delta);
            PresentPunchLayer(delta);
            if (IsPlayingAction && !IsUpperBodyAction)
            {
                CurrentClip = _actionClip;
                _animator.speed = _actionSpeed;
                return;
            }
            if (_motor.Suspended) { _animator.speed = 0; return; }
            PresentLocomotion();
            CurrentClip = IsPlayingAction ? _actionClip : !string.IsNullOrEmpty(_stanceClip) ? _stanceClip : _locomotionClip;
        }

        private void PresentLocomotion()
        {
            var speed = _motor.PlanarSpeed;
            // Jump is triggered once at takeoff; landing never selects a forced pose.
            var clip = speed > .15f ? "Cat_Run" : "Cat_Idle";
            if (clip != _locomotionClip)
            { _locomotionClip = clip; _animator.CrossFadeInFixedTime(_stateHashes[clip], .1f, 0, 0); }
            var runSpeed = clip == "Cat_Run" ? Mathf.Clamp(speed / Mathf.Max(.01f, _motor.Settings.RunSpeed), .4f, 1.4f) : 1;
            // Animator.speed affects every layer, so only the Run state gets this multiplier.
            if (_hasRunSpeedParameter) { _animator.SetFloat(RunSpeedHash, runSpeed); }
            _animator.speed = _hasRunSpeedParameter || _combatLayer >= 0 || _punchLayer >= 0 ? 1 : runSpeed;
        }

        private void PresentPunchLayer(float delta)
        {
            if (_punchLayer < 0) { return; }
            if (IsPreviewing || (IsPlayingAction && !IsUpperBodyAction)) { _punchWeight = 0; }
            else
            {
                var active = IsPlayingAction && IsUpperBodyAction;
                _punchWeight = Mathf.MoveTowards(_punchWeight, active ? 1 : 0, delta / (active ? .04f : StanceBlendSeconds));
            }
            _animator.SetLayerWeight(_punchLayer, _punchWeight);
        }

        private void PresentStanceLayer(float delta)
        {
            if (_combatLayer < 0) { return; }
            if (IsPreviewing || (IsPlayingAction && !IsUpperBodyAction))
            {
                // Punch has a higher layer; keep Grab's clock alive beneath it for a seamless return.
                _stanceWeight = 0;
                _presentedStanceClip = null;
                _animator.SetLayerWeight(_combatLayer, 0);
                return;
            }

            var active = !string.IsNullOrEmpty(_stanceClip);
            if (active && _presentedStanceClip != _stanceClip)
            {
                if (string.IsNullOrEmpty(_presentedStanceClip))
                {
                    // A zero-weight layer retains its last/default state. Reset its time BEFORE
                    // revealing it; crossfading Grab to its stale self replays the entry gesture.
                    _animator.Play(_stanceHashes[_stanceClip], _combatLayer, 0);
                }
                else
                {
                    _animator.CrossFadeInFixedTime(_stanceHashes[_stanceClip], StanceBlendSeconds, _combatLayer, 0);
                }
                _presentedStanceClip = _stanceClip;
            }

            _stanceWeight = Mathf.MoveTowards(_stanceWeight, active ? 1 : 0, delta / StanceBlendSeconds);
            _animator.SetLayerWeight(_combatLayer, _stanceWeight);
            // Re-pressing during release continues the same pose instead of restarting the clip.
            if (_stanceWeight == 0) { _presentedStanceClip = null; }
        }

        private void Sample()
        { _animator.speed = 0; _animator.Play(_stateHashes[CurrentClip], 0, Mathf.Min(_previewTime, .99999f)); _animator.Update(0); }
    }
}
