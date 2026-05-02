using System.Collections.Immutable;
using SilkroadAIBot.Domain.Enums;

namespace SilkroadAIBot.Domain.Entities
{
    /// <summary>
    /// Immutable snapshot of the player's own character state.
    /// Inherits SRBionic so it is trackable alongside other entities.
    /// Use <c>with</c>-expressions to produce updated copies.
    /// </summary>
    public record SRCharacter : SRBionic
    {
        /// <summary>Current character level.</summary>
        public byte Level { get; init; }

        /// <summary>Accumulated experience points.</summary>
        public long Experience { get; init; }

        /// <summary>Experience points needed for the next level.</summary>
        public long MaxExperience { get; init; }

        /// <summary>Gold carried in inventory.</summary>
        public long Gold { get; init; }

        /// <summary>Available skill points.</summary>
        public uint SkillPoints { get; init; }

        /// <summary>Available stat points.</summary>
        public int StatPoints { get; init; }

        /// <summary>Character race (Chinese / European).</summary>
        public CharacterRace Race { get; init; }

        /// <summary>Guild name, empty if unguilded.</summary>
        public string GuildName { get; init; } = string.Empty;

        /// <summary>Snapshot of learned skills.</summary>
        public ImmutableList<LearnedSkill> Skills { get; init; } = ImmutableList<LearnedSkill>.Empty;

        /// <summary>Character masteries (skill trees).</summary>
        public ImmutableList<Mastery> Masteries { get; init; } = ImmutableList<Mastery>.Empty;

        /// <summary>Alias for Skills to support legacy code.</summary>
        public ImmutableList<LearnedSkill> LearnedSkills => Skills;

        /// <summary>Alias for Experience to support legacy code.</summary>
        public long Exp => Experience;

        /// <summary>Snapshot of inventory items.</summary>
        public ImmutableList<SRItem> Inventory { get; init; } = ImmutableList<SRItem>.Empty;

        /// <summary>True if this character is of European race.</summary>
        public bool IsEuropean => Race == CharacterRace.European;

        /// <summary>HP as a 0–100 percentage.</summary>
        public float HPPercent => HPMax > 0 ? HP * 100f / HPMax : 0f;

        /// <summary>MP as a 0–100 percentage.</summary>
        public float MPPercent => MPMax > 0 ? MP * 100f / MPMax : 0f;

        /// <summary>EXP as a 0–100 percentage.</summary>
        public float ExpPercent => MaxExperience > 0 ? (float)((double)Experience / MaxExperience * 100.0) : 0f;
    }
    
    public record Mastery
    {
        public uint ID { get; init; }
        public string Name { get; init; } = string.Empty;
        public byte Level { get; init; }
        public uint Experience { get; init; }
    }
}

