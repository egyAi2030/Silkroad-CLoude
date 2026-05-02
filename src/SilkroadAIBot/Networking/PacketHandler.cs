using System;
using System.Collections.Generic;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Domain.Entities;
using SecurityAPI;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.Networking
{
    public class PacketHandler
    {
        private readonly WorldState _worldState;
        private readonly SilkroadAIBot.Data.DataManager _dataManager;
        private readonly WorldStateAnalyzer _analyzer;
        private readonly PacketParser _parser;
        private readonly List<byte> _characterDataBuffer = new List<byte>();

        public PacketHandler(WorldState worldState, SilkroadAIBot.Data.DataManager dataManager)
        {
            _worldState = worldState;
            _dataManager = dataManager;
            _analyzer = new WorldStateAnalyzer(worldState, worldState, dataManager);
            _parser = new PacketParser(worldState, worldState, dataManager);
        }

        public static event Action<Packet, bool>? OnGlobalPacketReceived;

        public void ResetCharacterDataBuffer()
        {
            _characterDataBuffer.Clear();
            BotLogger.Info("PacketHandler", "Character Data Buffer Reset.");
        }

        public void AppendCharacterData(Packet packet)
        {
            byte[] data = packet.ReadByteArray(packet.RemainingRead());
            _characterDataBuffer.AddRange(data);
            BotLogger.Debug("PacketHandler", $"Appended {data.Length} bytes to Character Data Buffer. Total: {_characterDataBuffer.Count}");
        }

        public void FinalizeCharacterData()
        {
            if (_characterDataBuffer.Count == 0) return;

            BotLogger.Debug("PacketHandler", $"Finalizing Character Data ({_characterDataBuffer.Count} bytes)");
            Packet assembledPacket = new Packet(Opcode.SERVER_CHARACTER_DATA, false, false, _characterDataBuffer.ToArray());
            assembledPacket.Lock();
            
            var srPacket = new SilkroadAIBot.Domain.Network.SRPacket(assembledPacket.Opcode, assembledPacket.GetBytes(), assembledPacket.Encrypted);
            _analyzer.ParseCharacterData(srPacket);
            _characterDataBuffer.Clear();
        }

        public static void TriggerGlobalPacket(Packet packet, bool isSent)
        {
            OnGlobalPacketReceived?.Invoke(packet, isSent);
        }

        private bool _isBufferingCharacterData = false;

        public void HandlePacket(Packet packet)
        {
            var srPacket = new SilkroadAIBot.Domain.Network.SRPacket(packet.Opcode, packet.GetBytes(), packet.Encrypted);
            OnGlobalPacketReceived?.Invoke(packet, false);

            switch (packet.Opcode)
            {
                // ── Core Character Data ──────────────────────────────
                case Opcode.SERVER_CHARACTER_DATA_BEGIN: // 0x34A5
                    _isBufferingCharacterData = true;
                    ResetCharacterDataBuffer();
                    break;

                case Opcode.SERVER_CHARACTER_DATA:       // 0x3013
                    if (_isBufferingCharacterData)
                    {
                        AppendCharacterData(packet);
                    }
                    else
                    {
                        _analyzer.ParseCharacterData(srPacket);
                    }
                    break;

                case Opcode.SERVER_CHARACTER_DATA_END:   // 0x34A6
                    if (_isBufferingCharacterData)
                    {
                        FinalizeCharacterData();
                        _isBufferingCharacterData = false;
                    }
                    break;


                // ── HP / MP ──────────────────────────────────────────
                case Opcode.SERVER_ENTITY_UPDATE_STATUS: // 0x3057
                case Opcode.SERVER_ENTITY_HPMP_UPDATE:  // 0x3054 (legacy alias)
                    _parser.ParseHpMpUpdate(srPacket);
                    break;

                // ── Skills / Inventory ───────────────────────────────
                case 0xAA17:                             // Skill list from server
                    _parser.ParseSkillList(srPacket);
                    break;

                case 0xAA7F:                             // Inventory contents
                    _parser.ParseInventory(srPacket);
                    break;

                // ── XP ───────────────────────────────────────────────

                // ── Entity Spawns ────────────────────────────────────
                case Opcode.SERVER_ENTITY_HPMP_AUTO:    // 0x3056
                    _parser.ParseHpMpUpdate(srPacket);
                    break;

                case Opcode.SERVER_PLAYER_STATS:        // 0x303D
                    _parser.ParseCharacterStats(srPacket);
                    break;

                case Opcode.SERVER_XP_UPDATE:           // 0x305C
                    _parser.ParseXpGain(srPacket);
                    break;

                case 0x3015:                            // Bulk Spawn
                case 0xAA12:
                    _parser.ParseNearbyEntities(srPacket);
                    break;

                case Opcode.SERVER_SINGLE_SPAWN:        // 0x3019
                    _parser.ParseSingleSpawn(srPacket);
                    break;

                case Opcode.SERVER_SINGLE_DESPAWN:      // 0x3016
                case Opcode.SERVER_GROUP_DESPAWN:       // 0x3017
                case 0x3018:
                    _parser.ParseEntityDespawn(srPacket);
                    break;

                case 0x3020:                            // Entity position update
                    _parser.ParseEntityPosition(srPacket);
                    break;

                case 0x30C9:                            // Kill confirmed
                    _parser.ParseKillConfirmed(srPacket);
                    break;

                // ── Movement ─────────────────────────────────────────
                case 0xB021:                             // Entity movement update
                    _parser.ParseEntityMovement(srPacket);
                    break;

                case Opcode.SERVER_ENTITY_MOVEMENT:      // 0x30D2
                    _analyzer.ParseMovement(srPacket);
                    break;

                // ── Combat Events ─────────────────────────────────────
                case 0xB070:                             // Skill used
                    _parser.ParseSkillUsed(srPacket);
                    break;

                case 0xB071:                             // Skill hit result
                    _parser.ParseSkillHit(srPacket);
                    break;

                case 0xAA78:                             // Hotbar data
                    _parser.ParseHotbar(srPacket);
                    break;

                case 0x3101:                             // Party/Guild member list
                    _parser.ParsePartyGuild(srPacket);
                    break;

                case 0x3305:                             // Guild info
                    _parser.ParseGuildInfo(srPacket);
                    break;

                case 0x30D0:                             // Monster aggro
                    _parser.ParseMonsterAggro(srPacket);
                    break;
                
                case 0xB04C:                             // Item use response
                    _parser.ParseItemUseResponse(srPacket);
                    break;
            }
        }
    }
}



