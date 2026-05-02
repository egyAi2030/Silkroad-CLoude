namespace SilkroadAIBot.Domain.Enums
{
    /// <summary>Broad classification of a game world entity.</summary>
    public enum EntityType : byte
    {
        None    = 0,
        Player  = 1,
        Monster = 2,
        Npc     = 3,
        Item    = 4,
        Portal  = 5,
    }

    /// <summary>Alive or dead state of a bionic entity.</summary>
    public enum LifeState : byte
    {
        Alive = 0,
        Dead  = 1,
    }

    /// <summary>Current movement state of a bionic entity.</summary>
    public enum MotionState : byte
    {
        Idle    = 0,
        Walking = 2,
    }

    /// <summary>Monster rarity / difficulty classification.</summary>
    public enum MobRarity : byte
    {
        Normal   = 0,
        Champion = 1,
        Giant    = 4,
        Titan    = 5,
        Elite    = 6,
        Unique   = 7,
    }

    /// <summary>Character race — determines mastery and skill pool.</summary>
    public enum CharacterRace : byte
    {
        Chinese  = 0,
        European = 1,
    }

    /// <summary>Status of a quest in the log.</summary>
    public enum QuestStatus : byte
    {
        None      = 0,
        Active    = 1,
        Completed = 2,
    }
}

