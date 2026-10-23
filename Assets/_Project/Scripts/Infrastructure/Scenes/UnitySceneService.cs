using System;
using System.Collections;
using PawArena.Core.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawArena.Infrastructure.Scenes
{
    /// <summary>
    /// Loads build scenes asynchronously and reports normalized progress.
    /// </summary>
    public sealed class UnitySceneService : MonoBehaviour, ISceneService
    {
        /// <inheritdoc />
        public event Action<string> SceneLoadStarted;

        /// <inheritdoc />
        public event Action<float> SceneLoadProgressChanged;

        /// <inheritdoc />
        public event Action<string> SceneLoaded;

        /// <inheritdoc />
        public event Action<string, string> SceneLoadFailed;

        /// <inheritdoc />
        public bool IsLoading { get; private set; }

        /// <inheritdoc />
        public string ActiveSceneName => SceneManager.GetActiveScene().name;

        /// <inheritdoc />
        public bool TryLoadScene(string sceneName, SceneLoadMode mode = SceneLoadMode.Single)
        {
            if (IsLoading)
            {
                SceneLoadFailed?.Invoke(sceneName, "Another scene load is already active.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                SceneLoadFailed?.Invoke(string.Empty, "Scene name is required.");
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneLoadFailed?.Invoke(sceneName, "Scene is not included in Build Settings.");
                return false;
            }

            StartCoroutine(LoadSceneRoutine(sceneName, mode));
            return true;
        }

        private IEnumerator LoadSceneRoutine(string sceneName, SceneLoadMode mode)
        {
            IsLoading = true;
            SceneLoadStarted?.Invoke(sceneName);
            SceneLoadProgressChanged?.Invoke(0f);

            var unityLoadMode = mode == SceneLoadMode.Additive
                ? LoadSceneMode.Additive
                : LoadSceneMode.Single;
            var operation = SceneManager.LoadSceneAsync(sceneName, unityLoadMode);
            if (operation == null)
            {
                IsLoading = false;
                SceneLoadFailed?.Invoke(sceneName, "Unity did not create a scene load operation.");
                yield break;
            }

            while (!operation.isDone)
            {
                SceneLoadProgressChanged?.Invoke(Mathf.Clamp01(operation.progress / 0.9f));
                yield return null;
            }

            IsLoading = false;
            SceneLoadProgressChanged?.Invoke(1f);
            SceneLoaded?.Invoke(sceneName);
        }
    }
}
