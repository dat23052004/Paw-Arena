using System;
using System.IO;
using PawArena.Core.Profiles;
using UnityEngine;

namespace PawArena.Infrastructure.Persistence
{
    /// <summary>
    /// Persists the offline profile as versioned JSON under persistentDataPath.
    /// </summary>
    public sealed class JsonProfileStore : IProfileStore
    {
        private const string ProfilesDirectoryName = "Profiles";
        private const string FileExtension = ".json";
        private const string BackupExtension = ".backup";
        private const string TemporaryExtension = ".tmp";

        private readonly string _profilesDirectory;

        /// <summary>
        /// Creates a store at the default or provided root directory.
        /// </summary>
        public JsonProfileStore(string rootDirectory = null)
        {
            var baseDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Application.persistentDataPath
                : rootDirectory;
            _profilesDirectory = Path.Combine(baseDirectory, ProfilesDirectoryName);
        }

        /// <inheritdoc />
        public bool TryLoad(string playerId, out PlayerProfileSnapshot snapshot)
        {
            snapshot = null;
            var profilePath = GetProfilePath(playerId);
            if (!File.Exists(profilePath))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(profilePath);
                snapshot = JsonUtility.FromJson<PlayerProfileSnapshot>(json);
                if (snapshot == null ||
                    !string.Equals(snapshot.PlayerId, playerId, StringComparison.Ordinal))
                {
                    snapshot = null;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to load profile '{playerId}': {exception.Message}");
                snapshot = null;
                return false;
            }
        }

        /// <inheritdoc />
        public void Save(PlayerProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (string.IsNullOrWhiteSpace(snapshot.PlayerId))
            {
                throw new ArgumentException("Profile player ID is required.", nameof(snapshot));
            }

            Directory.CreateDirectory(_profilesDirectory);
            var profilePath = GetProfilePath(snapshot.PlayerId);
            var temporaryPath = profilePath + TemporaryExtension;
            var backupPath = profilePath + BackupExtension;
            var json = JsonUtility.ToJson(snapshot, true);

            File.WriteAllText(temporaryPath, json);
            if (File.Exists(profilePath))
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Replace(temporaryPath, profilePath, backupPath, true);
            }
            else
            {
                File.Move(temporaryPath, profilePath);
            }
        }

        /// <inheritdoc />
        public bool Delete(string playerId)
        {
            var profilePath = GetProfilePath(playerId);
            if (!File.Exists(profilePath))
            {
                return false;
            }

            File.Delete(profilePath);
            var backupPath = profilePath + BackupExtension;
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            return true;
        }

        private string GetProfilePath(string playerId)
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

            return Path.Combine(_profilesDirectory, safePlayerId + FileExtension);
        }
    }
}
