using UnityEngine;

namespace PawArena.Gameplay.Maps
{
    /// <summary>Spawn markers belong to a map prefab, not to another Unity scene.</summary>
    [DisallowMultipleComponent]
    public sealed class MapSpawnPoints : MonoBehaviour
    {
        [SerializeField] private Transform[] _points = System.Array.Empty<Transform>();

        /// <summary>Gets the number of authored spawn points.</summary>
        public int Count => _points == null ? 0 : _points.Length;

        /// <summary>Gets a spawn point, or null when the index is outside the map capacity.</summary>
        public Transform GetPoint(int index) => index >= 0 && index < Count ? _points[index] : null;
    }
}
