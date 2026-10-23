using System;

namespace PawArena.Core.Inventory
{
    /// <summary>
    /// Serializable mapping between one slot and one equipped item.
    /// </summary>
    [Serializable]
    public sealed class EquipmentEntryData
    {
        public EquipmentSlotType SlotType;
        public int SlotIndex;
        public string ItemId;
    }
}
