using System;

namespace PawArena.Core.Scenes
{
    /// <summary>
    /// Coordinates asynchronous Unity scene changes behind a domain-safe contract.
    /// </summary>
    public interface ISceneService
    {
        /// <summary>
        /// Raised when a scene load starts.
        /// </summary>
        event Action<string> SceneLoadStarted;

        /// <summary>
        /// Raised when load progress changes from zero to one.
        /// </summary>
        event Action<float> SceneLoadProgressChanged;

        /// <summary>
        /// Raised after a requested scene finishes loading.
        /// </summary>
        event Action<string> SceneLoaded;

        /// <summary>
        /// Raised when a scene load cannot be started or completed.
        /// </summary>
        event Action<string, string> SceneLoadFailed;

        /// <summary>
        /// Gets whether a scene operation is currently active.
        /// </summary>
        bool IsLoading { get; }

        /// <summary>
        /// Gets the active Unity scene name.
        /// </summary>
        string ActiveSceneName { get; }

        /// <summary>
        /// Attempts to begin loading a configured build scene.
        /// </summary>
        bool TryLoadScene(string sceneName, SceneLoadMode mode = SceneLoadMode.Single);
    }
}
