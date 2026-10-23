using System;
using System.Collections.Generic;
using PawArena.Core.Inventory;
using UnityEngine;

namespace PawArena.Gameplay.Inventory
{
    /// <summary>
    /// Provides a validated, versioned inventory catalog to domain services.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Paw Arena/Inventory/Catalog")]
    public sealed class ItemCatalog : ScriptableObject, IItemCatalog
    {
        [SerializeField, Min(1)] private int _catalogVersion = 1;
        [SerializeField] private List<ItemDefinition> _definitions = new();

        private readonly List<ItemDescriptor> _items = new();
        private readonly Dictionary<string, ItemDescriptor> _itemById = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets the catalog version used by balancing and migration.
        /// </summary>
        public int CatalogVersion => _catalogVersion;

        /// <inheritdoc />
        public IReadOnlyList<ItemDescriptor> Items
        {
            get
            {
                EnsureCache();
                return _items;
            }
        }

        /// <inheritdoc />
        public bool TryGet(string itemId, out ItemDescriptor item)
        {
            item = null;
            EnsureCache();
            return !string.IsNullOrWhiteSpace(itemId) && _itemById.TryGetValue(itemId, out item);
        }

        private void OnEnable() => RebuildCache(false);

        private void OnValidate()
        {
            _catalogVersion = Mathf.Max(1, _catalogVersion);
            RebuildCache(true);
        }

        private void EnsureCache()
        {
            if (_itemById.Count == 0 && _definitions.Count > 0)
            {
                RebuildCache(false);
            }
        }

        private void RebuildCache(bool logErrors)
        {
            _items.Clear();
            _itemById.Clear();

            for (var i = 0; i < _definitions.Count; i++)
            {
                var definition = _definitions[i];
                if (definition == null)
                {
                    continue;
                }

                ItemDescriptor descriptor;
                try
                {
                    descriptor = definition.BuildDescriptor();
                }
                catch (Exception exception)
                {
                    if (logErrors)
                    {
                        Debug.LogError(
                            $"Invalid item definition '{definition.name}': {exception.Message}",
                            definition);
                    }

                    continue;
                }

                if (!_itemById.TryAdd(descriptor.ItemId, descriptor))
                {
                    if (logErrors)
                    {
                        Debug.LogError($"Duplicate item ID '{descriptor.ItemId}'.", definition);
                    }

                    continue;
                }

                _items.Add(descriptor);
            }
        }
    }
}
