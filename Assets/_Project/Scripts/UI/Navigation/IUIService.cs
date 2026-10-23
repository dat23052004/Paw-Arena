using System;

namespace PawArena.UI.Navigation
{
    /// <summary>
    /// Owns screen navigation without exposing concrete views to other systems.
    /// </summary>
    public interface IUIService
    {
        /// <summary>
        /// Raised after the active screen changes.
        /// </summary>
        event Action<UIScreenId, UIScreenId> ScreenChanged;

        /// <summary>
        /// Gets the active screen identifier.
        /// </summary>
        UIScreenId CurrentScreen { get; }

        /// <summary>
        /// Gets whether navigation can return to an earlier screen.
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// Pushes a screen onto the navigation stack.
        /// </summary>
        bool Push(UIScreenId screenId);

        /// <summary>
        /// Replaces the active screen without retaining it in history.
        /// </summary>
        bool Replace(UIScreenId screenId);

        /// <summary>
        /// Returns to the previous screen.
        /// </summary>
        bool GoBack();

        /// <summary>
        /// Clears history and opens a new root screen.
        /// </summary>
        bool ResetTo(UIScreenId screenId);
    }
}
