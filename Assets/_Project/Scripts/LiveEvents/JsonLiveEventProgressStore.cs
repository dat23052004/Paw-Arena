using System;
using System.IO;
using UnityEngine;

namespace PawArena.LiveEvents
{
    /// <summary>Stores versioned event progress beneath the Unity persistent-data folder.</summary>
    public sealed class JsonLiveEventProgressStore : ILiveEventProgressStore
    {
        private readonly string _directory;

        public JsonLiveEventProgressStore(string rootDirectory = null)
        {
            var root = string.IsNullOrWhiteSpace(rootDirectory)
                ? Application.persistentDataPath
                : rootDirectory;
            _directory = Path.Combine(root, "LiveEvents");
        }

        public bool TryLoad(string playerId, out LiveEventProgressSnapshot snapshot)
        {
            snapshot = null;
            var path = GetPath(playerId);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                snapshot = JsonUtility.FromJson<LiveEventProgressSnapshot>(File.ReadAllText(path));
                return snapshot != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Live-event progress could not be loaded: {exception.Message}");
                snapshot = null;
                return false;
            }
        }

        public void Save(string playerId, LiveEventProgressSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Directory.CreateDirectory(_directory);
            var path = GetPath(playerId);
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(snapshot, true));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temporaryPath, path);
        }

        private string GetPath(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player ID is required.", nameof(playerId));
            }

            var safePlayerId = playerId;
            var invalidCharacters = Path.GetInvalidFileNameChars();
            for (var i = 0; i < invalidCharacters.Length; i++)
            {
                safePlayerId = safePlayerId.Replace(invalidCharacters[i], '_');
            }

            return Path.Combine(_directory, safePlayerId + ".json");
        }
    }
}
