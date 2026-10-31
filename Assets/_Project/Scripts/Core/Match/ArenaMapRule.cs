using System;
using System.Collections.Generic;

namespace PawArena.Core.Match
{
    /// <summary>
    /// Contains the server-side matchmaking limits for one arena map.
    /// </summary>
    public readonly struct ArenaMapRule
    {
        private static readonly MatchMode[] DefaultModes = { MatchMode.Solos };
        private readonly MatchMode[] _supportedModes;

        /// <summary>
        /// Creates a validated map rule.
        /// </summary>
        public ArenaMapRule(
            string mapId,
            int minimumPlayers,
            int maximumPlayers,
            int spawnPointCount,
            IReadOnlyList<MatchMode> supportedModes = null)
        {
            if (string.IsNullOrWhiteSpace(mapId))
            {
                throw new ArgumentException("Map id cannot be empty.", nameof(mapId));
            }

            if (minimumPlayers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumPlayers));
            }

            if (maximumPlayers < minimumPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPlayers));
            }

            if (spawnPointCount < maximumPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spawnPointCount),
                    "A map must have at least one spawn point for every supported player.");
            }

            MapId = mapId.Trim();
            MinimumPlayers = minimumPlayers;
            MaximumPlayers = maximumPlayers;
            SpawnPointCount = spawnPointCount;
            var sourceModes = supportedModes == null || supportedModes.Count == 0
                ? DefaultModes
                : supportedModes;
            _supportedModes = new MatchMode[sourceModes.Count];
            for (var index = 0; index < sourceModes.Count; index++)
            {
                var mode = sourceModes[index];
                if (!Enum.IsDefined(typeof(MatchMode), mode))
                {
                    throw new ArgumentOutOfRangeException(nameof(supportedModes));
                }

                _supportedModes[index] = mode;
            }
        }

        /// <summary>Gets the stable map identifier.</summary>
        public string MapId { get; }

        /// <summary>Gets the minimum participant count required by the map.</summary>
        public int MinimumPlayers { get; }

        /// <summary>Gets the maximum participant count supported by the map.</summary>
        public int MaximumPlayers { get; }

        /// <summary>Gets the number of authored spawn points on the map.</summary>
        public int SpawnPointCount { get; }

        /// <summary>Gets the modes from which the server may choose for this map.</summary>
        public IReadOnlyList<MatchMode> SupportedModes => _supportedModes ?? DefaultModes;
    }
}
