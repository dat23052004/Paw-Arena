using System;

namespace PawArena.Core.Inventory
{
    /// <summary>
    /// Identifies one equipment slot; the index leaves TBD gear and emote counts configurable.
    /// </summary>
    public readonly struct EquipmentSlotId : IEquatable<EquipmentSlotId>
    {
        /// <summary>
        /// Creates a slot identifier.
        /// </summary>
        public EquipmentSlotId(EquipmentSlotType type, int index = 0)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            Type = type;
            Index = index;
        }

        /// <summary>
        /// Gets the equipment slot family.
        /// </summary>
        public EquipmentSlotType Type { get; }

        /// <summary>
        /// Gets the zero-based slot index within its family.
        /// </summary>
        public int Index { get; }

        /// <inheritdoc />
        public bool Equals(EquipmentSlotId other) => Type == other.Type && Index == other.Index;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is EquipmentSlotId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => ((int)Type * 397) ^ Index;

        /// <inheritdoc />
        public override string ToString() => $"{Type}:{Index}";

        public static bool operator ==(EquipmentSlotId left, EquipmentSlotId right) => left.Equals(right);
        public static bool operator !=(EquipmentSlotId left, EquipmentSlotId right) => !left.Equals(right);
    }
}
