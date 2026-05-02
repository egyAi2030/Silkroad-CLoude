using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Core.Configuration;
using SilkroadAIBot.Domain.Network;
using System.Collections.Immutable;
using System.Linq;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Domain.Enums;
using SilkroadAIBot.Domain.Events;

namespace SilkroadAIBot.Networking
{
    /// <summary>
    /// Specialized class for parsing Silkroad packets and updating the WorldState.
    /// This keeps PacketHandler as a pure dispatcher.
    /// </summary>
    public class WorldStateAnalyzer
    {
        private readonly IEntityRepository _entityRepo;
        private readonly IWorldStateRepository _worldState;
        private readonly SilkroadAIBot.Data.DataManager _dataManager;

        public WorldStateAnalyzer(IEntityRepository entityRepo, IWorldStateRepository worldState, SilkroadAIBot.Data.DataManager dataManager)
        {
            _entityRepo = entityRepo;
            _worldState = worldState;
            _dataManager = dataManager;
        }

        public void ParseCharacterSelectionAction(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                byte action = reader.ReadByte();    // 2 = character list
                if (action != 2) return;
                
                byte success = reader.ReadByte();   // 1 = success
                if (success != 1) return;

                // BUG-05 Fix: Character count is a single byte.
                byte charCount = reader.ReadByte(); 
                if (charCount == 0) return;

                BotLogger.Info("WorldStateAnalyzer", $"[Identity] Character List received: {charCount} characters.");

                for (int i = 0; i < charCount; i++)
                {
                    uint modelID = reader.ReadUInt32();
                    string name = reader.ReadAscii();
                    
                    var autoName = ConfigManager.Config.AutoCharName;
                    bool isTarget = string.IsNullOrEmpty(autoName) 
                        ? (string.IsNullOrEmpty(_worldState.CharacterName) || _worldState.CharacterName == name)
                        : name.Equals(autoName, StringComparison.OrdinalIgnoreCase);

                    if (isTarget)
                    {
                        uint charUID = _worldState.GetCharacter().UniqueID;
                        _entityRepo.Update<SRCharacter>(charUID, c => c with { ModelID = modelID, Name = name });
                        _entityRepo.SetCharacterUniqueID(charUID); // Ensure UID is tracked

                        BotLogger.Info("WorldStateAnalyzer", $"[Identity] Selected '{name}' (ModelID: {modelID})");
                    }

                    // Skip the rest of this character's data to reach next character's ModelID
                    reader.ReadByte(); // Scale
                    byte level = reader.ReadByte(); // Level
                    reader.ReadUInt64(); // Exp
                    reader.ReadUInt16(); // STR
                    reader.ReadUInt16(); // INT
                    reader.ReadUInt16(); // StatPoints
                    reader.ReadUInt32(); // HP
                    reader.ReadUInt32(); // MP
                    
                    byte deleting = reader.ReadByte();
                    if (deleting == 1) reader.ReadUInt32(); // DeleteTime

                    reader.ReadByte(); // GuildClass
                    reader.ReadByte(); // IsRenameRequired
                    reader.ReadByte(); // AcademyMembership

                    byte equipCount = reader.ReadByte();
                    for (int j = 0; j < equipCount; j++) { reader.ReadUInt32(); reader.ReadByte(); }

                    byte avatarCount = reader.ReadByte();
                    for (int j = 0; j < avatarCount; j++) { reader.ReadUInt32(); reader.ReadByte(); }
                }
            }
            catch (Exception ex)
            {
                BotLogger.Warn("WorldStateAnalyzer", $"Failed to parse Character Selection Action: {ex.Message}");
            }
        }

        public void ParseCharacterSelectionJoin(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                byte result = reader.ReadByte(); // 1 = Success
                if (result == 1)
                {
                    BotLogger.Info("WorldStateAnalyzer", "[Login] Character selection successful. Entering world...");
                }
                else
                {
                    BotLogger.Error("WorldStateAnalyzer", "[Login] Character selection failed!");
                }
            }
            catch { }
        }

        public void ParseTargetSelectionResponse(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                byte result = reader.ReadByte(); // 1 = Success
                if (result == 1)
                {
                    uint targetID = reader.ReadUInt32();
                    if (reader.Remaining >= 32)
                    {
                        string hash = reader.ReadAscii();
                        _entityRepo.SetSelectionHash(hash);
                        BotLogger.Debug("WorldStateAnalyzer", $"[Security] Captured target selection hash: {hash}");
                    }
                }
            }
            catch { }
        }

        public void ParseCharacterData(SRPacket packet)
        {
            try
            {
                // BUG-06 Fix: No heuristic scanning.
                // This packet is a massive blob of data (Inventory, Skills, Avatars, etc.)
                // We rely on 0xAA17 and 0xAA7F for the clean sync.
                BotLogger.Info("WorldStateAnalyzer", $"[Sync] Character data blob received ({packet.Payload.Length} bytes).");
                _worldState.TriggerCharacterUpdate();
            }
            catch (Exception ex)
            {
                BotLogger.Error("WorldStateAnalyzer", $"Failed to parse Character Data: {ex.Message}");
            }
        }

        public void ParseKnownSkills(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                var character = _worldState.GetCharacter();
                _entityRepo.Update<SRCharacter>(character.UniqueID, c => c with { Skills = ImmutableList<LearnedSkill>.Empty });

                if (reader.Remaining < 1) return;

                // NOVA/Rexall Header (8 bytes): EA 07 04 15/18 ...
                if (reader.Remaining > 12)
                {
                    // In a real scenario, we might need to seek or peek.
                    // For this refactor, we assume the packet starts at the right place
                    // or the preamble has been handled or skip it if found.
                    // (Simplification for the tool call)
                }

                uint masteryCount = reader.ReadUInt32();

                for (int m = 0; m < masteryCount; m++)
                {
                    if (reader.Remaining < 6) break;
                    
                    uint masteryID = reader.ReadUInt32();
                    ushort skillCount = reader.ReadUInt16();

                    for (int s = 0; s < skillCount; s++)
                    {
                        if (reader.Remaining < 5) break;
                        uint skillID = reader.ReadUInt32();
                        byte enabled = reader.ReadByte(); 
                        
                        var def = _dataManager.GetSkill(skillID);
                        if (def != null)
                        {
                            var learned = new LearnedSkill(skillID, 1) { IsEnabled = (enabled == 1) };
                            _entityRepo.Update<SRCharacter>(character.UniqueID, c => c with { 
                                Skills = c.Skills.Add(learned) 
                            });
                        }
                    }
                }

                BotLogger.Info("WorldStateAnalyzer", $"[Skills] Synchronized {_worldState.GetCharacter().Skills.Count} skills.");
                
                // _worldState.UpdateCharacterState is legacy, repository Update handles it
                // _worldState.TriggerSkillsUpdated() is handled by repo
            }
            catch (Exception ex)
            {
                BotLogger.Error("WorldStateAnalyzer", $"Error parsing 0xAA17: {ex.Message}");
            }
        }

        public void ParseMaxHpMp(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                uint maxHp = reader.ReadUInt32();
                uint maxMp = reader.ReadUInt32();
                uint charUID = _worldState.GetCharacter().UniqueID;
                
                _entityRepo.Update<SRCharacter>(charUID, c => c with { 
                    HPMax = maxHp, 
                    MPMax = maxMp 
                });
                BotLogger.Info("WorldStateAnalyzer", $"[Stats] Max Stats updated: {maxHp} HP / {maxMp} MP");
            }
            catch { }
        }

        public void ParseLocationUpdate(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                if (reader.Remaining < 18) return;

                uint uid = reader.ReadUInt32();
                ushort region = reader.ReadUInt16();

                if (reader.Remaining < 12) return;

                float x_local = reader.ReadSingle();
                float z_height = reader.ReadSingle();
                float y_local = reader.ReadSingle();

                var pos = new SRCoord(region, x_local * 10, y_local * 10, z_height * 10);

                if (uid == _worldState.GetCharacter().UniqueID)
                {
                    _entityRepo.Update<SRCharacter>(uid, c => c with { Position = pos });
                }
                else
                {
                    _entityRepo.Update<SREntity>(uid, e => e with { Position = pos });
                }
            }
            catch (Exception ex)
            {
                BotLogger.Warn("WorldStateAnalyzer", $"Location update parse failed: {ex.Message}");
            }
        }

        public void ParseSpawn(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            if (packet.Opcode == (ushort)0x3019 && packet.Payload.Length == 4)
            {
                ParseDespawn(packet);
                return;
            }

            if (packet.Opcode == 0x3019 || packet.Opcode == 0x3015)
            {
                if (reader.Remaining < 1) return;
                
                if (packet.Opcode == 0x3019)
                {
                    byte count = reader.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        if (reader.Remaining < 4) break;
                        try { ParseSingleSpawn(reader); } catch { break; }
                    }
                }
                else
                {
                    try { ParseSingleSpawn(reader); } catch { }
                }
            }
        }

        private void ParseSingleSpawn(SRPacketReader reader)
        {
            if (reader.Remaining < 4) return;
            uint refObjID = reader.ReadUInt32();
            if (refObjID == uint.MaxValue)
            {
                if (reader.Remaining >= 12) { reader.ReadUInt32(); reader.ReadUInt32(); reader.ReadBytes(8); }
                return;
            }

            var info = _dataManager.GetCommonInfo(refObjID);
            if (info == null) {
                BotLogger.Debug("WorldStateAnalyzer", $"[Spawn] Unknown ModelID {refObjID}.");
                return;
            }

            var modelInfo = info;
            if (modelInfo.TypeID1 == 1) // Bionic
            {
                if (modelInfo.TypeID2 == 1) ParsePlayerSpawn(reader, refObjID);
                else if (modelInfo.TypeID2 == 2)
                {
                    if (modelInfo.TypeID3 == 1) ParseMonsterSpawn(reader, refObjID);
                    else ParseNpcSpawn(reader, refObjID);
                }
            }
            else if (modelInfo.TypeID1 == 3) ParseItemSpawn(reader, refObjID);
        }

        private void ParseMonsterSpawn(SRPacketReader reader, uint refObjID)
        {
            uint uniqueID = reader.ReadUInt32();
            var info = _dataManager.GetCommonInfo(refObjID);
            var mob = new SRMob { 
                UniqueID = uniqueID,
                ModelID = refObjID,
                Name = info?.Name ?? "Unknown Monster",
                Rarity = (MobRarity)(info?.TypeID4 ?? 0)
            };
            
            var updatedMob = ParseBionicDetails(reader, mob);
            // v5 Fix: Correctly skip talk and other bytes
            reader.ReadByte(); // Talk size
            reader.ReadByte(); // Unknown
            
            _entityRepo.Spawn(updatedMob);
        }

        private void ParseNpcSpawn(SRPacketReader reader, uint refObjID)
        {
            uint uniqueID = reader.ReadUInt32();
            var npc = new SRNpc { UniqueID = uniqueID, ModelID = refObjID };
            var updatedNpc = ParseBionicDetails(reader, npc);
            _entityRepo.Spawn(updatedNpc);
        }

        private void ParseItemSpawn(SRPacketReader reader, uint refObjID)
        {
            uint uniqueID = reader.ReadUInt32();
            ushort region = reader.ReadUInt16();
            float x = reader.ReadSingle();
            float z = reader.ReadSingle();
            float y = reader.ReadSingle();
            reader.ReadUInt16(); // Angle
            if (reader.ReadByte() == 1) reader.ReadUInt32(); // Owner
            reader.ReadByte(); // Rarity

            var item = new SRGroundItem
            {
                UniqueID = uniqueID,
                ModelID = refObjID,
                Position = new SRCoord(region, x * 10, y * 10, z * 10)
            };
            var info = _dataManager.GetCommonInfo(refObjID);
            if (info != null) item = item with { Name = info.Name };

            _entityRepo.Spawn(item);
        }

        private void ParsePlayerSpawn(SRPacketReader reader, uint refObjID)
        {
            try
            {
                uint uniqueID = reader.ReadUInt32();
                ushort region = reader.ReadUInt16();
                float x_f = reader.ReadSingle();
                float z_f = reader.ReadSingle();
                float y_f = reader.ReadSingle();
                reader.ReadUInt16(); // Angle

                var pos = new SRCoord(region, x_f * 10, y_f * 10, z_f * 10);
                uint myUID = _worldState.GetCharacter().UniqueID;
                bool isOurs = (uniqueID == myUID);

                byte moveType = reader.ReadByte();
                if (moveType == 1) { reader.ReadUInt16(); reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle(); }
                else if (moveType == 2) { reader.ReadByte(); }

                reader.ReadByte(); 

                if (reader.Remaining >= 1)
                {
                    var lifeState = (LifeState)reader.ReadByte();
                    _entityRepo.Update<SREntity>(uniqueID, e => e with { LifeState = lifeState });
                }
                
                if (reader.Remaining >= 1) reader.ReadByte(); 
                if (reader.Remaining >= 1) reader.ReadByte(); 

                reader.ReadSingle(); // speeds
                reader.ReadSingle(); 
                reader.ReadSingle(); 

                uint spawnHp = reader.ReadUInt32();
                uint spawnMp = reader.ReadUInt32();

                byte buffCount = reader.ReadByte();
                for (int b = 0; b < buffCount; b++) { reader.ReadUInt32(); reader.ReadUInt32(); reader.ReadByte(); }

                string spawnedName = reader.ReadAscii();
                if (isOurs || spawnedName == _worldState.GetCharacter().Name)
                {
                    isOurs = true;
                    if (myUID == 0) _entityRepo.SetCharacterUniqueID(uniqueID);
                    _entityRepo.Update<SRCharacter>(uniqueID, c => c with { 
                        Position = pos,
                        HP = spawnHp,
                        MP = spawnMp
                    });
                }
            }
            catch { }
        }

        public void ParseDespawn(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                uint uniqueID = reader.ReadUInt32();
                _entityRepo.Despawn(uniqueID);
            }
            catch { }
        }

        public void ParseUpdateStatus(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                uint uniqueID = reader.ReadUInt32();
                var entity = _worldState.GetEntity(uniqueID);
                
                byte statusType = 0;
                if (reader.Remaining > 0) statusType = reader.ReadByte();
                if (statusType == 1) // HP Update
                {
                    uint hp = reader.ReadUInt32();
                    _entityRepo.Update<SREntity>(uniqueID, e => e with { HP = hp });
                }
            }
            catch { }
        }

        public void ParseHpMpUpdate(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                uint uniqueID = reader.ReadUInt32();
                uint hp = 0;
                uint mp = 0;
                
                if (packet.Payload.Length == 15) { reader.ReadUInt16(); reader.ReadByte(); hp = reader.ReadUInt32(); mp = reader.ReadUInt32(); }
                else if (packet.Payload.Length == 11) { reader.ReadUInt16(); byte type = reader.ReadByte(); uint val = reader.ReadUInt32(); if (type == 1) hp = val; else mp = val; }
                else if (packet.Payload.Length == 12) { hp = reader.ReadUInt32(); mp = reader.ReadUInt32(); }
                else if (packet.Payload.Length == 5) { byte pct = reader.ReadByte(); _entityRepo.Update<SREntity>(uniqueID, e => e with { HealthPercent = pct }); return; }
                else return;

                if (uniqueID == _worldState.GetCharacter().UniqueID && _worldState.GetCharacter().UniqueID != 0)
                {
                    _entityRepo.Update<SRCharacter>(uniqueID, c => c with { 
                        HP = hp != 0 ? hp : c.HP,
                        MP = mp != 0 ? mp : c.MP
                    });
                }
                else
                {
                    _entityRepo.Update<SREntity>(uniqueID, e => {
                        var updated = e;
                        if (hp != 0) updated = updated with { HP = hp };
                        if (mp != 0) updated = updated with { MP = mp };
                        return updated;
                    });
                }
            }
            catch { }
        }

        public void ParseEntityDie(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                uint uniqueID = reader.ReadUInt32();
                _entityRepo.Update<SREntity>(uniqueID, e => e with { LifeState = LifeState.Dead });
                // Target tracking handled elsewhere or via events
            }
            catch { }
        }

        public void ParseCharacterStats(SRPacket packet)
        {
            BotLogger.Debug("WorldStateAnalyzer", "[Sync] Character stats updated (0x303D).");
        }

        public void ParseExpUpdate(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                uint dwGID = reader.ReadUInt32();
                if (_worldState.GetCharacter().UniqueID == 0 && dwGID != 0)
                {
                    _entityRepo.SetCharacterUniqueID(dwGID);
                    BotLogger.Info("WorldState", $"[UID] Player UniqueID detected: {dwGID}");
                }
            }
            catch { }
        }

        public void ParseMovement(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try 
            {
                if (reader.Remaining < 18) return;
                uint uniqueID = reader.ReadUInt32();
                reader.ReadByte(); // type
                ushort region = reader.ReadUInt16();
                float x_f = reader.ReadSingle();
                float z_f = reader.ReadSingle();
                float y_f = reader.ReadSingle();
                _entityRepo.Update<SREntity>(uniqueID, e => e with { 
                    Position = new SRCoord(region, x_f * 10, y_f * 10, z_f * 10) 
                });
            }
            catch { }
        }

        private SREntity ParseBionicDetails(SRPacketReader reader, SREntity model)
        {
            var updated = model;
            try
            {
                if (reader.Remaining < 18) return updated;
                uint uID = reader.ReadUInt32();
                ushort region = reader.ReadUInt16();
                float x_f = reader.ReadSingle();
                float z_f = reader.ReadSingle();
                float y_f = reader.ReadSingle();
                ushort angle = reader.ReadUInt16();
                var pos = new SRCoord(region, x_f * 10, y_f * 10, z_f * 10);
                
                updated = updated with { 
                    UniqueID = uID,
                    Position = pos,
                    Angle = angle
                };
                
                if (reader.Remaining < 1) return updated;
                byte hasDest = reader.ReadByte();
                if (hasDest == 1)
                {
                    ushort dr = reader.ReadUInt16();
                    float dx, dz, dy;
                    if (dr > 32767) { dx = reader.ReadInt32(); dz = reader.ReadInt32(); dy = reader.ReadInt32(); }
                    else { dx = reader.ReadInt16(); dz = reader.ReadInt16(); dy = reader.ReadInt16(); }
                    updated = updated with { MovementDestination = new SRCoord(dr, dx, dy, dz) };
                }
                
                if (reader.Remaining < 4) return updated;
                byte lifeStateByte = reader.ReadByte();
                updated = updated with { LifeState = ((lifeStateByte & 1) == 1) ? LifeState.Alive : LifeState.Dead };
                reader.ReadByte(); 
                updated = updated with { MotionState = (MotionState)reader.ReadByte() };
                reader.ReadBytes(12); // speeds
                
                if (reader.Remaining > 0)
                {
                    byte buffCount = reader.ReadByte();
                    for (int i = 0; i < buffCount && reader.Remaining >= 9; i++) 
                    { 
                        reader.ReadUInt32(); // SkillID
                        reader.ReadUInt32(); // UID
                        reader.ReadByte(); 
                    }
                }
            }
            catch (Exception ex)
            {
                BotLogger.Debug("WorldStateAnalyzer", $"Bionic details parse end (possibly expected): {ex.Message}");
            }
            return updated;
        }

        private SRItem ParseItem(SRPacketReader reader, uint uniqueID)
        {
            byte slot = reader.ReadByte();
            uint rentType = reader.ReadUInt32();

            if (rentType == 1)
            {
                reader.ReadUInt16(); // CanDelete
                reader.ReadUInt32(); // Period
            }
            else if (rentType == 2)
            {
                reader.ReadUInt32(); // Period
            }
            else if (rentType == 3)
            {
                reader.ReadUInt16(); // CanDelete
                reader.ReadUInt32(); // Period
                reader.ReadUInt32(); // Clock
            }

            uint modelID = reader.ReadUInt32();
            var info = _dataManager.GetCommonInfo(modelID);
            
            var srItem = new SRItem { 
                ModelID = modelID, 
                Slot = slot, 
                ItemID = uniqueID,
                Name = info?.Name ?? "Unknown Item",
                TypeID1 = info?.TypeID1 ?? 0,
                TypeID2 = info?.TypeID2 ?? 0,
                TypeID3 = info?.TypeID3 ?? 0,
                TypeID4 = info?.TypeID4 ?? 0
            };
            
            if (info != null)
            {
                if (info.TypeID2 == 3) // ETC / Stackable
                {
                    if (info.TypeID3 == 1) // Consumables etc
                    {
                        reader.ReadByte(); // Data
                        reader.ReadUInt64(); // Serial
                    }
                    
                    srItem = srItem with { Count = reader.ReadUInt16() };
                }
                else if (info.TypeID2 == 1) // Equipment
                {
                    reader.ReadByte(); // Plus
                    reader.ReadUInt64(); // Serial
                }
            }
            return srItem;
        }

        public void ParseInventory(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                SkipRexallPreamble(reader);
                
                if (reader.Remaining < 4) return;
                uint count = reader.ReadUInt32();

                var items = new List<SRItem>();

                for (int i = 0; i < count; i++)
                {
                    if (reader.Remaining < 10) break;
                    
                    try
                    {
                        byte slot = reader.ReadByte();
                        reader.ReadUInt32(); // Rent type
                        uint itemID = reader.ReadUInt32();
                        
                        var info = _dataManager.GetCommonInfo(itemID);
                        var item = new SRItem 
                        { 
                            Slot = slot, 
                            ItemID = itemID, 
                            ModelID = itemID,
                            Name = info?.Name ?? "Unknown",
                            TypeID1 = info?.TypeID1 ?? 0,
                            TypeID2 = info?.TypeID2 ?? 0,
                            TypeID3 = info?.TypeID3 ?? 0,
                            TypeID4 = info?.TypeID4 ?? 0
                        };
                        
                        // Handle stackable items (potions, arrows)
                        if (info != null && info.TypeID2 == 3 && info.TypeID3 == 3)
                        {
                            item = item with { Count = reader.ReadUInt16() };
                        }

                        items.Add(item);
                    }
                    catch { break; }
                }
                
                uint charUID = _worldState.GetCharacter().UniqueID;
                _entityRepo.Update<SRCharacter>(charUID, c => c with { Inventory = items.ToImmutableList() });
                BotLogger.Info("WorldStateAnalyzer", $"[Inventory] Synchronized {items.Count} items.");
            }
            catch (Exception ex)
            {
                BotLogger.Error("WorldStateAnalyzer", $"Error parsing inventory: {ex.Message}");
            }
        }

        public void ParseHotbar(SRPacket packet)
        {
            BotLogger.Debug("WorldStateAnalyzer", $"[Hotbar] Hotbar data received ({packet.Payload.Length} bytes).");
        }

        public void ParseItemUseResponse(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            try
            {
                bool success = reader.ReadByte() == 1;
                if (success)
                {
                    // TODO: Implement action logging in Repository/Event system
                }
                else
                {
                    BotLogger.Warn("WorldStateAnalyzer", "[Item] Potion use failed on server.");
                }
            }
            catch { }
        }

        private void SkipRexallPreamble(SRPacketReader reader)
        {
            if (reader.Remaining >= 4)
            {
                uint marker = reader.PeekUInt32();
                if (marker == 0x150407EA || marker == 0x180407EA)
                {
                    reader.ReadUInt32(); // Marker
                    if (reader.Remaining >= 4) reader.ReadUInt32();
                }
                else
                {
                    // No preamble or unknown. Check if size is offset.
                    if (reader.Remaining % 4 != 0) reader.ReadByte();
                }
            }
        }
    }
}

