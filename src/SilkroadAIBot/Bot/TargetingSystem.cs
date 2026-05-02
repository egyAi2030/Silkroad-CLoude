using System;
using System.Linq;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Domain.Entities;

namespace SilkroadAIBot.Bot
{
    public class TargetingSystem
    {
        private readonly WorldState _worldState;
        private uint _currentTargetId;
        
        public TargetPriority Priority { get; set; } = TargetPriority.Closest;

        public TargetingSystem(WorldState worldState)
        {
            _worldState = worldState;
        }

        public uint? GetNextTarget()
        {
            var charPos = _worldState.Character?.Position;
            if (charPos == null) return null;

            // Filter for Mobs
            var targets = _worldState.GetEntities<SRMob>()
                .Where(m => _worldState.TrainingArea.IsInRange(m.Position)) 
                .Select(m => new 
                { 
                    Mob = m, 
                    Distance = charPos.DistanceTo(m.Position) 
                })
                .OrderBy(x => x.Distance)
                .ToList();

            if (targets.Count > 0)
            {
                _currentTargetId = targets[0].Mob.UniqueID;
                return _currentTargetId;
            }

            return null;
        }

        public enum TargetPriority
        {
            Closest,
            LowestHP,
            HighestHP
        }
    }
}


