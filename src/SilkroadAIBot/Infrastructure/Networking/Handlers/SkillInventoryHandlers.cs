using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Infrastructure.Networking.Handlers
{
    /// <summary>Handles 0xAA17 — Skill list (known skills for character).</summary>
    public sealed class SkillListHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public SkillListHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseKnownSkills(packet);
    }

    /// <summary>Handles 0xAA7F — Full inventory contents.</summary>
    public sealed class InventoryHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public InventoryHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseInventory(packet);
    }

    /// <summary>Handles 0xAA78 — Hotbar data.</summary>
    public sealed class HotbarHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public HotbarHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseHotbar(packet);
    }

    /// <summary>Handles 0xB04C — Item use response.</summary>
    public sealed class ItemUseResponseHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public ItemUseResponseHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseItemUseResponse(packet);
    }
}
