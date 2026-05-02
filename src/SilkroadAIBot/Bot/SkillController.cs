using System;
using System.Collections.Generic;
using System.Text;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Application.Interfaces;

namespace SilkroadAIBot.Bot
{
    public class SkillController
    {
        private readonly SilkroadAIBot.Application.Interfaces.IWorldStateRepository _worldState;
        private readonly Data.DatabaseManager _db;
        private readonly SilkroadAIBot.Application.Interfaces.IPacketSender _packetSender;
        private readonly Dictionary<uint, DateTime> _cooldowns = new Dictionary<uint, DateTime>();

        public SkillController(SilkroadAIBot.Application.Interfaces.IWorldStateRepository worldState, Data.DatabaseManager db, SilkroadAIBot.Application.Interfaces.IPacketSender packetSender)
        {
            _worldState = worldState;
            _db = db;
            _packetSender = packetSender;
        }

        public bool SelectTarget(uint targetId)
        {
            if (targetId == 0) return false;
            _packetSender.SendSelectTarget(targetId);
            return true;
        }

        public bool BasicAttack(uint targetId)
        {
            if (targetId == 0) return false;
            _packetSender.SendBasicAttack(targetId);
            return true;
        }

        public bool UseItem(byte slot)
        {
            _packetSender.SendUseItem(slot);
            return true;
        }

        public bool CastSkill(SRSkill skill, uint targetId, SRCoord? pos = null)
        {
            if (skill == null) return false;
            if (IsOnCooldown(skill.ID)) return false;

            _packetSender.SendCastSkill(skill.ID, targetId, pos != null ? new SilkroadAIBot.Domain.Entities.SRCoord(pos.Region, pos.X, pos.Y, pos.Z) : null);
            
            // Set cooldown
            _cooldowns[skill.ID] = DateTime.Now.AddMilliseconds(skill.Cooldown);
            return true;
        }

        public bool IsSkillReady(uint skillId)
        {
            return !IsOnCooldown(skillId);
        }

        public bool IsOnCooldown(uint skillId)
        {
            if (_cooldowns.TryGetValue(skillId, out DateTime endTime))
            {
                return DateTime.Now < endTime;
            }
            return false;
        }
    }
}


