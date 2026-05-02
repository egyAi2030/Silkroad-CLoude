using System.Collections.Immutable;
using SilkroadAIBot.Domain.Enums;

namespace SilkroadAIBot.Domain.Entities
{
    /// <summary>
    /// Immutable identity data for the bot's own character.
    /// Set once at login; changes only on level-up or name change.
    /// </summary>
    public record CharacterIdentity
    {
        /// <summary>Character display name (from 0xB007).</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Reference model ID.</summary>
        public uint ModelID { get; init; }

        /// <summary>Character race (Chinese / European).</summary>
        public CharacterRace Race { get; init; }

        /// <summary>Current character level.</summary>
        public byte Level { get; init; }

        /// <summary>True if this character uses the European mastery system.</summary>
        public bool IsEuropean { get; init; }

        /// <summary>Guild name; empty if unguilded.</summary>
        public string GuildName { get; init; } = string.Empty;

        /// <summary>Job type byte (0 = none, 1 = trader, etc.).</summary>
        public byte JobType { get; init; }

        /// <summary>Empty identity placeholder used before login.</summary>
        public static CharacterIdentity Empty { get; } = new CharacterIdentity();
    }

    /// <summary>
    /// Immutable volatile state for the bot's own character.
    /// Replaced entirely on each HP/MP tick, position update, or inventory change.
    /// Callers use <c>with</c>-expressions to produce the next state.
    /// </summary>
    public record CharacterState
    {
        /// <summary>Server-assigned unique ID (set after first EXP or spawn packet).</summary>
        public uint UniqueID { get; init; }

        /// <summary>Current world position.</summary>
        public SRCoord Position { get; init; }

        /// <summary>Current HP.</summary>
        public uint HP { get; init; }

        /// <summary>Maximum HP.</summary>
        public uint HPMax { get; init; }

        /// <summary>Current MP.</summary>
        public uint MP { get; init; }

        /// <summary>Maximum MP.</summary>
        public uint MPMax { get; init; }

        /// <summary>Total accumulated experience points.</summary>
        public long Exp { get; init; }

        /// <summary>Experience points needed for the next level.</summary>
        public long MaxExp { get; init; }

        /// <summary>Gold carried in inventory.</summary>
        public long Gold { get; init; }

        /// <summary>Available stat points.</summary>
        public int StatPoints { get; init; }

        /// <summary>Available skill points.</summary>
        public uint SkillPoints { get; init; }

        /// <summary>
        /// Snapshot of inventory items at the time of the last 0xAA7F packet.
        /// Immutable list — replace the entire reference to update.
        /// </summary>
        public ImmutableList<SRItem> Inventory { get; init; } = ImmutableList<SRItem>.Empty;

        /// <summary>
        /// Snapshot of learned skills at the time of the last 0xAA17 packet.
        /// </summary>
        public ImmutableList<LearnedSkill> Skills { get; init; } = ImmutableList<LearnedSkill>.Empty;

        /// <summary>HP as a 0–100 percentage.</summary>
        public float HPPercent => HPMax > 0 ? HP * 100f / HPMax : 0f;

        /// <summary>MP as a 0–100 percentage.</summary>
        public float MPPercent => MPMax > 0 ? MP * 100f / MPMax : 0f;

        /// <summary>EXP as a 0–100 percentage.</summary>
        public float ExpPercent => MaxExp > 0 ? (float)((double)Exp / MaxExp * 100.0) : 0f;

        /// <summary>Empty state placeholder used before world-entry.</summary>
        public static CharacterState Empty { get; } = new CharacterState();
    }
}
