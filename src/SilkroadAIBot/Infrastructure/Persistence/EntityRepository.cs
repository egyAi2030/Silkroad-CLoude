using System;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Domain.Events;

namespace SilkroadAIBot.Infrastructure.Persistence
{
    /// <summary>
    /// Implementation of IEntityRepository for packet parsers.
    /// Provides write access to the central WorldState.
    /// </summary>
    internal class EntityRepository : IEntityRepository
    {
        private readonly SilkroadAIBot.Bot.WorldState _state;

        public EntityRepository(SilkroadAIBot.Bot.WorldState state)
        {
            _state = state;
        }

        public void Spawn(SREntity entity)
        {
            if (entity == null) return;

            bool isNew = !_state.Entities.ContainsKey(entity.UniqueID);
            _state.Entities[entity.UniqueID] = entity;

            if (isNew)
            {
                _state.PublishDomainEvent(new EntitySpawnedEvent(entity));
            }
        }

        public void Despawn(uint uid)
        {
            if (_state.Entities.TryRemove(uid, out var entity))
            {
                _state.PublishDomainEvent(new EntityDespawnedEvent(uid));
            }
        }

        public void SetCharacterUniqueID(uint uid)
        {
            _state.CharacterUniqueID = uid;
        }

        public void SetTrainingArea(TrainingArea area)
        {
            _state.TrainingArea = area;
        }

        public void SetSelectionHash(string hash)
        {
            _state.LastSelectionHash = hash;
        }

        public void SetManualPath(System.Collections.Generic.IEnumerable<SRCoord> path)
        {
            _state.ManualPath = new System.Collections.Generic.List<SRCoord>(path);
        }

        public SREntity? Get(uint uid)
        {
            return _state.Entities.TryGetValue(uid, out var entity) ? entity : null;
        }

        public bool Update<T>(uint uid, Func<T, T> updater) where T : SREntity
        {
            if (_state.Entities.TryGetValue(uid, out var entity) && entity is T typedEntity)
            {
                var updated = updater(typedEntity);
                _state.Entities[uid] = updated;

                // Fire type-specific update events if needed
                if (updated is SRCharacter character)
                {
                    _state.PublishDomainEvent(new CharacterHpChangedEvent(
                        character.UniqueID, 
                        character.HP, character.HPMax, 
                        character.MP, character.MPMax));
                    
                    _state.PublishDomainEvent(new CharacterPositionChangedEvent(character.UniqueID, character.Position));
                }

                return true;
            }
            return false;
        }

        public void Clear()
        {
            _state.Entities.Clear();
            _state.PublishDomainEvent(new WorldStateClearedEvent());
        }
    }
}
