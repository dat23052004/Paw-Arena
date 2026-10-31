using System;
using System.Collections.Generic;
using PawArena.UI.Motion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PawArena.UI.Navigation
{
    /// <summary>
    /// Coordinates HUD navigation and guarantees that only one modal screen accepts input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour, IUIService
    {
        [Header("Presentation")]
        [SerializeField] private GameObject _modalDimmer = null;
        [SerializeField] private UIModalDimmerTransition _modalDimmerTransition = null;
        [SerializeField] private UIScreenId _initialScreen = UIScreenId.None;

        [Header("Screens")]
        [SerializeField] private List<UIScreen> _screens = new();

        [Header("Navigation")]
        [SerializeField] private List<NavigationBinding> _navigation = new();
        [SerializeField] private List<Button> _closeButtons = new();

        private readonly Dictionary<UIScreenId, UIScreen> _screenById = new();
        private readonly List<UIScreenId> _history = new();
        private readonly List<ButtonListener> _listeners = new();
        private bool _initialized;

        /// <inheritdoc />
        public event Action<UIScreenId, UIScreenId> ScreenChanged;

        /// <inheritdoc />
        public UIScreenId CurrentScreen => _history.Count == 0
            ? UIScreenId.None
            : _history[_history.Count - 1];

        /// <inheritdoc />
        public bool CanGoBack => _history.Count > 0;

        private void Awake() => Initialize();

        /// <summary>Builds the screen registry and binds authored navigation controls once.</summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            RebuildRegistry();
            UIRaycastTargetOptimizer.Optimize(transform);
            BindControls();
            HideAllScreens(true);
            SetDimmerVisible(false, true);
            _initialized = true;

            if (_initialScreen != UIScreenId.None)
            {
                ResetTo(_initialScreen);
            }
        }

        private void OnDestroy()
        {
            UnbindControls();
            _initialized = false;
        }

        /// <summary>Opens a screen or closes it when the same toggle route is pressed again.</summary>
        public bool Toggle(UIScreenId screenId)
        {
            if (CurrentScreen == screenId)
            {
                return CloseAll();
            }

            return Replace(screenId);
        }

        /// <inheritdoc />
        public bool Push(UIScreenId screenId)
        {
            if (!TryResolve(screenId, out var nextScreen) || CurrentScreen == screenId)
            {
                return false;
            }

            var previousId = CurrentScreen;
            if (TryResolve(previousId, out var previousScreen))
            {
                previousScreen.Hide();
            }

            _history.Add(screenId);
            Show(nextScreen);
            ScreenChanged?.Invoke(previousId, screenId);
            return true;
        }

        /// <inheritdoc />
        public bool Replace(UIScreenId screenId)
        {
            if (!TryResolve(screenId, out var nextScreen))
            {
                return false;
            }

            var previousId = CurrentScreen;
            if (previousId == screenId)
            {
                return false;
            }

            HideAllScreens(false);
            _history.Clear();
            _history.Add(screenId);
            Show(nextScreen);
            ScreenChanged?.Invoke(previousId, screenId);
            return true;
        }

        /// <inheritdoc />
        public bool GoBack()
        {
            if (_history.Count == 0)
            {
                return false;
            }

            var previousId = CurrentScreen;
            if (TryResolve(previousId, out var previousScreen))
            {
                previousScreen.Hide();
            }

            _history.RemoveAt(_history.Count - 1);
            var nextId = CurrentScreen;
            if (TryResolve(nextId, out var nextScreen))
            {
                Show(nextScreen);
            }
            else
            {
                SetDimmerVisible(false);
            }

            ScreenChanged?.Invoke(previousId, nextId);
            return true;
        }

        /// <inheritdoc />
        public bool ResetTo(UIScreenId screenId) => Replace(screenId);

        /// <inheritdoc />
        public bool CloseAll()
        {
            var previousId = CurrentScreen;
            if (previousId == UIScreenId.None)
            {
                SetDimmerVisible(false);
                return false;
            }

            HideAllScreens(false);
            _history.Clear();
            SetDimmerVisible(false);
            ScreenChanged?.Invoke(previousId, UIScreenId.None);
            return true;
        }

        /// <summary>Handles an Escape, gamepad back or close-button intent.</summary>
        public void HandleBackRequested() => GoBack();

        private void Show(UIScreen screen)
        {
            SetDimmerVisible(true);
            screen.Show();
        }

        private void BindControls()
        {
            UnbindControls();
            for (var i = 0; i < _navigation.Count; i++)
            {
                var binding = _navigation[i];
                if (binding.Button == null || binding.ScreenId == UIScreenId.None)
                {
                    continue;
                }

                var captured = binding;
                UnityAction action = () =>
                {
                    if (captured.ToggleWhenActive)
                    {
                        Toggle(captured.ScreenId);
                    }
                    else
                    {
                        Replace(captured.ScreenId);
                    }
                };
                binding.Button.onClick.AddListener(action);
                _listeners.Add(new ButtonListener(binding.Button, action));
            }

            for (var i = 0; i < _closeButtons.Count; i++)
            {
                var button = _closeButtons[i];
                if (button == null)
                {
                    continue;
                }

                UnityAction action = HandleBackRequested;
                button.onClick.AddListener(action);
                _listeners.Add(new ButtonListener(button, action));
            }
        }

        private void UnbindControls()
        {
            for (var i = 0; i < _listeners.Count; i++)
            {
                _listeners[i].Button.onClick.RemoveListener(_listeners[i].Action);
            }

            _listeners.Clear();
        }

        private bool TryResolve(UIScreenId screenId, out UIScreen screen)
        {
            screen = null;
            return screenId != UIScreenId.None && _screenById.TryGetValue(screenId, out screen);
        }

        private void RebuildRegistry()
        {
            _screenById.Clear();
            for (var i = 0; i < _screens.Count; i++)
            {
                var screen = _screens[i];
                if (screen == null)
                {
                    continue;
                }

                if (screen.ScreenId == UIScreenId.None)
                {
                    Debug.LogError($"Screen '{screen.name}' has no {nameof(UIScreenId)}.", screen);
                    continue;
                }

                if (!_screenById.TryAdd(screen.ScreenId, screen))
                {
                    Debug.LogError($"Duplicate UI screen ID '{screen.ScreenId}'.", screen);
                }
            }
        }

        private void HideAllScreens(bool immediate)
        {
            for (var i = 0; i < _screens.Count; i++)
            {
                if (_screens[i] != null)
                {
                    if (immediate)
                    {
                        _screens[i].HideImmediately();
                    }
                    else
                    {
                        _screens[i].Hide();
                    }
                }
            }
        }

        private void SetDimmerVisible(bool visible, bool immediate = false)
        {
            if (_modalDimmerTransition != null)
            {
                _modalDimmerTransition.SetVisible(visible, immediate);
                return;
            }

            if (_modalDimmer != null && _modalDimmer.activeSelf != visible)
            {
                _modalDimmer.SetActive(visible);
            }
        }

        private void OnValidate()
        {
            RemoveMissing(_screens);
            RemoveMissing(_closeButtons);
        }

        private static void RemoveMissing<T>(List<T> items) where T : UnityEngine.Object
        {
            for (var i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] == null)
                {
                    items.RemoveAt(i);
                }
            }
        }

        [Serializable]
        private sealed class NavigationBinding
        {
            [SerializeField] private Button _button = null;
            [SerializeField] private UIScreenId _screenId = UIScreenId.None;
            [SerializeField] private bool _toggleWhenActive = true;

            public Button Button => _button;
            public UIScreenId ScreenId => _screenId;
            public bool ToggleWhenActive => _toggleWhenActive;
        }

        private readonly struct ButtonListener
        {
            public ButtonListener(Button button, UnityAction action)
            {
                Button = button;
                Action = action;
            }

            public Button Button { get; }
            public UnityAction Action { get; }
        }
    }

    /// <summary>Disables decorative uGUI raycast targets while preserving authored interaction surfaces.</summary>
    internal static class UIRaycastTargetOptimizer
    {
        private static readonly HashSet<Graphic> RequiredTargets = new();

        /// <summary>Runs once when the UI composition root initializes.</summary>
        public static void Optimize(Transform root)
        {
            if (root == null)
            {
                return;
            }

            RequiredTargets.Clear();
            CollectSelectableTargets(root);
            CollectScrollTargets(root);
            CollectExplicitEventTargets(root);

            var graphics = root.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic != null && graphic.raycastTarget && !RequiredTargets.Contains(graphic))
                {
                    graphic.raycastTarget = false;
                }
            }

            RequiredTargets.Clear();
        }

        private static void CollectSelectableTargets(Transform root)
        {
            var selectables = root.GetComponentsInChildren<Selectable>(true);
            for (var i = 0; i < selectables.Length; i++)
            {
                var target = selectables[i] != null ? selectables[i].targetGraphic : null;
                if (target != null)
                {
                    RequiredTargets.Add(target);
                }
            }
        }

        private static void CollectScrollTargets(Transform root)
        {
            var scrollRects = root.GetComponentsInChildren<ScrollRect>(true);
            for (var i = 0; i < scrollRects.Length; i++)
            {
                var scrollRect = scrollRects[i];
                if (scrollRect == null)
                {
                    continue;
                }

                AddGraphic(scrollRect.GetComponent<Graphic>());
                if (scrollRect.viewport != null)
                {
                    AddGraphic(scrollRect.viewport.GetComponent<Graphic>());
                }
            }
        }

        private static void CollectExplicitEventTargets(Transform root)
        {
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour is IEventSystemHandler)
                {
                    AddGraphic(behaviour.GetComponent<Graphic>());
                }
            }
        }

        private static void AddGraphic(Graphic graphic)
        {
            if (graphic != null)
            {
                RequiredTargets.Add(graphic);
            }
        }
    }
}
