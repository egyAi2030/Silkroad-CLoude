using System;

namespace SilkroadAIBot.Domain.Entities
{
    /// <summary>
    /// Immutable reference skill data loaded from PK2 / SQLite.
    /// Used for lookup and display — not for tracking player skill levels.
    /// </summary>
    public record SRSkill
    {
        public uint ID { get; init; }
        public string CodeName { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string MasteryTree { get; init; } = string.Empty;
        public string Race { get; init; } = string.Empty;
        public string SkillType { get; init; } = string.Empty;
        public byte Level { get; init; }
        public int CastTime { get; init; }
        public int Cooldown { get; init; }
        public int Range { get; init; }
        public bool IsSelfOnly { get; init; }
        public int MPUsage { get; init; }
        public string DamageRange { get; init; } = string.Empty;
        public string IconPath { get; init; } = string.Empty;
    }

    /// <summary>
    /// Snapshot of a skill learned by the character.
    /// Links a reference <see cref="SRSkill"/> with user-controlled automation flags.
    /// </summary>
    public record LearnedSkill
    {
        public uint SkillID { get; init; }
        public byte Level { get; init; }
        public bool IsEnabled { get; init; } = true;
        public bool UseInSequence { get; init; }
        public bool AIAutoManage { get; init; }

        public LearnedSkill() { }
        public LearnedSkill(uint id, byte lv) 
        { 
            SkillID = id; 
            Level = lv; 
        }
    }
}
