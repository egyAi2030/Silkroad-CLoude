using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Infrastructure.Networking.Handlers
{
    /// <summary>Handles 0x3057 and 0x3054 — HP/MP update packets.</summary>
    public sealed class HpMpUpdateHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public HpMpUpdateHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseHpMpUpdate(packet);
    }

    /// <summary>Handles 0xAA0F — Max HP/MP update (NOVA/Rexall specific).</summary>
    public sealed class MaxHpMpHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public MaxHpMpHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseMaxHpMp(packet);
    }

    /// <summary>Handles 0x30BF — Entity Die event.</summary>
    public sealed class EntityDieHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public EntityDieHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseEntityDie(packet);
    }
}
