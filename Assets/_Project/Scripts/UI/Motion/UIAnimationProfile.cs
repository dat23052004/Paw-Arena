using DG.Tweening;
using UnityEngine;

namespace PawArena.UI.Motion
{
    /// <summary>Centralizes the timing and easing used by the shared UI motion system.</summary>
    [CreateAssetMenu(fileName = "UIAnimationProfile", menuName = "Paw Arena/UI/Animation Profile")]
    public sealed class UIAnimationProfile : ScriptableObject
    {
        [Header("Screen")]
        [SerializeField, Min(0.01f)] private float _screenShowDuration = 0.22f;
        [SerializeField, Min(0.01f)] private float _screenHideDuration = 0.16f;
        [SerializeField, Range(0.8f, 1f)] private float _screenHiddenScale = 0.94f;
        [SerializeField] private Ease _screenShowEase = Ease.OutBack;
        [SerializeField] private Ease _screenHideEase = Ease.InCubic;

        [Header("Modal Dimmer")]
        [SerializeField, Min(0.01f)] private float _dimmerDuration = 0.14f;
        [SerializeField] private Ease _dimmerEase = Ease.OutQuad;

        [Header("Buttons")]
        [SerializeField, Range(0.8f, 1f)] private float _buttonPressedScale = 0.96f;
        [SerializeField, Min(0.01f)] private float _buttonPressDuration = 0.07f;
        [SerializeField, Min(0.01f)] private float _buttonReleaseDuration = 0.12f;
        [SerializeField] private Ease _buttonPressEase = Ease.OutQuad;
        [SerializeField] private Ease _buttonReleaseEase = Ease.OutBack;

        [Header("Tabs")]
        [SerializeField, Min(0f)] private float _tabEnterOffset = 36f;
        [SerializeField, Min(0.01f)] private float _tabEnterDuration = 0.18f;
        [SerializeField] private Ease _tabEnterEase = Ease.OutCubic;

        [Header("Runtime")]
        [SerializeField] private bool _useUnscaledTime = true;

        public float ScreenShowDuration => _screenShowDuration;
        public float ScreenHideDuration => _screenHideDuration;
        public float ScreenHiddenScale => _screenHiddenScale;
        public Ease ScreenShowEase => _screenShowEase;
        public Ease ScreenHideEase => _screenHideEase;
        public float DimmerDuration => _dimmerDuration;
        public Ease DimmerEase => _dimmerEase;
        public float ButtonPressedScale => _buttonPressedScale;
        public float ButtonPressDuration => _buttonPressDuration;
        public float ButtonReleaseDuration => _buttonReleaseDuration;
        public Ease ButtonPressEase => _buttonPressEase;
        public Ease ButtonReleaseEase => _buttonReleaseEase;
        public float TabEnterOffset => _tabEnterOffset;
        public float TabEnterDuration => _tabEnterDuration;
        public Ease TabEnterEase => _tabEnterEase;
        public bool UseUnscaledTime => _useUnscaledTime;

        private void OnValidate()
        {
            _screenShowDuration = Mathf.Max(0.01f, _screenShowDuration);
            _screenHideDuration = Mathf.Max(0.01f, _screenHideDuration);
            _screenHiddenScale = Mathf.Clamp(_screenHiddenScale, 0.8f, 1f);
            _dimmerDuration = Mathf.Max(0.01f, _dimmerDuration);
            _buttonPressedScale = Mathf.Clamp(_buttonPressedScale, 0.8f, 1f);
            _buttonPressDuration = Mathf.Max(0.01f, _buttonPressDuration);
            _buttonReleaseDuration = Mathf.Max(0.01f, _buttonReleaseDuration);
            _tabEnterOffset = Mathf.Max(0f, _tabEnterOffset);
            _tabEnterDuration = Mathf.Max(0.01f, _tabEnterDuration);
        }
    }
}
