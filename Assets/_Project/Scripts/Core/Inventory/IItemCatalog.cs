using System.Collections.Generic;

namespace PawArena.Core.Inventory
{
    /// <summary>
    /// Exposes versioned item definitions to domain services.
    /// </summary>
    public interface IItemCatalog
    {
        /// <summary>
        /// Gets all valid catalog entries.
        /// </summary>
        IReadOnlyList<ItemDescriptor> Items { get; }

        /// <summary>
        /// Attempts to find an item by stable identifier.
        /// </summary>
        bool TryGet(string itemId, out ItemDescriptor item);
    }
}
