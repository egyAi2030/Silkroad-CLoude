using System;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Data;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Infrastructure.Networking.Handlers
{
    /// <summary>
    /// Handles 0x34A5 — Character Data Begin.
    /// Clears <see cref="ICharacterDataBuffer"/> and marks buffering active.
    /// </summary>
    public sealed class CharacterDataBeginHandler : IPacketHandler
    {
        private readonly ICharacterDataBuffer _buffer;

        public CharacterDataBeginHandler(ICharacterDataBuffer buffer)
            => _buffer = buffer;

        public void Handle(SRPacket packet)
        {
            _buffer.Reset();
        }
    }

    /// <summary>
    /// Handles 0x3013 — Character Data Chunk.
    /// Appends all remaining bytes in the packet to <see cref="ICharacterDataBuffer"/>.
    /// </summary>
    public sealed class CharacterDataChunkHandler : IPacketHandler
    {
        private readonly ICharacterDataBuffer _buffer;

        public CharacterDataChunkHandler(ICharacterDataBuffer buffer)
            => _buffer = buffer;

        public void Handle(SRPacket packet)
        {
            if (!_buffer.IsBuffering) return;
            _buffer.Append(packet.Payload);
        }
    }

    /// <summary>
    /// Handles 0x34A6 — Character Data End.
    /// Finalizes the buffer, writes a hex dump for debugging,
    /// attempts to lock the character UID, then dispatches to
    /// <see cref="WorldStateAnalyzer.ParseCharacterData"/>.
    /// </summary>
    public sealed class CharacterDataEndHandler : IPacketHandler
    {
        private readonly ICharacterDataBuffer _buffer;
        private readonly WorldStateAnalyzer _analyzer;
        private readonly WorldState _worldState;
        private readonly DataManager _dataManager;

        public CharacterDataEndHandler(
            ICharacterDataBuffer buffer,
            WorldStateAnalyzer analyzer,
            WorldState worldState,
            DataManager dataManager)
        {
            _buffer = buffer;
            _analyzer = analyzer;
            _worldState = worldState;
            _dataManager = dataManager;
        }

        public void Handle(SRPacket packet)
        {
            if (!_buffer.IsBuffering) return;

            var raw = _buffer.FinalizeAndGet();
            BotLogger.Debug("CharacterDataEndHandler",
                $"Finalizing character data ({raw.Length} bytes).");

            // Emergency UID lock from first 4 bytes of assembled 0x3013
            if (_worldState.CharacterUniqueID == 0 && raw.Length >= 4)
            {
                uint uid = BitConverter.ToUInt32(raw, 0);
                if (uid > 0)
                {
                    _worldState.CharacterUniqueID = uid;
                    BotLogger.Info("CharacterDataEndHandler",
                        $"[UID] Character UID locked from 0x3013: {uid}");
                }
            }

            // Hex dump for protocol debugging
            try
            {
                string hexDump = BitConverter.ToString(raw).Replace("-", " ");
                System.IO.File.AppendAllText("logs/debug_3013.log",
                    $"[{DateTime.Now:HH:mm:ss.fff}] 0x3013 ({raw.Length} bytes):\n{hexDump}\n\n");
            }
            catch { /* non-critical */ }

            var assembled = new SRPacket(Opcode.SERVER_CHARACTER_DATA, raw);
            _analyzer.ParseCharacterData(assembled);

            BotDiagnostic.RunIfNeeded(_worldState, _dataManager);
        }
    }
}
