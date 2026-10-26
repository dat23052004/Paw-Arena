using UnityEngine;

namespace PawArena.Gameplay.Maps
{
    /// <summary>Spawn markers belong to a map prefab, not to another Unity scene.</summary>
    public sealed class MapSpawnPoints : MonoBehaviour
    {
        [SerializeField] private Transform[] _points;
        public int Count => _points == null ? 0 : _points.Length;
        public Transform GetPoint(int index) => index >= 0 && index < Count ? _points[index] : null;
    }
}
