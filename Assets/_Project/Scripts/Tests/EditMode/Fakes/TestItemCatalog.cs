using System;
using System.Collections.Generic;
using PawArena.Core.Inventory;

namespace PawArena.Tests.EditMode.Fakes
{
    /// <summary>
    /// Provides an in-memory item catalog to domain tests.
    /// </summary>
    public sealed class TestItemCatalog : IItemCatalog
    {
        private readonly List<ItemDescriptor> _items = new();
        private readonly Dictionary<string, ItemDescriptor> _itemById = new(StringComparer.Ordinal);

        /// <summary>
        /// Creates a catalog containing the provided descriptors.
        /// </summary>
        public TestItemCatalog(params ItemDescriptor[] items)
        {
            for (var i = 0; i < items.Length; i++)
            {
                _items.Add(items[i]);
                _itemById.Add(items[i].ItemId, items[i]);
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<ItemDescriptor> Items => _items;

        /// <inheritdoc />
        public bool TryGet(string itemId, out ItemDescriptor item)
        {
            item = null;
            return itemId != null && _itemById.TryGetValue(itemId, out item);
        }
    }
}
