using System;
using System.Collections.Generic;
using System.Linq;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Domain.Entities;

namespace SilkroadAIBot.Data
{
    public class SkillRepository : ISkillRepository
    {
        private readonly DataManager _dataManager;
        private readonly Dictionary<uint, SRSkill> _cache = new();
        private readonly Dictionary<string, SRSkill> _nameCache = new(StringComparer.OrdinalIgnoreCase);

        public SkillRepository(DataManager dataManager)
        {
            _dataManager = dataManager;
            LoadFromManager();
        }

        private void LoadFromManager()
        {
            // Access private _skills via reflection if necessary, but better to add a public getter to DataManager.
            // For now, let's assume we can add a public getter to DataManager or that DataManager already exposes them.
            var skills = _dataManager.GetAllSkills(); 
            foreach (var s in skills)
            {
                var domainSkill = ConvertToDomain(s);
                _cache[domainSkill.ID] = domainSkill;
                if (!string.IsNullOrEmpty(domainSkill.CodeName))
                    _nameCache[domainSkill.CodeName] = domainSkill;
            }
        }

        public SRSkill? GetByID(uint skillID)
        {
            _cache.TryGetValue(skillID, out var skill);
            return skill;
        }

        public SRSkill? GetByCodeName(string codeName)
        {
            _nameCache.TryGetValue(codeName, out var skill);
            return skill;
        }

        public IReadOnlyList<SRSkill> GetByMastery(string masteryTree)
        {
            return _cache.Values.Where(s => s.MasteryTree.Equals(masteryTree, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public int Count => _cache.Count;

        private SRSkill ConvertToDomain(SilkroadAIBot.Domain.Entities.SRSkill s)
        {
            return new SRSkill
            {
                ID = s.ID,
                CodeName = s.CodeName,
                Name = s.Name,
                MasteryTree = s.MasteryTree,
                Race = s.Race,
                SkillType = s.SkillType,
                Level = s.Level,
                CastTime = s.CastTime,
                Cooldown = s.Cooldown,
                Range = s.Range,
                IsSelfOnly = s.IsSelfOnly,
                MPUsage = s.MPUsage,
                DamageRange = s.DamageRange,
                IconPath = s.IconPath
            };
        }
    }
}

