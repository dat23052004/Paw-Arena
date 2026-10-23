using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawArena.UI.Navigation
{
    /// <summary>
    /// Provides deterministic screen history, back navigation and click-through protection.
    /// </summary>
    public sealed class UIStackController : MonoBehaviour, IUIService
    {
        [SerializeField] private UIScreenId _initialScreen = UIScreenId.Lobby;
        [SerializeField] private List<UIScreen> _screens = new();

        private readonly Dictionary<UIScreenId, UIScreen> _screenById = new();
        private readonly List<UIScreenId> _history = new();

        /// <inheritdoc />
        public event Action<UIScreenId, UIScreenId> ScreenChanged;

        /// <inheritdoc />
        public UIScreenId CurrentScreen => _history.Count == 0
            ? UIScreenId.None
            : _history[_history.Count - 1];

        /// <inheritdoc />
        public bool CanGoBack => _history.Count > 1;

        private void Awake()
        {
            RebuildRegistry();
            HideAllScreens();
            if (_initialScreen != UIScreenId.None)
            {
                ResetTo(_initialScreen);
            }
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
            nextScreen.Show();
            ScreenChanged?.Invoke(previousId, screenId);
            return true;
        }

        /// <inheritdoc />
        public bool Replace(UIScreenId screenId)
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

            if (_history.Count > 0)
            {
                _history.RemoveAt(_history.Count - 1);
            }

            _history.Add(screenId);
            nextScreen.Show();
            ScreenChanged?.Invoke(previousId, screenId);
            return true;
        }

        /// <inheritdoc />
        public bool GoBack()
        {
            if (!CanGoBack)
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
            _screenById[nextId].Show();
            ScreenChanged?.Invoke(previousId, nextId);
            return true;
        }

        /// <inheritdoc />
        public bool ResetTo(UIScreenId screenId)
        {
            if (!TryResolve(screenId, out var screen))
            {
                return false;
            }

            var previousId = CurrentScreen;
            HideAllScreens();
            _history.Clear();
            _history.Add(screenId);
            screen.Show();
            ScreenChanged?.Invoke(previousId, screenId);
            return true;
        }

        /// <summary>
        /// Handles an Escape, gamepad back or UI close-button intent.
        /// </summary>
        public void HandleBackRequested() => GoBack();

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

        private void HideAllScreens()
        {
            for (var i = 0; i < _screens.Count; i++)
            {
                if (_screens[i] != null)
                {
                    _screens[i].Hide();
                }
            }
        }

        private void OnValidate()
        {
            for (var i = _screens.Count - 1; i >= 0; i--)
            {
                if (_screens[i] == null)
                {
                    _screens.RemoveAt(i);
                }
            }
        }
    }
}
