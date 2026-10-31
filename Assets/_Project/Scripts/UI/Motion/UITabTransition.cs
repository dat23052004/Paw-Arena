using DG.Tweening;
using UnityEngine;

namespace PawArena.UI.Motion
{
    /// <summary>Animates only the presentation of an already-selected tab panel.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UITabTransition : MonoBehaviour
    {
        private const float DefaultOffset = 36f;
        private const float DefaultDuration = 0.18f;

        [SerializeField] private CanvasGroup _canvasGroup = null;
        [SerializeField] private RectTransform _motionRoot = null;
        [SerializeField] private UIAnimationProfile _profile = null;

        private Tween _activeTween;
        private Vector2 _restingPosition;
        private bool _initialized;

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

        public void PlayIn(int direction)
        {
            EnsureInitialized();
            KillActiveTween();
            if (_canvasGroup == null || _motionRoot == null)
            {
                return;
            }

            var sign = direction < 0 ? -1f : 1f;
            _canvasGroup.alpha = 0f;
            _motionRoot.anchoredPosition = _restingPosition + Vector2.right * (Offset * sign);

            var sequence = DOTween.Sequence();
            sequence.SetUpdate(UseUnscaledTime);
            sequence.SetTarget(this);
            sequence.Join(
                DOTween.To(
                        () => _canvasGroup.alpha,
                        value => _canvasGroup.alpha = value,
                        1f,
                        Duration)
                    .SetEase(Ease.OutQuad));
            sequence.Join(
                DOTween.To(
                        () => _motionRoot.anchoredPosition,
                        value => _motionRoot.anchoredPosition = value,
                        _restingPosition,
                        Duration)
                    .SetEase(EnterEase));
            _activeTween = sequence.OnComplete(() => _activeTween = null);
        }

        private void Awake() => EnsureInitialized();

        private void OnEnable() => EnsureInitialized();

        private void OnDisable()
        {
            KillActiveTween();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            if (_motionRoot != null)
            {
                _motionRoot.anchoredPosition = _restingPosition;
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
                _restingPosition = _motionRoot.anchoredPosition;
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

        private float Offset => _profile != null ? _profile.TabEnterOffset : DefaultOffset;
        private float Duration => _profile != null ? _profile.TabEnterDuration : DefaultDuration;
        private Ease EnterEase => _profile != null ? _profile.TabEnterEase : Ease.OutCubic;
        private bool UseUnscaledTime => _profile == null || _profile.UseUnscaledTime;
    }
}
