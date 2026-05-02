using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Domain.Enums;

namespace SilkroadAIBot.Application.Interfaces
{
    /// <summary>
    /// Read-only snapshot of the current world state.
    /// Used by MCP tools and UI layers — never by packet parsers.
    /// Implementations must be thread-safe.
    /// </summary>
    public interface IWorldStateRepository
    {
        /// <summary>Fires when character stats or position change.</summary>
        event System.Action OnCharacterUpdated;
        
        /// <summary>Fires when the skill list is updated.</summary>
        event System.Action OnSkillsUpdated;

        /// <summary>Returns the current character's unique ID.</summary>
        uint CharacterUniqueID { get; }

        /// <summary>Returns the current character's name.</summary>
        string CharacterName { get; }

        /// <summary>Returns the character's current immutable identity and state.</summary>
        SRCharacter Character { get; }

        /// <summary>Legacy method for UI compatibility.</summary>
        SRCharacter GetCharacter();

        /// <summary>Legacy method for UI compatibility.</summary>
        IReadOnlyList<SREntity> GetAllEntities();

        /// <summary>Legacy method for UI compatibility.</summary>
        IReadOnlyList<T> GetEntities<T>() where T : SREntity;

        /// <summary>Returns the bot's current session XP gain.</summary>
        long SessionXP { get; }

        /// <summary>Returns the bot's current session kill count.</summary>
        int SessionKills { get; }

        /// <summary>Returns all currently tracked entities within default range.</summary>
        IReadOnlyList<SREntity> NearbyEntities { get; }

        /// <summary>Returns the live entity dictionary. Use for UI binding only.</summary>
        System.Collections.Concurrent.ConcurrentDictionary<uint, SREntity> Entities { get; }

        /// <summary>Returns or sets the current target's unique ID, or 0 if none.</summary>
        uint CurrentTargetID { get; set; }

        /// <summary>Returns the action log history.</summary>
        IReadOnlyList<string> ActionLogs { get; }

        /// <summary>Returns the entity with the given UID, or null if not found.</summary>
        SREntity GetEntity(uint uid);

        /// <summary>Returns the hash of the last selected entity for packet verification.</summary>
        string LastSelectionHash { get; }

        /// <summary>Returns or sets the current training area configuration.</summary>
        TrainingArea TrainingArea { get; set; }

        /// <summary>Legacy method for UI compatibility.</summary>
        TrainingArea GetTrainingArea();

        /// <summary>Legacy property for UI compatibility.</summary>
        CharacterRace CharacterRace { get; }

        /// <summary>Returns all entities within <paramref name="range"/> units of the character's current position.</summary>
        IReadOnlyList<SREntity> GetNearbyEntities(float range);

        /// <summary>Subscribes to world state change events.</summary>
        void Subscribe<TEvent>(System.Action<TEvent> handler);

        /// <summary>Returns the current manually set path for navigation.</summary>
        IReadOnlyList<SRCoord> GetManualPath();

        /// <summary>Explicitly triggers a character update event.</summary>
        void TriggerCharacterUpdate();
    }

    /// <summary>
    /// Read/write access to the live entity dictionary.
    /// Used by packet handlers to spawn, despawn, and update entities.
    /// </summary>
    public interface IEntityRepository
    {
        /// <summary>
        /// Adds or replaces an entity in the world state.
        /// Fires <see cref="Domain.Events.EntitySpawnedEvent"/> if the entity is new.
        /// </summary>
        /// <param name="entity">The fully constructed immutable entity record.</param>
        void Spawn(SREntity entity);

        /// <summary>
        /// Removes the entity with the given <paramref name="uid"/> from the world state.
        /// Fires <see cref="Domain.Events.EntityDespawnedEvent"/>.
        /// </summary>
        /// <param name="uid">The entity's unique runtime ID.</param>
        void Despawn(uint uid);

        /// <summary>
        /// Returns the entity with the given <paramref name="uid"/>, or <c>null</c> if not found.
        /// </summary>
        SREntity? Get(uint uid);

        /// <summary>
        /// Atomically applies <paramref name="updater"/> to the entity at <paramref name="uid"/>
        /// and stores the resulting immutable copy.
        /// </summary>
        /// <typeparam name="T">Concrete entity type.</typeparam>
        /// <param name="uid">Target entity UID.</param>
        /// <param name="updater">Pure function: old state → new state using <c>with</c> expression.</param>
        /// <returns><c>true</c> if found and updated; <c>false</c> if not found or wrong type.</returns>
        bool Update<T>(uint uid, System.Func<T, T> updater) where T : SREntity;

        /// <summary>Removes all entities from the world state (used on zone change).</summary>
        void Clear();

        /// <summary>Sets the character's unique ID for identity tracking.</summary>
        void SetCharacterUniqueID(uint uid);

        /// <summary>Updates the active training area configuration.</summary>
        void SetTrainingArea(TrainingArea area);

        /// <summary>Sets the last received security hash for target selection.</summary>
        void SetSelectionHash(string hash);

        /// <summary>Sets a manual path for navigation.</summary>
        void SetManualPath(System.Collections.Generic.IEnumerable<SRCoord> path);
    }

    /// <summary>
    /// Read-only skill data access backed by the SQLite RefSkill table.
    /// Implementations must cache all skills in memory on startup (O(1) lookups).
    /// </summary>
    public interface ISkillRepository
    {
        /// <summary>Returns skill data for the given <paramref name="skillID"/>, or <c>null</c> if unknown.</summary>
        SRSkill? GetByID(uint skillID);

        /// <summary>Returns skill data for the given <paramref name="codeName"/>, or <c>null</c> if unknown.</summary>
        SRSkill? GetByCodeName(string codeName);

        /// <summary>Returns all skills whose mastery tree matches <paramref name="masteryTree"/>.</summary>
        IReadOnlyList<SRSkill> GetByMastery(string masteryTree);

        /// <summary>Returns the total number of skills loaded into the cache.</summary>
        int Count { get; }
    }

    /// <summary>
    /// Read-only item data access backed by the SQLite RefObjCommon table.
    /// Implementations must cache all items in memory on startup (O(1) lookups).
    /// </summary>
    public interface IItemRepository
    {
        /// <summary>Returns item model info for the given <paramref name="modelID"/>, or <c>null</c> if unknown.</summary>
        SRModelInfo? GetByID(uint modelID);

        /// <summary>Returns item model info by internal <paramref name="codeName"/>, or <c>null</c> if unknown.</summary>
        SRModelInfo? GetByCodeName(string codeName);

        /// <summary>
        /// Returns all items whose TypeID2 and TypeID3 match the given values.
        /// Used to enumerate all consumables of a specific potion type.
        /// </summary>
        IReadOnlyList<SRModelInfo> GetByType(byte typeID2, byte typeID3);

        /// <summary>Returns the total number of items loaded into the cache.</summary>
        int Count { get; }
    }
}
