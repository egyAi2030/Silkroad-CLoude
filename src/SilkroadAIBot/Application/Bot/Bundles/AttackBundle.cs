using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Domain.Enums;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Application.Bot;

namespace SilkroadAIBot.Application.Bot.Bundles
{
    /// <summary>
    /// Hybrid Combat Engine: 
    /// - Auto-farms standard mobs using highest-damage skills.
    /// - Hands over control to the AI Agent for High-Value Targets (Players, Uniques).
    /// </summary>
    public class AttackBundle : IBotBundle
    {
        public string Name => "AttackBundle";
        public int Priority => 50; // Higher than Loot, lower than Recovery

        private readonly IWorldStateRepository _worldState;
        private readonly ISkillRepository _skills;
        private readonly IBotController _controller;
        
        private uint? _activeTargetUID;
        private TargetSource _source = TargetSource.Auto;
        private DateTime _lastActionTime = DateTime.MinValue;

        private enum TargetSource { Auto, Strategic }

        public AttackBundle(IWorldStateRepository worldState, ISkillRepository skills, IBotController controller)
        {
            _worldState = worldState;
            _skills = skills;
            _controller = controller;
        }

        public Task StartAsync(CancellationToken ct)
        {
            BotLogger.Info(Name, "Combat Engine initialized.");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _activeTargetUID = null;
            return Task.CompletedTask;
        }

        public Task TickAsync(CancellationToken ct)
        {
            var character = _worldState.GetCharacter();
            if (character.HP <= 0) return Task.CompletedTask;

            // 1. Validate Current Target
            if (!IsTargetValid(_activeTargetUID))
            {
                _activeTargetUID = null;
                _source = TargetSource.Auto;
            }

            // 2. Acquisition (If Idle or Auto-farming)
            if (_activeTargetUID == null)
            {
                var nearestMob = FindNearestMob(character);
                if (nearestMob != null)
                {
                    _activeTargetUID = nearestMob.UniqueID;
                    _source = TargetSource.Auto;
                    _controller.Enqueue(new SelectTargetCommand(_activeTargetUID.Value));
                    BotLogger.Debug(Name, $"Auto-locked: {nearestMob.Name} (UID: {_activeTargetUID})");
                }
            }

            // 3. Combat Execution
            if (_activeTargetUID.HasValue && _source == TargetSource.Auto)
            {
                ExecuteAutoCombat(character, _activeTargetUID.Value);
            }

            return Task.CompletedTask;
        }

        private bool IsTargetValid(uint? uid)
        {
            if (!uid.HasValue) return false;
            var entity = _worldState.GetEntity(uid.Value);
            return entity != null && entity.LifeState == LifeState.Alive;
        }

        private SREntity FindNearestMob(SRCharacter character)
        {
            var area = _worldState.GetTrainingArea();
            
            return _worldState.GetAllEntities()
                .Where(e => e.EntityType == EntityType.Monster && e.LifeState == LifeState.Alive)
                .Where(e => area.IsInRange(e.Position))
                .OrderBy(e => character.Position.DistanceTo(e.Position))
                .FirstOrDefault();
        }

        private void ExecuteAutoCombat(SRCharacter character, uint targetUID)
        {
            // For mobs, we pick the highest level attack skill we can afford
            var bestSkill = character.Skills
                .Select(ls => _skills.GetByID(ls.SkillID))
                .Where(s => s != null && s.MPUsage <= character.MP)
                .OrderByDescending(s => s.Level) 
                .FirstOrDefault();

            if (bestSkill != null)
            {
                _controller.Enqueue(new CastSkillCommand(bestSkill.ID, targetUID));
            }
            else
            {
                _controller.Enqueue(new BasicAttackCommand(targetUID));
            }
        }

        /// <summary>
        /// External hook for the AI Agent to take control of the combat engine.
        /// </summary>
        public void SetStrategicTarget(uint uid)
        {
            _activeTargetUID = uid;
            _source = TargetSource.Strategic;
            BotLogger.Info(Name, $"[AI OVERRIDE] Strategic Lock: {uid}");
        }
    }
}
