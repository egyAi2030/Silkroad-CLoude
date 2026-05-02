using System;

namespace SilkroadAIBot.Domain.Network
{
    /// <summary>
    /// Pure, immutable representation of a Silkroad packet.
    /// Used across Domain and Application layers to decouple from Infrastructure-specific networking libraries.
    /// </summary>
    public record SRPacket(ushort Opcode, byte[] Payload, bool IsEncrypted = false)
    {
        /// <summary>Alias for Payload for backward compatibility.</summary>
        public byte[] Data => Payload;

        /// <summary>Returns the total size of the payload.</summary>
        public int Length => Payload?.Length ?? 0;

        /// <summary>Returns a hex string representation of the opcode and payload for debugging.</summary>
        public override string ToString()
        {
            return $"[0x{Opcode:X4}] Length={Length} Encrypted={IsEncrypted}";
        }
    }
}
