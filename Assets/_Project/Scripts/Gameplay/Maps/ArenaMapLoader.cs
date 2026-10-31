using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PawArena.Gameplay.Maps
{
    /// <summary>Loads the server-selected map prefab under the existing scene map anchor.</summary>
    [DisallowMultipleComponent]
    public sealed class ArenaMapLoader : MonoBehaviour
    {
        private const string GroundLayerName = "Ground";

        [Header("Permanent Lobby")]
        [SerializeField] private MapSpawnPoints _lobbySpawnPoints = null;

        [Header("Gameplay Arena Placement")]
        [FormerlySerializedAs("_mapAnchor")]
        [SerializeField, Tooltip("Move or rotate this Transform in the Inspector to place every gameplay arena.")]
        private Transform _gameplayMapAnchor = null;
        [SerializeField, Min(0.5f), Tooltip("Vertical distance below the arena origin that counts as a fall.")]
        private float _ringOutDepth = 5f;

        [Header("Map Data")]
        [SerializeField] private ArenaMapCatalog _catalog = null;

        private readonly Dictionary<string, CachedMap> _cachedMaps = new(StringComparer.Ordinal);
        private GameObject _currentMap;

        /// <summary>Gets the permanent MainLobby spawn points.</summary>
        public MapSpawnPoints LobbySpawnPoints => _lobbySpawnPoints;

        /// <summary>Gets the Inspector-positioned parent used by every gameplay arena.</summary>
        public Transform GameplayMapAnchor => _gameplayMapAnchor;

        /// <summary>Gets the currently active map spawn points.</summary>
        public MapSpawnPoints CurrentSpawnPoints { get; private set; }

        /// <summary>Gets the authoritative world-space fall threshold for the current placement.</summary>
        public float RingOutY => (_gameplayMapAnchor == null ? 0f : _gameplayMapAnchor.position.y) - _ringOutDepth;

        /// <summary>Replaces the visible map with the server-selected prefab.</summary>
        public bool TryLoad(string mapId, out MapSpawnPoints spawnPoints)
        {
            spawnPoints = null;
            if (_gameplayMapAnchor == null || _lobbySpawnPoints == null || _catalog == null ||
                string.IsNullOrWhiteSpace(mapId) ||
                !_catalog.TryGet(mapId, out var definition) ||
                definition.Prefab == null)
            {
                Debug.LogError($"Cannot load server-selected map '{mapId}'.", this);
                return false;
            }

            DeactivateGameplayMaps();
            if (!_cachedMaps.TryGetValue(mapId, out var cachedMap) || cachedMap.Instance == null)
            {
                _cachedMaps.Remove(mapId);
                var instance = Instantiate(definition.Prefab, _gameplayMapAnchor);
                instance.name = definition.Prefab.name;
                ApplyGroundLayer(instance.transform);
                cachedMap = new CachedMap(instance, instance.GetComponent<MapSpawnPoints>());
                _cachedMaps.Add(mapId, cachedMap);
            }

            var mapTransform = cachedMap.Instance.transform;
            mapTransform.SetParent(_gameplayMapAnchor, false);
            mapTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            mapTransform.localScale = Vector3.one;
            CurrentSpawnPoints = cachedMap.SpawnPoints;
            if (CurrentSpawnPoints == null || CurrentSpawnPoints.Count < definition.MaximumPlayers)
            {
                Debug.LogError($"Map '{mapId}' does not contain enough spawn points.", cachedMap.Instance);
                _cachedMaps.Remove(mapId);
                DestroyMap(cachedMap.Instance);
                CurrentSpawnPoints = null;
                _currentMap = null;
                return false;
            }

            cachedMap.Instance.SetActive(true);
            _currentMap = cachedMap.Instance;
            spawnPoints = CurrentSpawnPoints;
            return true;
        }

        /// <summary>Removes only the transient gameplay arena and preserves MainLobby.</summary>
        public void UnloadGameplayMap()
        {
            if (_gameplayMapAnchor == null)
            {
                return;
            }

            DeactivateGameplayMaps();
            _currentMap = null;
            CurrentSpawnPoints = null;
        }

        private void OnDestroy()
        {
            foreach (var cachedMap in _cachedMaps.Values)
            {
                DestroyMap(cachedMap.Instance);
            }

            _cachedMaps.Clear();
            _currentMap = null;
            CurrentSpawnPoints = null;
        }

        private void DeactivateGameplayMaps()
        {
            if (_currentMap != null && _currentMap.activeSelf)
            {
                _currentMap.SetActive(false);
            }

            for (var index = _gameplayMapAnchor.childCount - 1; index >= 0; index--)
            {
                var child = _gameplayMapAnchor.GetChild(index).gameObject;
                if (child.activeSelf)
                {
                    child.SetActive(false);
                }
            }
        }

        private static void DestroyMap(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        private static void ApplyGroundLayer(Transform root)
        {
            var groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (groundLayer < 0)
            {
                return;
            }

            ApplyGroundLayer(root, groundLayer);
        }

        private static void ApplyGroundLayer(Transform current, int groundLayer)
        {
            var currentLayer = current.gameObject.layer;
            if (currentLayer == 0 || currentLayer == groundLayer)
            {
                current.gameObject.layer = groundLayer;
            }

            for (var index = 0; index < current.childCount; index++)
            {
                ApplyGroundLayer(current.GetChild(index), groundLayer);
            }
        }

        private readonly struct CachedMap
        {
            public CachedMap(GameObject instance, MapSpawnPoints spawnPoints)
            {
                Instance = instance;
                SpawnPoints = spawnPoints;
            }

            public GameObject Instance { get; }
            public MapSpawnPoints SpawnPoints { get; }
        }
    }
}
