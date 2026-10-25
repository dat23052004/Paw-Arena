namespace PawArena.Core.Inventory
{
    /// <summary>Stable save identifiers. Keep existing EquipmentSlotType enum values unchanged.</summary>
    public static class CatGearSlots
    {
        public const int Count = 3;
        public static readonly EquipmentSlotId Head = new(EquipmentSlotType.Gear, 0);
        public static readonly EquipmentSlotId Waist = new(EquipmentSlotType.Gear, 1);
        public static readonly EquipmentSlotId Hand = new(EquipmentSlotType.Gear, 2);

        /// <summary>Only the three implemented cosmetic slots are supported.</summary>
        public static bool Contains(EquipmentSlotId slot) => slot.Type == EquipmentSlotType.Gear && slot.Index < Count;
    }
}
