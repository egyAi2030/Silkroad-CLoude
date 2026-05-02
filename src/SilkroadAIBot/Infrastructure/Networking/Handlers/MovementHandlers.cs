using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Infrastructure.Networking.Handlers
{
    /// <summary>Handles 0xB023 — Real-time character location update (vSRO 1.188+).</summary>
    public sealed class LocationUpdateHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public LocationUpdateHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseLocationUpdate(packet);
    }

    /// <summary>Handles 0xB021 — Entity movement update (small/large region variants).</summary>
    public sealed class EntityMovementHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public EntityMovementHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseEntityMovement(packet);
    }

    /// <summary>Handles 0x30D2 — Character movement (legacy vSRO movement packet).</summary>
    public sealed class MovementHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public MovementHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseMovement(packet);
    }
}
