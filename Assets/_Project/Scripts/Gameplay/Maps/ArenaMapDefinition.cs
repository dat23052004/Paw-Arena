using System;
using System.Collections.Generic;
using PawArena.Core.Match;
using UnityEngine;

namespace PawArena.Gameplay.Maps
{
    /// <summary>Stores designer-authored presentation data and server matchmaking limits for one map.</summary>
    [CreateAssetMenu(fileName = "ArenaMap", menuName = "Paw Arena/Match/Arena Map")]
    public sealed class ArenaMapDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _mapId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;

        [Header("Presentation")]
        [SerializeField] private Sprite _thumbnail = null;
        [SerializeField] private Color _thumbnailTint = new(0.18f, 0.62f, 0.9f, 1f);
        [SerializeField] private bool _showModeSelection;

        [Header("Runtime")]
        [SerializeField] private GameObject _prefab = null;
        [SerializeField, Min(1)] private int _minimumPlayers = 2;
        [SerializeField, Range(1, AuthoritativeMatchServer.MaximumLobbyMembers)]
        private int _maximumPlayers = 4;
        [SerializeField, Min(1)] private int _spawnPointCount = 4;
        [SerializeField] private MatchMode[] _supportedModes = { MatchMode.Solos };

        /// <summary>Gets the stable server map identifier.</summary>
        public string MapId => _mapId;

        /// <summary>Gets the user-facing map name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Gets the runtime map prefab.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>Gets the optional map-card thumbnail.</summary>
        public Sprite Thumbnail => _thumbnail;

        /// <summary>Gets the fallback color used when the map has no authored thumbnail.</summary>
        public Color ThumbnailTint => _thumbnailTint;

        /// <summary>Gets whether the client should display the server-selected mode cards.</summary>
        public bool ShowModeSelection => _showModeSelection;

        /// <summary>Gets the minimum supported participant count.</summary>
        public int MinimumPlayers => _minimumPlayers;

        /// <summary>Gets the maximum supported participant count.</summary>
        public int MaximumPlayers => _maximumPlayers;

        /// <summary>Gets the authored spawn point count.</summary>
        public int SpawnPointCount => _spawnPointCount;

        /// <summary>Gets the modes available to the authoritative server.</summary>
        public IReadOnlyList<MatchMode> SupportedModes => _supportedModes;

        /// <summary>Creates the Unity-free rule consumed by the authoritative server.</summary>
        public ArenaMapRule ToRule()
        {
            return new ArenaMapRule(
                _mapId,
                _minimumPlayers,
                _maximumPlayers,
                _spawnPointCount,
                _supportedModes);
        }

        private void OnValidate()
        {
            _mapId = _mapId?.Trim() ?? string.Empty;
            _displayName = string.IsNullOrWhiteSpace(_displayName) ? name : _displayName.Trim();
            _minimumPlayers = Mathf.Clamp(_minimumPlayers, 1, AuthoritativeMatchServer.MaximumLobbyMembers);
            _maximumPlayers = Mathf.Clamp(
                _maximumPlayers,
                _minimumPlayers,
                AuthoritativeMatchServer.MaximumLobbyMembers);
            _spawnPointCount = Mathf.Max(_maximumPlayers, _spawnPointCount);
            if (_supportedModes == null || _supportedModes.Length == 0)
            {
                _supportedModes = new[] { MatchMode.Solos };
            }

            for (var index = 0; index < _supportedModes.Length; index++)
            {
                if (!Enum.IsDefined(typeof(MatchMode), _supportedModes[index]))
                {
                    _supportedModes[index] = MatchMode.Solos;
                }
            }
        }
    }
}
