using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Application.Interfaces
{
    /// <summary>
    /// Core security service for the Silkroad protocol.
    /// Manages Blowfish encryption, CRC calculation, and the 4-step security handshake.
    /// </summary>
    public interface ISecurityService
    {
        /// <summary>Returns <c>true</c> if the security handshake is complete.</summary>
        bool IsHandshakeComplete { get; }

        /// <summary>Returns the current Blowfish seed/key context information.</summary>
        uint CountSeed { get; }
        uint CRCSeed { get; }
        ulong InitialBlowfishKey { get; }

        /// <summary>
        /// Processes raw incoming bytes from the socket.
        /// Decrypts packets and returns them via <see cref="GetIncomingPackets"/>.
        /// </summary>
        /// <param name="buffer">Raw byte buffer from the socket.</param>
        /// <param name="offset">Start offset in the buffer.</param>
        /// <param name="count">Number of bytes read.</param>
        void Recv(byte[] buffer, int offset, int count);

        /// <summary>
        /// Returns all fully assembled and decrypted packets from the last <see cref="Recv"/> call.
        /// </summary>
        System.Collections.Generic.IEnumerable<SRPacket> GetIncomingPackets();

        /// <summary>
        /// Formats and encrypts a packet for sending over the wire.
        /// Returns the raw bytes to be written to the socket.
        /// </summary>
        /// <param name="packet">The packet to format.</param>
        byte[] FormatPacket(SRPacket packet);

        /// <summary>
        /// Returns the raw encrypted/framed bytes for any pending outgoing packets 
        /// (e.g., automatic handshake responses generated internally).
        /// </summary>
        System.Collections.Generic.IEnumerable<byte[]> GetOutgoingBytes();

        /// <summary>
        /// Initializes the security context as a SERVER.
        /// Used by the Proxy to simulate a game server for the local SRO client.
        /// </summary>
        void InitializeAsServer(uint countSeed, uint crcSeed, ulong initialBlowfish);

        /// <summary>
        /// Resets the security context (Blowfish keys and seeds).
        /// Required when transitioning from Gateway to Agent server.
        /// </summary>
        void Reset();
    }
}
