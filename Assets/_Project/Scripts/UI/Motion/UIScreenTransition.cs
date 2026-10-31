using System;
using DG.Tweening;
using UnityEngine;

namespace PawArena.UI.Motion
{
    /// <summary>Owns the visual transition of a screen without knowing its navigation rules.</summary>
    [DisallowMultipleComponent]
    public sealed class UIScreenTransition : MonoBehaviour
    {
        private const float DefaultShowDuration = 0.22f;
        private const float DefaultHideDuration = 0.16f;
        private const float DefaultHiddenScale = 0.94f;

        [SerializeField] private CanvasGroup _canvasGroup = null;
        [SerializeField] private RectTransform _motionRoot = null;
        [SerializeField] private UIAnimationProfile _profile = null;

        private Tween _activeTween;
        private Vector3 _shownScale = Vector3.one;
        private bool _initialized;

        public bool IsAnimating => _activeTween != null && _activeTween.IsActive();

        /// <summary>Assigns authored dependencies from the UI installer.</summary>
        public void Configure(
            CanvasGroup canvasGroup,
            RectTransform motionRoot,
            UIAnimationProfile profile)
        {
            _canvasGroup = canvasGroup;
            _motionRoot = motionRoot;
            _profile = profile;
            _initialized = false;
        }

        public void PlayShow(Action completed)
        {
            EnsureInitialized();
            KillActiveTween();
            if (_canvasGroup == null || _motionRoot == null)
            {
                completed?.Invoke();
                return;
            }

            if (_canvasGroup.alpha <= 0.001f)
            {
                _motionRoot.localScale = _shownScale * HiddenScale;
            }

            var sequence = DOTween.Sequence();
            sequence.SetUpdate(UseUnscaledTime);
            sequence.SetTarget(this);
            sequence.Join(
                DOTween.To(
                        () => _canvasGroup.alpha,
                        value => _canvasGroup.alpha = value,
                        1f,
                        ShowDuration)
                    .SetEase(Ease.OutQuad));
            sequence.Join(
                DOTween.To(
                        () => _motionRoot.localScale,
                        value => _motionRoot.localScale = value,
                        _shownScale,
                        ShowDuration)
                    .SetEase(ShowEase));
            Track(sequence, completed);
        }

        public void PlayHide(Action completed)
        {
            EnsureInitialized();
            KillActiveTween();
            if (_canvasGroup == null || _motionRoot == null)
            {
                completed?.Invoke();
                return;
            }

            var sequence = DOTween.Sequence();
            sequence.SetUpdate(UseUnscaledTime);
            sequence.SetTarget(this);
            sequence.Join(
                DOTween.To(
                        () => _canvasGroup.alpha,
                        value => _canvasGroup.alpha = value,
                        0f,
                        HideDuration)
                    .SetEase(Ease.InQuad));
            sequence.Join(
                DOTween.To(
                        () => _motionRoot.localScale,
                        value => _motionRoot.localScale = value,
                        _shownScale * HiddenScale,
                        HideDuration)
                    .SetEase(HideEase));
            Track(sequence, completed);
        }

        public void SetImmediate(bool visible)
        {
            EnsureInitialized();
            KillActiveTween();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
            }

            if (_motionRoot != null)
            {
                _motionRoot.localScale = visible ? _shownScale : _shownScale * HiddenScale;
            }
        }

        public void Cancel() => KillActiveTween();

        private void Awake() => EnsureInitialized();

        private void OnDisable()
        {
            KillActiveTween();
            if (_initialized && _motionRoot != null)
            {
                _motionRoot.localScale = _shownScale;
            }
        }

        private void OnDestroy() => KillActiveTween();

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _canvasGroup ??= GetComponent<CanvasGroup>();
            _motionRoot ??= transform as RectTransform;
            if (_motionRoot != null)
            {
                _shownScale = _motionRoot.localScale;
            }

            _initialized = true;
        }

        private void Track(Tween tween, Action completed)
        {
            _activeTween = tween;
            tween.OnComplete(() =>
            {
                _activeTween = null;
                completed?.Invoke();
            });
        }

        private void KillActiveTween()
        {
            if (_activeTween != null && _activeTween.IsActive())
            {
                _activeTween.Kill(false);
            }

            _activeTween = null;
        }

        private float ShowDuration => _profile != null ? _profile.ScreenShowDuration : DefaultShowDuration;
        private float HideDuration => _profile != null ? _profile.ScreenHideDuration : DefaultHideDuration;
        private float HiddenScale => _profile != null ? _profile.ScreenHiddenScale : DefaultHiddenScale;
        private Ease ShowEase => _profile != null ? _profile.ScreenShowEase : Ease.OutBack;
        private Ease HideEase => _profile != null ? _profile.ScreenHideEase : Ease.InCubic;
        private bool UseUnscaledTime => _profile == null || _profile.UseUnscaledTime;
    }
}
