using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Domain.Enums;
using SilkroadAIBot.Domain.Entities;
using SREntity = SilkroadAIBot.Domain.Entities.SREntity;
using SRCoord = SilkroadAIBot.Domain.Entities.SRCoord;

namespace SilkroadAIBot.Bot
{
    public class WorldState : IWorldStateRepository, IEntityRepository
    {
        // v2.1.0: Spatial Partitioning & Pooling
        private SilkroadAIBot.Core.Helpers.Quadtree<SREntity> _spatialGrid = new Core.Helpers.Quadtree<SREntity>(0, new Core.Helpers.Quadtree<SREntity>.Rect(-10000, -10000, 20000, 20000));

        private ConcurrentDictionary<uint, SREntity> _entities = new ConcurrentDictionary<uint, SREntity>();
        
        public ConcurrentDictionary<uint, SREntity> Entities => _entities;
        
        // v8.0 interface alignment
        public IReadOnlyList<SREntity> NearbyEntities => _entities.Values.ToList();
        public IReadOnlyList<string> ActionLogs => _actionLogs.AsReadOnly();
        private List<string> _actionLogs = new List<string>();

        public event Action OnNearbyEntitiesUpdated;
        public void TriggerNearbyEntitiesUpdated() => OnNearbyEntitiesUpdated?.Invoke();

        public event Action OnSkillsUpdated;
        public void TriggerSkillsUpdated() => OnSkillsUpdated?.Invoke();

        public uint CurrentTargetID { get; set; } = 0;
        public List<SRCoord> ManualPath = new List<SRCoord>();

        public void SetManualPath(IEnumerable<SRCoord> path)
        {
            ManualPath = path.ToList();
        }

        IReadOnlyList<SRCoord> IWorldStateRepository.GetManualPath() => ManualPath.AsReadOnly();

        public void DespawnEntity(uint uid)
        {
            RemoveEntity(uid);
            TriggerNearbyEntitiesUpdated();
        }

        #region IWorldStateRepository Implementation
        public CharacterIdentity GetCharacterIdentity()
        {
            return new CharacterIdentity {
                Name = CharacterName,
                ModelID = Character.ModelID,
                Race = !Character.IsEuropean ? Domain.Enums.CharacterRace.Chinese : Domain.Enums.CharacterRace.European,
                Level = (byte)Character.Level,
                IsEuropean = Character.IsEuropean
            };
        }

        public SRCharacter GetCharacter()
        {
            return Character;
        }

        public IReadOnlyList<SREntity> GetAllEntities()
        {
            return _entities.Values.ToList();
        }

        public IReadOnlyList<T> GetEntities<T>() where T : SREntity
        {
            return _entities.Values.OfType<T>().ToList();
        }

        public CharacterState GetCharacterState()
        {
            return new CharacterState
            {
                UniqueID = CharacterUniqueID,
                HP = (uint)Character.HP,
                HPMax = (uint)Character.HPMax,
                MP = (uint)Character.MP,
                MPMax = (uint)Character.MPMax,
                Position = (SilkroadAIBot.Domain.Entities.SRCoord)Character.Position,
                Skills = System.Collections.Immutable.ImmutableList.CreateRange(
                    Character.LearnedSkills.Select(s => new LearnedSkill { 
                        SkillID = s.SkillID,
                        IsEnabled = true
                    })
                )
            };
        }

        IReadOnlyList<SilkroadAIBot.Domain.Entities.SREntity> IWorldStateRepository.GetNearbyEntities(float range)
        {
            var charPos = Character.Position;
            return _entities.Values
                .Where(e => charPos.DistanceTo(e.Position) <= range)
                .Select(e => ConvertToDomainEntity(e))
                .ToList();
        }

        SilkroadAIBot.Domain.Entities.SREntity IWorldStateRepository.GetEntity(uint uid)
        {
            return _entities.TryGetValue(uid, out var e) ? e : null;
        }


        SilkroadAIBot.Domain.Entities.TrainingArea IWorldStateRepository.TrainingArea
        {
            get => new SilkroadAIBot.Domain.Entities.TrainingArea {
                Center    = (SilkroadAIBot.Domain.Entities.SRCoord)TrainingArea.Center,
                Radius    = TrainingArea.Radius,
                IsEnabled = TrainingArea.IsEnabled
            };
            set => SetTrainingArea(value);
        }

        public uint GetCurrentTargetID() => CurrentTargetID;

        private SilkroadAIBot.Domain.Entities.SREntity ConvertToDomainEntity(SREntity e)
        {
            return new SilkroadAIBot.Domain.Entities.SRMob { 
                UniqueID = e.UniqueID, 
                ModelID = e.ModelID, 
                Name = e.Name, 
                Position = (SilkroadAIBot.Domain.Entities.SRCoord)e.Position,
                EntityType = (SilkroadAIBot.Domain.Enums.EntityType)e.EntityType,
                LifeState = (SilkroadAIBot.Domain.Enums.LifeState)e.LifeStateType
            };
        }
        #endregion

        public void LogAction(string message)
        {
            _actionLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (_actionLogs.Count > 500) _actionLogs.RemoveAt(0);
        }

        // ────────── Character State ──────────
        public uint CharacterUniqueID { get; set; } = 0;
        public string CharacterName { get; set; } = "";
        public SRCharacter Character { get; set; } = new SRCharacter { 
            UniqueID = 0, 
            ModelID = 0, 
            Position = new SRCoord(0,0,0,0) 
        };
        public long SessionXP { get; set; } = 0;
        public int SessionKills { get; set; } = 0;

        public void UpdateCharacter(Func<SRCharacter, SRCharacter> updateFn)
        {
            if (Character != null) Character = updateFn(Character);
        }

        public TrainingArea TrainingArea { get; set; } = new TrainingArea {
            Center = new SRCoord(0,0,0,0),
            Radius = 50,
            IsEnabled = false
        };

        public TrainingArea GetTrainingArea() => TrainingArea;
        public CharacterRace CharacterRace => Character.Race;
        
        // v2.1.2: Thread-safe Selection Tracking
        public string LastSelectionHash { get; set; }
        public CharacterIdentity DomainIdentity { get; set; } = CharacterIdentity.Empty;

        public event Action? OnCharacterUpdated;
        public void TriggerCharacterUpdate() => OnCharacterUpdated?.Invoke();

        public event Action OnInventoryUpdated;
        public void TriggerInventoryUpdated() => OnInventoryUpdated?.Invoke();

        public void SetCharacterName(string name)
        {
            CharacterName = name;
            Character = Character with { Name = name };
            TriggerCharacterUpdate();
        }

        public SREntity GetEntity(uint uniqueID)
        {
            _entities.TryGetValue(uniqueID, out var entity);
            return entity;
        }

        public void AddEntity(SREntity entity)
        {
            if (entity == null) return;
            _entities[entity.UniqueID] = entity;
            _spatialGrid.Insert(entity);
        }

        public void RemoveEntity(uint uniqueID)
        {
            if (_entities.TryRemove(uniqueID, out var entity))
            {
                // Quadtree removal logic
            }
        }

        public IEnumerable<SREntity> GetNearbyEntities(float radius)
        {
            return _entities.Values.Where(e => Character.Position.DistanceTo(e.Position) <= radius);
        }

        public SREntity GetNearestEntity(float maxDistance = 1000)
        {
            return _entities.Values
                .Where(e => e.UniqueID != CharacterUniqueID)
                .OrderBy(e => Character.Position.DistanceTo(e.Position))
                .FirstOrDefault();
        }

        public void UpdateCharacterPosition(SRCoord pos)
        {
            Character = Character with { Position = pos };
            TriggerCharacterUpdate();
        }

        public void SpawnEntity(SREntity entity)
        {
            if (entity == null) return;
            _entities[entity.UniqueID] = entity;
            _spatialGrid.Insert(entity);
        }

        public void Spawn(SREntity entity) => SpawnEntity(entity);
        public void Despawn(uint uid) => DespawnEntity(uid);
        public SREntity? Get(uint uid) => GetEntity(uid);

        public bool Update<T>(uint uid, Func<T, T> updater) where T : SREntity
        {
            if (_entities.TryGetValue(uid, out var entity) && entity is T typedEntity)
            {
                var updated = updater(typedEntity);
                _entities[uid] = updated;
                _spatialGrid.Insert(updated); // Re-insert to update spatial grid
                return true;
            }
            return false;
        }


        public void UpdateEntityLifeState(uint uniqueID, LifeState state)
        {
            var entity = GetEntity(uniqueID);
            if (entity != null)
            {
                _entities[uniqueID] = entity with { LifeState = state };
            }
        }


        public void Clear()
        {
            _entities.Clear();
        }

        public void SetCharacterUniqueID(uint uid) => CharacterUniqueID = uid;
        public void SetTrainingArea(SilkroadAIBot.Domain.Entities.TrainingArea area) 
        {
            TrainingArea = new TrainingArea {
                Center = new SRCoord(area.Center.Region, area.Center.X, area.Center.Y, area.Center.Z),
                Radius = area.Radius,
                IsEnabled = area.IsEnabled
            };
        }
        public void SetSelectionHash(string hash) => LastSelectionHash = hash;

        #region Domain Event System (Module 5)
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            var type = typeof(TEvent);
            if (!_subscribers.ContainsKey(type)) _subscribers[type] = new List<Delegate>();
            _subscribers[type].Add(handler);
        }

        public void PublishDomainEvent<TEvent>(TEvent ev)
        {
            var type = typeof(TEvent);
            if (_subscribers.TryGetValue(type, out var handlers))
            {
                foreach (var handler in handlers.Cast<Action<TEvent>>())
                {
                    handler(ev);
                }
            }
        }

        // Bridge methods for legacy code to trigger new events
        public void UpdateCharacterState(Func<SilkroadAIBot.Domain.Entities.CharacterState, SilkroadAIBot.Domain.Entities.CharacterState> updateFunc)
        {
            PublishDomainEvent(new SilkroadAIBot.Domain.Events.CharacterHpChangedEvent(
                CharacterUniqueID, (uint)Character.HP, (uint)Character.HPMax, (uint)Character.MP, (uint)Character.MPMax));
        }

        public void TriggerKillConfirmed(uint victimUID)
        {
            PublishDomainEvent(new SilkroadAIBot.Domain.Events.KillConfirmedEvent(CharacterUniqueID, victimUID));
        }
        #endregion

        // ────────── Character Identity Setup ──────────
        public void SetCharacterIdentity(CharacterIdentity identity)
        {
            DomainIdentity = identity;
            CharacterName = identity.Name;
            Character = Character with { 
                Name = identity.Name,
                ModelID = identity.ModelID,
                Level = identity.Level
            };
            TriggerCharacterUpdate();
        }

        public bool IsNavigatingManualPath { get; set; } = false;

        public List<Quest> ActiveQuests { get; } = new();
        public List<Quest> AvailableQuests { get; } = new();
    }
}
