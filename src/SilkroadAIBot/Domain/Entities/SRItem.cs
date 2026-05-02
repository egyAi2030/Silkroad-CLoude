namespace SilkroadAIBot.Domain.Entities
{
    /// <summary>
    /// Immutable record representing an item instance in an inventory or on the ground.
    /// Uses <see cref="SRModelInfo"/> for static data and tracks instance-specific state.
    /// </summary>
    public record SRItem
    {
        /// <summary>Inventory slot index (0-indexed or 1-indexed depending on implementation).</summary>
        public byte Slot { get; init; }

        /// <summary>Server-assigned unique item instance ID.</summary>
        public uint ItemID { get; init; }

        /// <summary>Reference Model ID from PK2/RefObjCommon.</summary>
        public uint ModelID { get; init; }

        /// <summary>Current stack size or durability.</summary>
        public uint Count { get; init; }

        /// <summary>Alias for Count to support legacy code.</summary>
        public uint Amount => Count;

        /// <summary>Localized display name (cached for convenience).</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Item category ID (e.g. 1=Equipment, 3=Consumable).</summary>
        public byte TypeID1 { get; init; }
        public byte TypeID2 { get; init; }
        public byte TypeID3 { get; init; }
        public byte TypeID4 { get; init; }

        /// <summary>True if the item is a potion (TypeID1=3, TypeID2=1).</summary>
        public bool IsPotion => TypeID1 == 3 && TypeID2 == 1;

        /// <summary>True if the item is an HP potion (TypeID3=1).</summary>
        public bool IsHpPotion => IsPotion && TypeID3 == 1;

        /// <summary>True if the item is an MP potion (TypeID3=2).</summary>
        public bool IsMpPotion => IsPotion && TypeID3 == 2;
    }
}
