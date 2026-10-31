using System;
using System.Collections.Generic;

namespace PawArena.Core.Inventory
{
    /// <summary>
    /// Serializable ownership and equipment state.
    /// </summary>
    [Serializable]
    public sealed class InventorySnapshot
    {
        public int Version = 2;
        public List<string> OwnedItemIds = new();
        public List<EquipmentEntryData> Equipment = new();
    }
}
