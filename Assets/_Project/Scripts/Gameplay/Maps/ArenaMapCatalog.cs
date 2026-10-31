using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawArena.Gameplay.Maps
{
    /// <summary>Provides the two test maps to server and client composition code.</summary>
    [CreateAssetMenu(fileName = "ArenaMapCatalog", menuName = "Paw Arena/Match/Arena Map Catalog")]
    public sealed class ArenaMapCatalog : ScriptableObject
    {
        [SerializeField] private ArenaMapDefinition[] _maps = Array.Empty<ArenaMapDefinition>();

        /// <summary>Gets all configured maps.</summary>
        public IReadOnlyList<ArenaMapDefinition> Maps => _maps;

        /// <summary>Finds a map by its server id.</summary>
        public bool TryGet(string mapId, out ArenaMapDefinition definition)
        {
            for (var index = 0; index < _maps.Length; index++)
            {
                var candidate = _maps[index];
                if (candidate != null && string.Equals(candidate.MapId, mapId, StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
