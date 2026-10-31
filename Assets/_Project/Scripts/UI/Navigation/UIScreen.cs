using System;
using PawArena.UI.Motion;
using TMPro;
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
        [SerializeField] private UIScreenTransition _transition = null;
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
        /// Gets whether this screen is opening or currently visible.
        /// </summary>
        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            EnsureDependencies();
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

            EnsureDependencies();
            RuntimeUiState.SetActive(gameObject, true);
            IsVisible = true;
            SetCanvasInteraction(false);

            if (_transition != null)
            {
                _transition.PlayShow(CompleteShow);
            }
            else
            {
                CompleteShow();
            }
        }

        /// <summary>
        /// Hides the screen and disables raycast blocking.
        /// </summary>
        public virtual void Hide()
        {
            EnsureDependencies();
            if (!IsVisible && (!gameObject.activeSelf || _canvasGroup.alpha <= 0f))
            {
                return;
            }

            IsVisible = false;
            SetCanvasInteraction(false);

            if (_transition != null && gameObject.activeInHierarchy)
            {
                _transition.PlayHide(CompleteHide);
            }
            else
            {
                CompleteHide();
            }
        }

        /// <summary>Hides the screen without playing motion, used during initial UI setup.</summary>
        public void HideImmediately()
        {
            EnsureDependencies();
            var shouldNotify = IsVisible ||
                               (gameObject.activeSelf && _canvasGroup != null && _canvasGroup.alpha > 0f);
            IsVisible = false;
            _transition?.SetImmediate(false);
            SetCanvasState(false);
            if (shouldNotify)
            {
                Hidden?.Invoke();
            }

            if (!_keepActiveWhenHidden)
            {
                RuntimeUiState.SetActive(gameObject, false);
            }
        }

        private void CompleteShow()
        {
            if (!IsVisible)
            {
                return;
            }

            SetCanvasState(true);
            Shown?.Invoke();
        }

        private void CompleteHide()
        {
            if (IsVisible)
            {
                return;
            }

            SetCanvasState(false);
            Hidden?.Invoke();
            if (!_keepActiveWhenHidden)
            {
                RuntimeUiState.SetActive(gameObject, false);
            }
        }

        private void SetCanvasState(bool isVisible)
        {
            RuntimeUiState.SetCanvasGroup(
                _canvasGroup,
                isVisible ? 1f : 0f,
                isVisible,
                isVisible);
        }

        private void SetCanvasInteraction(bool enabled)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.interactable = enabled;
            _canvasGroup.blocksRaycasts = enabled;
        }

        private void EnsureDependencies()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            if (_transition == null)
            {
                _transition = GetComponent<UIScreenTransition>();
            }
        }

        private void OnValidate()
        {
            EnsureDependencies();
        }
    }

    /// <summary>Applies high-frequency UI state only when it actually changes.</summary>
    internal static class RuntimeUiState
    {
        public static void SetText(TMP_Text target, string value)
        {
            if (target == null)
            {
                return;
            }

            value ??= string.Empty;
            if (!string.Equals(target.text, value, StringComparison.Ordinal))
            {
                target.text = value;
            }
        }

        public static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        public static void SetCanvasGroup(
            CanvasGroup target,
            float alpha,
            bool interactable,
            bool blocksRaycasts)
        {
            if (target == null)
            {
                return;
            }

            if (!Mathf.Approximately(target.alpha, alpha))
            {
                target.alpha = alpha;
            }

            if (target.interactable != interactable)
            {
                target.interactable = interactable;
            }

            if (target.blocksRaycasts != blocksRaycasts)
            {
                target.blocksRaycasts = blocksRaycasts;
            }
        }

        /// <summary>
        /// Splits non-interactive, frequently changing graphics from their parent canvas rebuild.
        /// A raycaster is intentionally not added because these roots never accept input.
        /// </summary>
        public static Canvas EnsureNonInteractiveNestedCanvas(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            var canvas = target.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = target.AddComponent<Canvas>();
            }

            canvas.overrideSorting = false;
            return canvas;
        }
    }
}
