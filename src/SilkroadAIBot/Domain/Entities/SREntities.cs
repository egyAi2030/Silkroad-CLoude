using SilkroadAIBot.Domain.Enums;

namespace SilkroadAIBot.Domain.Entities
{
    /// <summary>
    /// Base immutable record for every tracked game-world entity.
    /// Identity is determined solely by <see cref="UniqueID"/>.
    /// Use <c>with</c>-expressions to produce updated copies.
    /// </summary>
    public abstract record SREntity
    {
        /// <summary>Server-assigned unique identifier (runtime identity).</summary>
        public required uint UniqueID { get; init; }

        /// <summary>Reference object / model ID (links to DB / PK2 data).</summary>
        public required uint ModelID { get; init; }

        /// <summary>Display name resolved from textdata or packet.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Current world position.</summary>
        public SRCoord Position { get; init; }

        /// <summary>Broad entity classification.</summary>
        public EntityType EntityType { get; init; }

        /// <summary>Current life state (alive / dead).</summary>
        public LifeState LifeState { get; init; } = LifeState.Alive;

        /// <summary>Alias for LifeState to support legacy code.</summary>
        public LifeState LifeStateType => LifeState;

        /// <summary>Current motion state.</summary>
        public MotionState MotionState { get; init; } = MotionState.Idle;

        /// <summary>Alias for MotionState to support legacy code.</summary>
        public MotionState MotionStateType => MotionState;

        /// <summary>Current HP. Use <c>with { HP = value }</c> to update.</summary>
        public uint HP { get; init; }

        /// <summary>Maximum HP.</summary>
        public uint HPMax { get; init; }

        /// <summary>Current MP.</summary>
        public uint MP { get; init; }

        /// <summary>Maximum MP.</summary>
        public uint MPMax { get; init; }

        /// <summary>HP as a 0–100 percentage (used for mobs when exact HP unknown).</summary>
        public byte HealthPercent { get; init; }

        /// <summary>Entity facing angle (0–65535).</summary>
        public ushort Angle { get; init; }

        /// <summary>Destination position (when moving).</summary>
        public SRCoord MovementDestination { get; init; }
    }

    /// <summary>
    /// An entity with bionic (living creature) properties.
    /// Extended by <see cref="SRMob"/>, <see cref="SRPlayer"/>, <see cref="SRNpc"/>.
    /// </summary>
    public abstract record SRBionic : SREntity;

    /// <summary>Immutable record for a monster / non-player creature.</summary>
    public record SRMob : SRBionic
    {
        /// <summary>Rarity class of this monster (Normal, Champion, Unique, etc.).</summary>
        public MobRarity Rarity { get; init; } = MobRarity.Normal;

        /// <summary>Character level read from PK2 data.</summary>
        public byte Level { get; init; }
    }

    /// <summary>Immutable record for any NPC (trader, guard, teleport NPC, etc.).</summary>
    public record SRNpc : SRBionic
    {
        /// <summary>True if this NPC currently carries a quest marker.</summary>
        public bool HasQuest { get; init; }
    }

    /// <summary>Immutable record for another player visible in the game world.</summary>
    public record SRPlayer : SRBionic
    {
        /// <summary>Character level.</summary>
        public byte Level { get; init; }

        /// <summary>Guild name, empty if unguilded.</summary>
        public string GuildName { get; init; } = string.Empty;

        /// <summary>Character race (Chinese / European).</summary>
        public CharacterRace Race { get; init; }

        /// <summary>Job type byte (0 = none, 1 = trader, 2 = thief, 3 = hunter).</summary>
        public byte JobType { get; init; }
    }

    /// <summary>Immutable record for a dropped item lying on the ground.</summary>
    public record SRGroundItem : SREntity
    {
        /// <summary>Reference item ID (same as ModelID for most drops).</summary>
        public uint ItemID { get; init; }

        /// <summary>Stack count (relevant for gold and stackable drops).</summary>
        public uint Count { get; init; }

        /// <summary>Owner player name; empty means anyone can pick up.</summary>
        public string Owner { get; init; } = string.Empty;

        /// <summary>True if this drop is a gold coin pile.</summary>
        public bool IsGold { get; init; }
    }
}
