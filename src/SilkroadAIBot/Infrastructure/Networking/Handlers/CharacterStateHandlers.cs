using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Infrastructure.Networking.Handlers
{
    /// <summary>Handles 0xB007 — Character Selection Action Response (character list).</summary>
    public sealed class CharacterSelectionActionHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public CharacterSelectionActionHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseCharacterSelectionAction(packet);
    }

    /// <summary>Handles 0xB001 — Character Selection Join Response.</summary>
    public sealed class CharacterSelectionJoinHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public CharacterSelectionJoinHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseCharacterSelectionJoin(packet);
    }

    /// <summary>Handles 0xB045 — Target Selection Response.</summary>
    public sealed class TargetSelectionResponseHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public TargetSelectionResponseHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseTargetSelectionResponse(packet);
    }

    /// <summary>Handles 0x303D — Character Stats (ATK, DEF, etc.).</summary>
    public sealed class CharacterStatsHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public CharacterStatsHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseCharacterStats(packet);
    }

    /// <summary>Handles 0x3056 — Level Up / EXP Update.</summary>
    public sealed class ExpUpdateHandler : IPacketHandler
    {
        private readonly WorldStateAnalyzer _analyzer;
        public ExpUpdateHandler(WorldStateAnalyzer analyzer) => _analyzer = analyzer;
        public void Handle(SRPacket packet) => _analyzer.ParseExpUpdate(packet);
    }
}
