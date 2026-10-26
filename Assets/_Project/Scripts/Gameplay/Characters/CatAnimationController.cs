using System.Collections.Generic;
using UnityEngine;

namespace PawArena.Gameplay.Characters
{
    /// <summary>Authored locomotion clips only. No combat, procedural bones, IK or map knowledge.</summary>
    [DisallowMultipleComponent]
    public sealed class CatAnimationController : MonoBehaviour
    {
        private static readonly string[] Clips = { "Cat_Idle", "Cat_Run", "Cat_Jump_Air" };
        public static int PreviewClipCount => Clips.Length;
        [SerializeField] private CatLocomotion _motor;
        [SerializeField] private Animator _animator;
        private readonly Dictionary<string, float> _lengths = new();
        private readonly Dictionary<string, bool> _loops = new();
        private bool _ready;
        private float _previewTime, _actionRemaining;
        private string _actionClip, _stanceClip;
        private int _preview = -1;
        public string CurrentClip { get; private set; } = "Cat_Idle";
        public bool IsPreviewing => _preview >= 0;
        public bool IsPlayingAction => _actionRemaining > 0;
        public string StanceClip => _stanceClip;
        public bool IsPaused { get; private set; }
        public float PlaybackSpeed { get; set; } = 1;
        public float NormalizedTime => _previewTime;
        public Animator Animator => _animator;
        private void Awake() => Initialize();
        private void OnEnable() { if (_motor != null) { _motor.Warped += ResetPresentation; } }
        private void OnDisable() { if (_motor != null) { _motor.Warped -= ResetPresentation; } }
        private void Update() => Present(Time.deltaTime);

        public void Initialize()
        {
            if (_ready || _animator == null || _motor == null || _animator.runtimeAnimatorController == null) { return; }
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            { _lengths[clip.name] = clip.length; _loops[clip.name] = clip.isLooping; }
            _animator.applyRootMotion = false; _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; _ready = true;
        }
        private void ResetPresentation() { _actionRemaining = 0; _actionClip = _stanceClip = null; CurrentClip = ""; }

        public void Preview(int index)
        {
            Initialize(); if (!_ready || index < 0 || index >= Clips.Length) { return; }
            _preview = index; IsPaused = false; _previewTime = 0; CurrentClip = Clips[index];
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
            _animator.speed = 1;
            _animator.CrossFadeInFixedTime(Animator.StringToHash("Base Layer." + clip), .08f, 0, 0);
            return true;
        }

        /// <summary>Returns immediately to state-driven locomotion.</summary>
        public void ClearAction() { _actionRemaining = 0; _actionClip = null; CurrentClip = ""; }

        /// <summary>Sets a persistent combat stance that replaces Idle/Run until explicitly cleared.</summary>
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
                if (_actionRemaining > 0)
                {
                    if (CurrentClip != _actionClip)
                    {
                        CurrentClip = _actionClip;
                        _animator.CrossFadeInFixedTime(Animator.StringToHash("Base Layer." + CurrentClip), .08f, 0, 0);
                    }
                    _animator.speed = 1;
                    return;
                }
                _actionClip = null;
                CurrentClip = "";
            }
            if (_motor.Suspended) { _animator.speed = 0; return; }
            var speed = _motor.PlanarSpeed;
            if (!string.IsNullOrEmpty(_stanceClip))
            {
                if (CurrentClip != _stanceClip)
                {
                    CurrentClip = _stanceClip;
                    _animator.CrossFadeInFixedTime(Animator.StringToHash("Base Layer." + CurrentClip), .08f, 0, 0);
                }
                _animator.speed = CurrentClip == "Cat_Carry_Move" ? Mathf.Clamp(speed / _motor.Settings.RunSpeed, .4f, 1.4f) : 1;
                return;
            }
            var clip = !_motor.Grounded ? "Cat_Jump_Air" : speed > .15f ? "Cat_Run" : "Cat_Idle";
            if (clip != CurrentClip)
            { CurrentClip = clip; _animator.CrossFadeInFixedTime(Animator.StringToHash("Base Layer." + clip), .1f, 0, 0); }
            _animator.speed = clip == "Cat_Run" ? Mathf.Clamp(speed / _motor.Settings.RunSpeed, .4f, 1.4f) : 1;
        }
        private void Sample()
        { _animator.speed = 0; _animator.Play(Animator.StringToHash("Base Layer." + CurrentClip), 0, Mathf.Min(_previewTime, .99999f)); _animator.Update(0); }
    }
}
