using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Infrastructure.Networking.Handlers
{
    /// <summary>Handles 0x3015 — Single entity spawn.</summary>
    public sealed class SpawnHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public SpawnHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseSpawn(packet);
    }

    /// <summary>Handles 0x3019 — Group/single spawn (Rexall specific).</summary>
    public sealed class GroupSpawnHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public GroupSpawnHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseSingleSpawn(packet);
    }

    /// <summary>Handles 0xAA12 — Nearby entities bulk list.</summary>
    public sealed class NearbyEntitiesHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public NearbyEntitiesHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseNearbyEntities(packet);
    }

    /// <summary>Handles 0xAA11 — Entity spawned with resolved name.</summary>
    public sealed class EntityWithNameHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public EntityWithNameHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseEntityWithName(packet);
    }

    /// <summary>Handles 0xAA14 — Entity position update.</summary>
    public sealed class EntityPositionHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public EntityPositionHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseEntityPosition(packet);
    }

    /// <summary>Handles 0x3016 — Entity despawn.</summary>
    public sealed class DespawnHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public DespawnHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseDespawn(packet);
    }

    /// <summary>Handles 0x300C — Entity died/removed (alternative despawn).</summary>
    public sealed class EntityDespawnHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public EntityDespawnHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseEntityDespawn(packet);
    }

    /// <summary>Handles 0x30C9 — Kill confirmed. Triggers loot bundle logic.</summary>
    public sealed class KillConfirmedHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public KillConfirmedHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseKillConfirmed(packet);
    }
}
