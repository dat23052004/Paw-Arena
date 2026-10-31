using DG.Tweening;
using UnityEngine;

namespace PawArena.UI.Motion
{
    /// <summary>Fades the shared modal backdrop and deactivates it after the fade-out.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIModalDimmerTransition : MonoBehaviour
    {
        private const float DefaultDuration = 0.14f;

        [SerializeField] private CanvasGroup _canvasGroup = null;
        [SerializeField] private UIAnimationProfile _profile = null;

        private Tween _activeTween;
        private bool _targetVisible;

        public void Configure(CanvasGroup canvasGroup, UIAnimationProfile profile)
        {
            _canvasGroup = canvasGroup;
            _profile = profile;
        }

        public void SetVisible(bool visible, bool immediate)
        {
            EnsureCanvasGroup();
            _targetVisible = visible;
            KillActiveTween();

            if (visible && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (_canvasGroup == null || immediate)
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = visible ? 1f : 0f;
                }

                if (!visible && gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            _activeTween = DOTween.To(
                    () => _canvasGroup.alpha,
                    value => _canvasGroup.alpha = value,
                    visible ? 1f : 0f,
                    Duration)
                .SetEase(DimmerEase)
                .SetUpdate(UseUnscaledTime)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    _activeTween = null;
                    if (!_targetVisible && gameObject.activeSelf)
                    {
                        gameObject.SetActive(false);
                    }
                });
        }

        private void Awake() => EnsureCanvasGroup();

        private void OnDisable() => KillActiveTween();

        private void OnDestroy() => KillActiveTween();

        private void EnsureCanvasGroup() => _canvasGroup ??= GetComponent<CanvasGroup>();

        private void KillActiveTween()
        {
            if (_activeTween != null && _activeTween.IsActive())
            {
                _activeTween.Kill(false);
            }

            _activeTween = null;
        }

        private float Duration => _profile != null ? _profile.DimmerDuration : DefaultDuration;
        private Ease DimmerEase => _profile != null ? _profile.DimmerEase : Ease.OutQuad;
        private bool UseUnscaledTime => _profile == null || _profile.UseUnscaledTime;
    }
}
