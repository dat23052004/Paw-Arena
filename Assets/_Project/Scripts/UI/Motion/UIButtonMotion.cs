using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PawArena.UI.Motion
{
    /// <summary>Adds consistent press and release feedback without owning button actions.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class UIButtonMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float DefaultPressedScale = 0.96f;
        private const float DefaultPressDuration = 0.07f;
        private const float DefaultReleaseDuration = 0.12f;

        [SerializeField] private Selectable _selectable = null;
        [SerializeField] private RectTransform _visualRoot = null;
        [SerializeField] private UIAnimationProfile _profile = null;

        private Tween _activeTween;
        private Vector3 _restingScale = Vector3.one;
        private bool _initialized;

        public void Configure(
            Selectable selectable,
            RectTransform visualRoot,
            UIAnimationProfile profile)
        {
            _selectable = selectable;
            _visualRoot = visualRoot;
            _profile = profile;
            _initialized = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureInitialized();
            if (_selectable == null || !_selectable.IsInteractable())
            {
                return;
            }

            TweenTo(_restingScale * PressedScale, PressDuration, PressEase);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Awake() => EnsureInitialized();

        private void OnEnable()
        {
            EnsureInitialized();
            if (_visualRoot != null)
            {
                _restingScale = _visualRoot.localScale;
            }
        }

        private void OnDisable()
        {
            KillActiveTween();
            if (_visualRoot != null)
            {
                _visualRoot.localScale = _restingScale;
            }
        }

        private void OnDestroy() => KillActiveTween();

        private void Release()
        {
            EnsureInitialized();
            if (_visualRoot != null)
            {
                TweenTo(_restingScale, ReleaseDuration, ReleaseEase);
            }
        }

        private void TweenTo(Vector3 targetScale, float duration, Ease ease)
        {
            if (_visualRoot == null)
            {
                return;
            }

            KillActiveTween();
            _activeTween = DOTween.To(
                    () => _visualRoot.localScale,
                    value => _visualRoot.localScale = value,
                    targetScale,
                    duration)
                .SetEase(ease)
                .SetUpdate(UseUnscaledTime)
                .SetTarget(this)
                .OnComplete(() => _activeTween = null);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _selectable ??= GetComponent<Selectable>();
            _visualRoot ??= transform as RectTransform;
            if (_visualRoot != null)
            {
                _restingScale = _visualRoot.localScale;
            }

            _initialized = true;
        }

        private void KillActiveTween()
        {
            if (_activeTween != null && _activeTween.IsActive())
            {
                _activeTween.Kill(false);
            }

            _activeTween = null;
        }

        private float PressedScale => _profile != null ? _profile.ButtonPressedScale : DefaultPressedScale;
        private float PressDuration => _profile != null ? _profile.ButtonPressDuration : DefaultPressDuration;
        private float ReleaseDuration => _profile != null ? _profile.ButtonReleaseDuration : DefaultReleaseDuration;
        private Ease PressEase => _profile != null ? _profile.ButtonPressEase : Ease.OutQuad;
        private Ease ReleaseEase => _profile != null ? _profile.ButtonReleaseEase : Ease.OutBack;
        private bool UseUnscaledTime => _profile == null || _profile.UseUnscaledTime;
    }
}
