using System.Collections.Generic;
using System.Linq;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Bot;

namespace SilkroadAIBot.Infrastructure.Persistence
{
    /// <summary>
    /// Implementation of IWorldStateRepository for read-only layers (MCP/UI).
    /// Provides thread-safe snapshots of the internal WorldState.
    /// </summary>
    internal class WorldStateRepository : IWorldStateRepository
    {
        private readonly SilkroadAIBot.Bot.WorldState _state;
        private readonly IActionLogger _logger;

        public WorldStateRepository(SilkroadAIBot.Bot.WorldState state, IActionLogger logger)
        {
            _state = state;
            _logger = logger;
        }

        public event System.Action? OnCharacterUpdated
        {
            add => _state.OnCharacterUpdated += value;
            remove => _state.OnCharacterUpdated -= value;
        }

        public event System.Action? OnSkillsUpdated
        {
            add => _state.OnSkillsUpdated += value;
            remove => _state.OnSkillsUpdated -= value;
        }

        public uint CharacterUniqueID => _state.CharacterUniqueID;
        public string CharacterName => _state.CharacterName;
        public SRCharacter Character => GetCharacter();
        public long SessionXP => _state.SessionXP;
        public int SessionKills => _state.SessionKills;
        public uint CurrentTargetID 
        { 
            get => _state.CurrentTargetID;
            set => _state.CurrentTargetID = value;
        }

        public SRCharacter GetCharacter()
        {
            if (_state.Entities.TryGetValue(_state.CharacterUniqueID, out var entity) && entity is SRCharacter character)
            {
                return character;
            }

            // Fallback for character not yet spawned
            return new SRCharacter
            {
                UniqueID = _state.CharacterUniqueID,
                ModelID = 0,
                Position = new SRCoord { Region = 0, X = 0, Y = 0, Z = 0 },
                Name = "Unknown"
            };
        }

        public IReadOnlyList<SREntity> GetAllEntities()
        {
            return _state.Entities.Values.ToList();
        }

        public IReadOnlyList<SREntity> GetNearbyEntities(float range)
        {
            var charPos = GetCharacter().Position;
            return _state.Entities.Values
                .Where(e => charPos.DistanceTo(e.Position) <= range)
                .ToList();
        }

        public IReadOnlyList<T> GetEntities<T>() where T : SREntity
        {
            return _state.Entities.Values.OfType<T>().ToList();
        }


        public TrainingArea TrainingArea
        {
            get => _state.CharacterUniqueID == 0 ? new TrainingArea() : _state.TrainingArea;
            set => _state.TrainingArea = value;
        }

        public TrainingArea GetTrainingArea() => TrainingArea;
        public SilkroadAIBot.Domain.Enums.CharacterRace CharacterRace => GetCharacter().Race;

        public SREntity GetEntity(uint uid)
        {
            return _state.Entities.TryGetValue(uid, out var entity) ? entity : null;
        }

        public uint GetCurrentTargetID()
        {
            // TODO: Move targeting logic to its own service or property in WorldState
            return 0; 
        }

        public string LastSelectionHash => _state.LastSelectionHash;
        public IReadOnlyList<SREntity> NearbyEntities => _state.Entities.Values.ToList();
        public System.Collections.Concurrent.ConcurrentDictionary<uint, SREntity> Entities => _state.Entities;
        public IReadOnlyList<string> ActionLogs => _logger.GetRecentLogs();

        public void Subscribe<TEvent>(System.Action<TEvent> handler)
        {
            _state.Subscribe(handler);
        }

        public IReadOnlyList<SRCoord> GetManualPath() => _state.ManualPath;

        public void TriggerCharacterUpdate() => _state.TriggerCharacterUpdate();
    }
}
