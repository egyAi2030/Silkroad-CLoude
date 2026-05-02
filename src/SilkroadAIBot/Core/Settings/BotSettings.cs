using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SilkroadAIBot.Core.Settings
{
    public enum JobRoleType { None, Trader, Hunter, Thief }

    public class BotSettings
    {
        [JsonIgnore]
        public static BotSettings Instance { get; private set; } = new BotSettings();

        public int MinLogLevel { get; set; } = 0; // 0=DEBUG, 1=INFO, 2=WARN, 3=ERROR
        public bool VerboseLogging { get; set; } = false;

        // -----------------------------------------
        // 1. Combat
        // -----------------------------------------
        public bool AutoSelectTarget { get; set; } = true;
        public int TargetingRadius { get; set; } = 30; // meters
        public bool IgnorePlayersCOS { get; set; } = true;
        public bool AutoCastBuffs { get; set; } = true;
        public int AutoCastBuffDelay { get; set; } = 500;
        public List<uint> BuffSkillList { get; set; } = new List<uint>();
        public List<uint> BuffPriorityList { get; set; } = new List<uint>();
        public bool AutoCastAttacks { get; set; } = true;
        public bool AllowTargetChasing { get; set; } = true;
        public List<uint> AttackSkillSequence { get; set; } = new List<uint>();
        public bool AutoLearnUpgradeSkills { get; set; } = false;
        public bool AutoAllocateStatPoints { get; set; } = false;

        // -----------------------------------------
        // 2. Safety
        // -----------------------------------------
        public bool AutoHPPotion { get; set; } = true;
        public float AutoHPPotionThreshold { get; set; } = 0.60f; // 60%
        public bool AutoMPPotion { get; set; } = true;
        public float AutoMPPotionThreshold { get; set; } = 0.60f; // 60%
        public bool AutoPill { get; set; } = true;
        public List<string> AutoPillStatuses { get; set; } = new List<string> { "Burn", "Freeze", "Poison", "Zombie" };
        public bool ReturnToTownOnDeath { get; set; } = true;
        public int ReturnToTownDelay { get; set; } = 5000; // ms

        // -----------------------------------------
        // 3. Movement
        // -----------------------------------------
        public bool UseNavMeshPathfinding { get; set; } = true;
        public int NavMeshMaxNodeDistance { get; set; } = 10;
        public bool EnableHuntRadius { get; set; } = true;
        public int HuntRadiusDistance { get; set; } = 50;
        public bool AvoidObstaclesAutoUnstuck { get; set; } = true;
        public int UnstuckWaitTime { get; set; } = 5000;
        public bool UseTeleportNPC { get; set; } = false;
        public string AutoTeleportDestination { get; set; } = "Jangan";
        public bool UseTransportMount { get; set; } = false;
        public string TransportMountType { get; set; } = "Horse";
        public bool UseReturnScroll { get; set; } = false;

        // -----------------------------------------
        // 4. Loot
        // -----------------------------------------
        public bool AutoPickupItems { get; set; } = true;
        public bool ItemFilterIsWhitelist { get; set; } = false;
        public List<uint> ItemFilterList { get; set; } = new List<uint>();
        public bool AutoPickupGold { get; set; } = true;
        public bool AutoSortInventory { get; set; } = false;
        public bool EnablePetPickup { get; set; } = true;

        // -----------------------------------------
        // 5. Supply (Town)
        // -----------------------------------------
        public bool AutoRepairItems { get; set; } = true;
        public float RepairDurabilityThreshold { get; set; } = 0.20f; // 20%
        public bool RestockPotions { get; set; } = true;
        public int RestockHPThreshold { get; set; } = 100;
        public int RestockMPThreshold { get; set; } = 100;
        public int RestockPillThreshold { get; set; } = 50;
        public bool RestockAmmo { get; set; } = false;
        public int RestockAmmoThreshold { get; set; } = 1000;
        public bool NPCStorageAutoDeposit { get; set; } = false;
        public bool NPCShopAutoBuySell { get; set; } = false;

        // -----------------------------------------
        // 6. Quest
        // -----------------------------------------
        public bool AutoAcceptQuests { get; set; } = false;
        public bool AutoCompleteObjectives { get; set; } = false;
        public bool AutoReturnQuests { get; set; } = false;

        // -----------------------------------------
        // 7. Job System
        // -----------------------------------------
        public JobRoleType JobRole { get; set; } = JobRoleType.None;
        public bool AutoEquipJobOutfit { get; set; } = false;
        public bool JobAutoStartTradeRun { get; set; } = false;
        public string TradeRoute { get; set; } = "Jangan-Donwhang";
        public bool HunterAutoProtectTrader { get; set; } = false;
        public bool ThiefAutoAttack { get; set; } = false;
        public bool ThiefAutoCollectGoods { get; set; } = false;

        // -----------------------------------------
        // 8. Social & Chat
        // -----------------------------------------
        public bool SendChatMessages { get; set; } = false;
        public string TargetChatChannel { get; set; } = "General";
        public bool AutoRespondWhispers { get; set; } = false;
        public string WhisperResponse { get; set; } = "I'm currently busy.";
        public bool TriggerEmotes { get; set; } = false;
        public string SelectedEmote { get; set; } = "Wave";

        // -----------------------------------------
        // 9. Party
        // -----------------------------------------
        public bool AutoAcceptPartyInvites { get; set; } = false;
        public bool AcceptOnlyFriendsAndGuild { get; set; } = true;
        public bool AutoFormPartyMatch { get; set; } = false;
        public string PartyMatchName { get; set; } = "Auto EXP Party";
        public int PartyMatchMinLevel { get; set; } = 1;
        public int PartyMatchMaxLevel { get; set; } = 140;
        public bool AutoResurrectPartyMember { get; set; } = false;
        public uint ResurrectSkillId { get; set; } = 0;
        public bool AutoAssistPartyMember { get; set; } = false;

        // -----------------------------------------
        // 10. Guild
        // -----------------------------------------
        public bool AutoManageGuild { get; set; } = false; // Join/Create
        public bool AccessGuildStorage { get; set; } = false;
        public bool RegisterFortressWar { get; set; } = false;
        public bool PlaceCommandPost { get; set; } = false;
        public bool UseCombatFlags { get; set; } = false;

        // -----------------------------------------
        // 11. COS (Pets)
        // -----------------------------------------
        public bool AutoFeedPet { get; set; } = true;
        public float PetFeedThreshold { get; set; } = 0.30f; // 30% HGP
        public bool AutoSummonPet { get; set; } = true;

        // -----------------------------------------
        // 12. Alchemy
        // -----------------------------------------
        public bool AutoReinforce { get; set; } = false;
        public int TargetPlusLevel { get; set; } = 5;
        public bool AutoEnchant { get; set; } = false;
        public string TargetEnchantTier { get; set; } = "STR/INT";

        // -----------------------------------------
        // 13. Network / Stall
        // -----------------------------------------
        public bool AutoStallCreation { get; set; } = false;
        public string AutoStallName { get; set; } = "Auto Shop";
        public bool ConsignmentCheck { get; set; } = false;

        // -----------------------------------------
        // 14. Events
        // -----------------------------------------
        public bool DetectActiveEvents { get; set; } = false;
        public bool AutoParticipateEvents { get; set; } = false;
        public bool AutoExchangeEventItems { get; set; } = false;

        // -----------------------------------------
        // 15. Advanced
        // -----------------------------------------
        public bool FGWAutoFarmPillars { get; set; } = false;
        public bool JobSafeTradeAutomation { get; set; } = false;
        public bool AutoAcademyCreation { get; set; } = false;

        // API compatibility - private constructor
        private BotSettings() { }

        // -----------------------------------------
        // Serialization Methods
        // -----------------------------------------
        private static readonly string SettingsFile = "bot_settings.json";

        public static void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Instance, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[Error] Could not save BotSettings: {ex.Message}");
            }
        }

        public static void Load()
        {
            if (File.Exists(SettingsFile))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFile);
                    var loaded = JsonSerializer.Deserialize<BotSettings>(json);
                    if (loaded != null)
                    {
                        Instance = loaded;
                    }
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"[Error] Could not load BotSettings: {ex.Message}");
                }
            }
        }
    }
}
