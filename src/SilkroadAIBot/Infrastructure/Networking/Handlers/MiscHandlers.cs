using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Domain.Network;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.Infrastructure.Networking.Handlers
{
    /// <summary>Handles 0xB070 — Skill used (cast broadcast).</summary>
    public sealed class SkillUsedHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public SkillUsedHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseSkillUsed(packet);
    }

    /// <summary>Handles 0xB071 — Skill hit result (damage values).</summary>
    public sealed class SkillHitHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public SkillHitHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseSkillHit(packet);
    }

    /// <summary>Handles 0x3020 — Character UniqueID notification.</summary>
    public sealed class CharacterIdHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public CharacterIdHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseCharacterId(packet);
    }

    /// <summary>Handles 0x305C — XP gain (also captures player UID).</summary>
    public sealed class XpGainHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public XpGainHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseXpGain(packet);
    }

    /// <summary>Handles 0x3101 — Party/Guild member list.</summary>
    public sealed class PartyGuildHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public PartyGuildHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParsePartyGuild(packet);
    }

    /// <summary>Handles 0x3305 — Guild info.</summary>
    public sealed class GuildInfoHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public GuildInfoHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseGuildInfo(packet);
    }

    /// <summary>Handles 0x30D0 — Monster aggro update.</summary>
    public sealed class MonsterAggroHandler : IPacketHandler
    {
        private readonly PacketParser _parser;
        public MonsterAggroHandler(PacketParser parser) => _parser = parser;
        public void Handle(SRPacket packet) => _parser.ParseMonsterAggro(packet);
    }

    /// <summary>Handles 0x2001 — Identity exchange (GatewayServer, AgentServer).</summary>
    public sealed class IdentityHandler : IPacketHandler
    {
        public void Handle(SRPacket packet) { /* Silent */ }
    }

    /// <summary>
    /// Handles 0x2005, 0x6005, 0xAA01 — Security Guard Challenges.
    /// 'Instant Flush' implementation: Echoes the challenge back to the server 
    /// to maintain connection stability during proxy handovers.
    /// </summary>
    public sealed class GuardChallengeHandler : IPacketHandler
    {
        private readonly IPacketSender _sender;
        public GuardChallengeHandler(IPacketSender sender) => _sender = sender;
        
        public void Handle(SRPacket packet)
        {
            BotLogger.Info("Security", $"[Guard] Echoing Challenge 0x{packet.Opcode:X4} (Instant Flush)");
            _sender.SendPacket(packet);
        }
    }
}
