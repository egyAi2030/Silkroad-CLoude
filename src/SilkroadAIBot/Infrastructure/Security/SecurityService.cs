using System;
using System.Collections.Generic;
using System.Linq;
using SecurityAPI;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Infrastructure.Security
{
    /// <summary>
    /// Wrapper implementation of <see cref="ISecurityService"/> that delegates to the legacy <see cref="SecurityAPI.Security"/> class.
    /// Performs mapping between legacy <see cref="Packet"/> and domain <see cref="SRPacket"/>.
    /// </summary>
    public sealed class SecurityService : ISecurityService
    {
        private SecurityAPI.Security _legacySecurity;
        private readonly object _lock = new object();

        public SecurityService()
        {
            _legacySecurity = new SecurityAPI.Security();
        }

        public bool IsHandshakeComplete => _legacySecurity.HasHandshake;
        public uint CountSeed => _legacySecurity.CountSeed;
        public uint CRCSeed => _legacySecurity.CRCSeed;
        public ulong InitialBlowfishKey => _legacySecurity.InitialBlowfish;

        public void Recv(byte[] buffer, int offset, int count)
        {
            lock (_lock)
            {
                _legacySecurity.Recv(buffer, offset, count);
            }
        }

        public IEnumerable<SRPacket> GetIncomingPackets()
        {
            lock (_lock)
            {
                var legacyPackets = _legacySecurity.TransferIncoming();
                if (legacyPackets == null) yield break;

                foreach (var p in legacyPackets)
                {
                    yield return MapToDomain(p);
                }
            }
        }

        public byte[] FormatPacket(SRPacket packet)
        {
            lock (_lock)
            {
                // Note: legacy FormatPacket requires the opcode and byte array separately.
                // It also determines encryption based on internal state (m_enc_opcodes).
                return _legacySecurity.FormatPacket(packet.Opcode, packet.Payload, packet.IsEncrypted);
            }
        }

        public IEnumerable<byte[]> GetOutgoingBytes()
        {
            lock (_lock)
            {
                var legacyKvp = _legacySecurity.TransferOutgoing();
                if (legacyKvp == null) yield break;

                foreach (var kvp in legacyKvp)
                {
                    var buffer = kvp.Key;
                    var wireBytes = new byte[buffer.Size];
                    Buffer.BlockCopy(buffer.Buffer, buffer.Offset, wireBytes, 0, buffer.Size);
                    yield return wireBytes;
                }
            }
        }

        public void InitializeAsServer(uint countSeed, uint crcSeed, ulong initialBlowfish)
        {
            lock (_lock)
            {
                var flags = new SecurityAPI.Security.SecurityFlags
                {
                    blowfish = 1,
                    security_bytes = 1,
                    handshake = 1
                };
                _legacySecurity.GenerateSecurity(flags, countSeed, crcSeed, initialBlowfish);
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _legacySecurity = new SecurityAPI.Security();
            }
        }

        private static SRPacket MapToDomain(Packet legacyPacket)
        {
            return new SRPacket(
                legacyPacket.Opcode,
                legacyPacket.GetBytes(),
                legacyPacket.Encrypted
            );
        }
    }
}
