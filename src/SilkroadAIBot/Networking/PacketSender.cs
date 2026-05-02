using System;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Domain.Network;
using SilkroadAIBot.Application.Interfaces;

namespace SilkroadAIBot.Networking
{
    /// <summary>
    /// v1.3.2 — Sends C→S packets to the game server via the proxy connection.
    /// Keeps all outgoing packet construction in one place.
    /// Consolidated and de-duplicated to resolve CS0111 errors.
    /// </summary>
    public class PacketSender : IPacketSender
    {
        private readonly Func<ClientlessConnection?> _connectionProvider;
        private readonly IWorldStateRepository _worldState;

        public PacketSender(IWorldStateRepository worldState, Func<ClientlessConnection?> connectionProvider)
        {
            _worldState = worldState;
            _connectionProvider = connectionProvider;
        }

        private ClientlessConnection? _connection => _connectionProvider();

        public bool IsConnected => _connection?.IsConnected == true;

        // ─────────────────────────────────────────────────────────────
        // 0x7021 — Character Movement (walk to position)
        // ─────────────────────────────────────────────────────────────
        public void SendMovement(SRCoord destination)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(Opcode.CLIENT_CHARACTER_MOVEMENT);
            writer.WriteByte(1); // 1 = Walk, 2 = Run
            writer.WriteUInt16(destination.Region);
            if (destination.Region > 32767) // Dungeon
            {
                writer.WriteInt32((int)destination.X);
                writer.WriteInt32((int)destination.Z);
                writer.WriteInt32((int)destination.Y);
            }
            else
            {
                writer.WriteInt16((short)destination.X);
                writer.WriteInt16((short)destination.Z);
                writer.WriteInt16((short)destination.Y);
            }
            _connection!.SendPacket(writer.Build());
            BotLogger.Debug("PacketSender", $"[Move] → Region={destination.Region} X={destination.X:F1} Y={destination.Y:F1}");
        }

        // ─────────────────────────────────────────────────────────────
        // 0x704B — Pickup
        // ─────────────────────────────────────────────────────────────
        public void SendPickup()
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(Opcode.CLIENT_CHARACTER_PICKUP);
            _connection!.SendPacket(writer.Build());
            BotLogger.Info("PacketSender", "[Pickup] Sent 0x704B");
        }

        // ─────────────────────────────────────────────────────────────
        // 0x7045 — Select Target
        // ─────────────────────────────────────────────────────────────
        public void SendSelectTarget(uint uniqueID)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(Opcode.CLIENT_CHARACTER_ACTION_REQUEST_SELECTION);
            writer.WriteUInt32(uniqueID);
            
            if (!string.IsNullOrEmpty(_worldState.LastSelectionHash))
            {
                writer.WriteAscii(_worldState.LastSelectionHash);
            }

            _connection!.SendPacket(writer.Build());
            BotLogger.Debug("PacketSender", $"[Select] → Target {uniqueID} {(string.IsNullOrEmpty(_worldState.LastSelectionHash) ? "" : "(Hash Appended)")}");
        }

        // ─────────────────────────────────────────────────────────────
        // 0x7074 — Cast Skill (attack/buff)
        // ─────────────────────────────────────────────────────────────
        public void SendCastSkill(uint skillID, uint targetUID = 0)
        {
            SendCastSkill(skillID, targetUID, null);
        }

        public void SendCastSkill(uint skillID, uint targetUID, SRCoord? targetPos)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(Opcode.CLIENT_CHARACTER_ACTION_REQUEST);
            writer.WriteByte(0x01); // MainAction: Skill
            writer.WriteByte(0x04); // SubAction: Cast
            writer.WriteUInt32(skillID);
            
            if (targetUID != 0)
            {
                writer.WriteByte(1);    // TargetType: Entity
                writer.WriteUInt32(targetUID);
            }
            else if (targetPos != null)
            {
                writer.WriteByte(2);    // TargetType: Location
                writer.WriteUInt16(targetPos.Region);
                writer.WriteInt16((short)targetPos.X);
                writer.WriteInt16((short)targetPos.Z);
                writer.WriteInt16((short)targetPos.Y);
            }
            else
            {
                writer.WriteByte(0);    // TargetType: None (Self/Buff)
            }
            
            _connection!.SendPacket(writer.Build());
            BotLogger.Debug("PacketSender", $"[Skill] → Cast 0x{skillID:X} on target {targetUID}");
        }

        // ─────────────────────────────────────────────────────────────
        // 0x7074 — Basic Attack
        // ─────────────────────────────────────────────────────────────
        public void SendBasicAttack(uint targetUID)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(Opcode.CLIENT_CHARACTER_ACTION_REQUEST);
            writer.WriteByte(0x01); // MainAction: Attack
            writer.WriteByte(0x02); // SubAction: Basic Attack
            writer.WriteUInt32(1);
            writer.WriteByte(1);    // TargetType: Entity
            writer.WriteUInt32(targetUID);
            _connection!.SendPacket(writer.Build());
            BotLogger.Debug("PacketSender", $"[Attack] → Basic attack on {targetUID}");
        }

        // ─────────────────────────────────────────────────────────────
        // 0x704C — Use Inventory Item (potion/pill)
        // ─────────────────────────────────────────────────────────────
        public void SendUseItem(byte slot)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(Opcode.CLIENT_INVENTORY_ITEM_USE);
            writer.WriteByte(slot);
            writer.WriteByte(0x00);
            _connection!.SendPacket(writer.Build());
            BotLogger.Info("PacketSender", $"[Item] → Used item at slot {slot}");
        }

        // ─────────────────────────────────────────────────────────────
        // Handshake & Init Packets
        // ─────────────────────────────────────────────────────────────
        public void SendSpawnConfirm()
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(Opcode.CLIENT_CHARACTER_CONFIRM_SPAWN);
            _connection!.SendPacket(writer.Build());
            BotLogger.Info("PacketSender", "[Spawn] Sent 0x3012");
        }

        public void SendPostSpawnInit()
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x750E);
            _connection!.SendPacket(writer.Build());
        }

        public void SendLoadingComplete()
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x34B6);
            _connection!.SendPacket(writer.Build());
        }

        public void SendResurrection(byte type)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x3053);
            writer.WriteByte(type);
            _connection!.SendPacket(writer.Build());
        }

        public void SendHeartbeat()
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(Opcode.CLIENT_KEEPALIVE);
            _connection!.SendPacket(writer.Build());
        }

        // ─────────────────────────────────────────────────────────────
        // Party Actions
        // ─────────────────────────────────────────────────────────────
        public void SendPartyCreate(uint targetUID, byte settings)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x7060);
            writer.WriteUInt32(targetUID);
            writer.WriteByte(settings);
            _connection!.SendPacket(writer.Build());
        }

        public void SendPartyInvite(uint targetUID)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x7062);
            writer.WriteUInt32(targetUID);
            _connection!.SendPacket(writer.Build());
        }

        public void SendPartyLeave()
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x7061);
            _connection!.SendPacket(writer.Build());
        }

        public void SendPartyKick(uint memberJID)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x7063);
            writer.WriteUInt32(memberJID);
            _connection!.SendPacket(writer.Build());
        }

        public void SendPartyMatchingJoin(uint partyNumber)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x706D);
            writer.WriteUInt32(partyNumber);
            _connection!.SendPacket(writer.Build());
        }

        // ─────────────────────────────────────────────────────────────
        // Stall & Exchange
        // ─────────────────────────────────────────────────────────────
        public void SendStallCreate(string stallName)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x70B1);
            writer.WriteAscii(stallName);
            _connection!.SendPacket(writer.Build());
        }

        public void SendStallTalk(uint stallUID)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x70B3);
            writer.WriteUInt32(stallUID);
            _connection!.SendPacket(writer.Build());
        }

        public void SendStallLeave()
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x70B5);
            _connection!.SendPacket(writer.Build());
        }

        public void SendStallDestroy()
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x70B2);
            _connection!.SendPacket(writer.Build());
        }

        public void SendStallBuy(byte slot)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x70B4);
            writer.WriteByte(slot);
            _connection!.SendPacket(writer.Build());
        }

        public void SendExchangeStart(uint targetUID)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x7081);
            writer.WriteUInt32(targetUID);
            _connection!.SendPacket(writer.Build());
        }

        public void SendExchangeApprove()
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x7083);
            _connection!.SendPacket(writer.Build());
        }

        public void SendExchangeConfirm()
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x7082);
            _connection!.SendPacket(writer.Build());
        }

        public void SendExchangeCancel()
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x7084);
            _connection!.SendPacket(writer.Build());
        }

        // ─────────────────────────────────────────────────────────────
        // Chat & Teleport
        // ─────────────────────────────────────────────────────────────
        public void SendChat(byte chatType, string message, string targetName = "")
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x7025);
            writer.WriteByte(chatType);
            writer.WriteByte(0); // Index
            if (chatType == 2) writer.WriteAscii(targetName);
            writer.WriteAscii(message);
            _connection!.SendPacket(writer.Build());
        }

        public void SendTeleportUse(uint npcUID, byte teleportType, uint teleportID = 0, byte guideType = 0)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x705A);
            writer.WriteUInt32(npcUID);
            writer.WriteByte(teleportType);
            if (teleportType == 2) writer.WriteUInt32(teleportID);
            else if (teleportType == 3) writer.WriteByte(0);
            else if (teleportType == 5) writer.WriteByte(guideType);
            _connection!.SendPacket(writer.Build());
        }

        public void SendLogout(byte logoutMode = 1)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x7005);
            writer.WriteByte(logoutMode);
            _connection!.SendPacket(writer.Build());
        }

        // ─────────────────────────────────────────────────────────────
        // Inventory & Alchemy
        // ─────────────────────────────────────────────────────────────
        public void SendItemMove(byte actionType, byte sourceSlot, byte destSlot, ushort quantity = 0)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(Opcode.CLIENT_INVENTORY_ITEM_MOVEMENT);
            writer.WriteByte(actionType);
            writer.WriteByte(sourceSlot);
            writer.WriteByte(destSlot);
            if (quantity > 0) writer.WriteUInt16(quantity);
            _connection!.SendPacket(writer.Build());
        }

        public void SendAlchemyReinforce(byte itemSlot, byte elixirSlot, byte luckPowderSlot = 255)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(Opcode.CLIENT_ALCHEMY_REINFORCE);
            writer.WriteByte(1); // Action
            writer.WriteByte(itemSlot);
            writer.WriteByte(elixirSlot);
            writer.WriteByte(luckPowderSlot);
            _connection!.SendPacket(writer.Build());
        }

        public void SendAlchemyEnchant(byte itemSlot, byte magicStoneSlot)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(Opcode.CLIENT_ALCHEMY_ENCHANT);
            writer.WriteByte(1); // Action
            writer.WriteByte(itemSlot);
            writer.WriteByte(magicStoneSlot);
            _connection!.SendPacket(writer.Build());
        }

        // ─────────────────────────────────────────────────────────────
        // NPC & Entity Interaction
        // ─────────────────────────────────────────────────────────────
        public void SendEntityAction(uint entityUID, byte actionType = 1)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x7046);
            writer.WriteUInt32(entityUID);
            writer.WriteByte(actionType);
            _connection!.SendPacket(writer.Build());
        }

        public void SendAction(byte actionType)
        {
            if (!IsConnected) return;
            using var writer = new SRPacketWriter(0x704F);
            writer.WriteByte(actionType);
            _connection!.SendPacket(writer.Build());
        }

        // ─────────────────────────────────────────────────────────────
        // Raw Packet
        // ─────────────────────────────────────────────────────────────
        public void SendPacket(SRPacket packet)
        {
            _connection?.SendPacket(packet);
        }
    }
}
