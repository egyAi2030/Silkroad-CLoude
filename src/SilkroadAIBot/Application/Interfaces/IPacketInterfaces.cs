using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Application.Interfaces
{
    /// <summary>
    /// Constructs and sends C→S game packets to the agent server.
    /// All send methods are synchronous fire-and-forget socket writes.
    /// Callers must check <see cref="IsConnected"/> before sending.
    /// </summary>
    public interface IPacketSender
    {
        /// <summary>Returns <c>true</c> if the agent connection is currently active.</summary>
        bool IsConnected { get; }

        /// <summary>Sends a movement request (0x7021).</summary>
        void SendMovement(SRCoord destination);

        /// <summary>Sends a skill cast request (0x7074).</summary>
        void SendCastSkill(uint skillID, uint targetUID = 0);
        void SendCastSkill(uint skillID, uint targetUID, SRCoord? targetPos);

        /// <summary>Sends a basic attack request (0x7074).</summary>
        void SendBasicAttack(uint targetUID);

        /// <summary>Sends an inventory item use request (0x704C).</summary>
        void SendUseItem(byte slot);

        /// <summary>Sends a ground item pickup request (0x704B).</summary>
        void SendPickup();

        /// <summary>Sends a keepalive heartbeat (0x2002).</summary>
        void SendHeartbeat();

        /// <summary>Sends a chat message (0x7025).</summary>
        void SendChat(byte chatType, string message, string targetName = "");

        /// <summary>Sends a target selection request (0x7045).</summary>
        void SendSelectTarget(uint uniqueID);

        /// <summary>Sends a character action (Sit/Stand/Zerk) (0x704F).</summary>
        void SendAction(byte type);

        /// <summary>Sends a stall creation request (0x70B1).</summary>
        void SendStallCreate(string name);

        /// <summary>Sends an exchange start request (0x7081).</summary>
        void SendExchangeStart(uint targetUID);

        // --- Party ---
        void SendPartyCreate(uint targetUID, byte settings);
        void SendPartyInvite(uint targetUID);
        void SendPartyLeave();
        void SendPartyKick(uint memberJID);
        void SendPartyMatchingJoin(uint partyNumber);

        // --- Stall & Exchange Additional ---
        void SendStallTalk(uint stallUID);
        void SendStallLeave();
        void SendStallDestroy();
        void SendStallBuy(byte slot);
        void SendExchangeApprove();
        void SendExchangeConfirm();
        void SendExchangeCancel();

        // --- World & Interaction ---
        void SendTeleportUse(uint npcUID, byte teleportType, uint teleportID = 0, byte guideType = 0);
        void SendLogout(byte logoutMode = 1);
        void SendItemMove(byte actionType, byte sourceSlot, byte destSlot, ushort quantity = 0);
        void SendEntityAction(uint entityUID, byte actionType = 1);
        void SendResurrection(byte type);
        
        // --- Init ---
        void SendSpawnConfirm();
        void SendPostSpawnInit();
        void SendLoadingComplete();

        // --- Alchemy ---
        void SendAlchemyReinforce(byte itemSlot, byte elixirSlot, byte luckPowderSlot = 255);
        void SendAlchemyEnchant(byte itemSlot, byte magicStoneSlot);

        /// <summary>Sends a raw pre-built packet.</summary>
        void SendPacket(SRPacket packet);
    }

    /// <summary>
    /// Handles a single incoming S→C packet opcode.
    /// One implementation per opcode group — never shared across opcodes.
    /// </summary>
    public interface IPacketHandler
    {
        /// <summary>
        /// Processes <paramref name="packet"/> and updates world state accordingly.
        /// Implementations must catch all exceptions internally — a handler must never throw.
        /// </summary>
        /// <param name="packet">The incoming decrypted packet to handle.</param>
        void Handle(SRPacket packet);
    }

    /// <summary>
    /// Registers and resolves <see cref="IPacketHandler"/> instances by opcode.
    /// Resolution must be O(1) — no linear search.
    /// </summary>
    public interface IPacketHandlerFactory
    {
        /// <summary>
        /// Registers a handler factory for the given <paramref name="opcode"/>.
        /// Calling this with an already-registered opcode replaces the previous registration.
        /// </summary>
        /// <param name="opcode">The 2-byte packet opcode.</param>
        /// <param name="factory">Factory function that produces a handler instance.</param>
        void Register(ushort opcode, Func<IPacketHandler> factory);

        /// <summary>
        /// Returns a handler for the given <paramref name="opcode"/>, or <c>null</c> if unregistered.
        /// </summary>
        /// <param name="opcode">The 2-byte packet opcode to resolve.</param>
        IPacketHandler? Resolve(ushort opcode);

        /// <summary>Returns all currently registered opcodes.</summary>
        IReadOnlyCollection<ushort> RegisteredOpcodes { get; }
    }

    /// <summary>
    /// Manages the packet processing pipeline using a bounded <c>Channel&lt;SRPacket&gt;</c>.
    /// Producers enqueue packets; a single consumer dequeues and dispatches.
    /// </summary>
    public interface IPacketDispatcher
    {
        /// <summary>
        /// Enqueues <paramref name="packet"/> for processing.
        /// Non-blocking. Returns <c>false</c> if the channel is full (back-pressure applied).
        /// </summary>
        /// <param name="packet">The packet to enqueue.</param>
        bool Enqueue(SRPacket packet);

        /// <summary>
        /// Starts the consumer loop. Dequeues packets and dispatches to registered handlers.
        /// Returns when <paramref name="ct"/> is cancelled.
        /// </summary>
        /// <param name="ct">Cancellation token; cancellation stops the consumer loop cleanly.</param>
        Task StartAsync(CancellationToken ct);
    }
}
