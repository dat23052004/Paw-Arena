using System;
using UnityEngine;

namespace PawArena.UI.Navigation
{
    /// <summary>
    /// Provides common visibility and input-blocking behavior for a UI screen.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour
    {
        [SerializeField] private UIScreenId _screenId = UIScreenId.None;
        [SerializeField] private CanvasGroup _canvasGroup = null;
        [SerializeField] private bool _keepActiveWhenHidden = false;

        /// <summary>
        /// Raised after the screen becomes visible and interactive.
        /// </summary>
        public event Action Shown;

        /// <summary>
        /// Raised after the screen becomes hidden and non-interactive.
        /// </summary>
        public event Action Hidden;

        /// <summary>
        /// Gets the stable navigation identifier.
        /// </summary>
        public UIScreenId ScreenId => _screenId;

        /// <summary>
        /// Gets whether this screen currently accepts interaction.
        /// </summary>
        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            EnsureCanvasGroup();
        }

        /// <summary>
        /// Makes the screen visible and blocks click-through.
        /// </summary>
        public virtual void Show()
        {
            if (IsVisible)
            {
                return;
            }

            EnsureCanvasGroup();
            gameObject.SetActive(true);
            SetCanvasState(true);
            IsVisible = true;
            Shown?.Invoke();
        }

        /// <summary>
        /// Hides the screen and disables raycast blocking.
        /// </summary>
        public virtual void Hide()
        {
            EnsureCanvasGroup();
            if (!IsVisible && (!gameObject.activeSelf || _canvasGroup.alpha <= 0f))
            {
                return;
            }

            SetCanvasState(false);
            IsVisible = false;
            Hidden?.Invoke();
            if (!_keepActiveWhenHidden)
            {
                gameObject.SetActive(false);
            }
        }

        private void SetCanvasState(bool isVisible)
        {
            _canvasGroup.alpha = isVisible ? 1f : 0f;
            _canvasGroup.interactable = isVisible;
            _canvasGroup.blocksRaycasts = isVisible;
        }

        private void EnsureCanvasGroup()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void OnValidate()
        {
            EnsureCanvasGroup();
        }
    }
}
