namespace SilkroadAIBot.Domain.Entities
{
    /// <summary>
    /// Immutable reference object info loaded from PK2 / SQLite.
    /// Shared between monsters, NPCs, and items.
    /// </summary>
    public record SRModelInfo
    {
        /// <summary>Reference object ID.</summary>
        public uint ID { get; init; }

        /// <summary>Internal code name.</summary>
        public string CodeName { get; init; } = string.Empty;

        /// <summary>Localized display name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Top-level type ID (1=Bionic, 3=Item, 4=Portal, etc.).</summary>
        public byte TypeID1 { get; init; }

        /// <summary>Second-level type ID.</summary>
        public byte TypeID2 { get; init; }

        /// <summary>Third-level type ID.</summary>
        public byte TypeID3 { get; init; }

        /// <summary>Fourth-level type ID.</summary>
        public byte TypeID4 { get; init; }

        /// <summary>Icon file path for UI display.</summary>
        public string IconPath { get; init; } = string.Empty;
    }

    /// <summary>
    /// Immutable record describing a bot training area.
    /// Center + radius define the region; <see cref="IsEnabled"/> gates it.
    /// </summary>
    public record TrainingArea
    {
        /// <summary>Center coordinate of the training area.</summary>
        public SRCoord Center { get; init; }

        /// <summary>Radius in game units around <see cref="Center"/>.</summary>
        public int Radius { get; init; } = 50;

        /// <summary>Whether training-area enforcement is active.</summary>
        public bool IsEnabled { get; init; }

        /// <summary>Returns true if <paramref name="pos"/> is within the training area (or if disabled).</summary>
        public bool IsInRange(SRCoord pos)
        {
            if (!IsEnabled) return true;
            return pos.DistanceTo(Center) <= Radius;
        }

        /// <summary>Disabled placeholder.</summary>
        public static TrainingArea Disabled { get; } = new TrainingArea();
    }


    public record Quest
    {
        public uint ID { get; init; }
        public string Name { get; init; } = string.Empty;
        public SilkroadAIBot.Domain.Enums.QuestStatus Status { get; init; }
    }
}
