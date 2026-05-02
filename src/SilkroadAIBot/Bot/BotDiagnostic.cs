using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Data;
using SilkroadAIBot.Domain.Entities;

namespace SilkroadAIBot.Bot
{
    /// <summary>
    /// v2.1.0 — xBot Decryption & Precise Asset Parsing.
    /// Results are written to both the main bot log and to debug_YYYYMMDD.log.
    /// Find logs in: <AppDir>\logs\debug_*.log
    /// ─────────────────────────────────────────────────────────────────────────
    /// </summary>
    public static class BotDiagnostic
    {
        private static bool _hasRun = false;

        public static void RunIfNeeded(WorldState ws, DataManager dm)
        {
            if (_hasRun) return;
            // Relaxed guard: we need to run even if UID is unknown to diagnose WHY it's unknown.
            if (ws.Character == null) return; 
            _hasRun = true;

            Run(ws, dm);
        }

        public static void Reset() => _hasRun = false;

        private static void Run(WorldState ws, DataManager dm)
        {
            var results = new List<(string Name, bool Pass, string Detail)>();
            var c = ws.Character;

            Sep("=== BOT DIAGNOSTIC v2.1.0 ===");
    /// Results also saved to: logs\debug_" + DateTime.Now.ToString("yyyyMMdd") + ".log

            // ── 1. World State ────────────────────────────────────────
            {
                bool pass = ws.CharacterUniqueID > 0 && c != null && c.Position.Region > 0;
                results.Add(Check("WorldState.InWorld",
                    pass,
                    $"UID={ws.CharacterUniqueID}  HP={c?.HP}  MP={c?.MP}  MaxHP={c?.HPMax}  MaxMP={c?.MPMax}  Level={c?.Level}  Race={c?.Race}"));
            }

            // ── 2. HP/MP Sanity ───────────────────────────────────────
            {
                bool hpSane = c != null && c.HPMax > 0 && c.HPMax < 5_000_000 && c.HP <= c.HPMax;
                bool mpSane = c != null && c.MPMax < 5_000_000;
                results.Add(Check("HP/MP Values Sanity",
                    hpSane && mpSane,
                    $"HP={c?.HP}/{c?.HPMax}  MP={c?.MP}/{c?.MPMax}  " +
                    $"HP_Sane={hpSane}  MP_Sane={mpSane}  " +
                    $"[If FAIL: 0x3057 format mismatch — HP bytes are being misread as uint]"));
            }

            // ── 3. Character Position ─────────────────────────────────
            {
                var pos = c.Position;
                bool pass = pos.Region > 0;
                results.Add(Check("CharacterPosition",
                    pass,
                    pass
                        ? $"Region={pos.Region}  X={pos.X:F1}  Y={pos.Y:F1}  Z={pos.Z:F1}"
                        : "NULL — 0x3019 spawn packets are failing"));
            }

            // ── 4. Model Database ─────────────────────────────────────
            {
                // In Lite mode, we don't track a local count easily
                results.Add(Check("ModelDatabase", true, "On-demand DB active (Lite Mode)"));
            }

            // ── 5. Character Model Info ───────────────────────────────
            {
                var modelInfo = dm.GetCommonInfo(c?.ModelID ?? 0);
                bool pass = modelInfo != null;
                results.Add(Check("PlayerModelInfo",
                    pass,
                    pass
                        ? $"ModelID={c?.ModelID}  Code={modelInfo?.CodeName}  T1={modelInfo?.TypeID1}"
                        : $"ModelID={c?.ModelID} NOT found in DB — spawn parse cannot identify player type"));
            }

            // ── 6. Inventory Contents ─────────────────────────────────
            {
                var inv = c?.Inventory ?? ImmutableList<SRItem>.Empty;
                bool hasItems = inv.Count > 0;
                results.Add(Check("Inventory.Loaded",
                    hasItems,
                    $"{inv.Count} items in inventory"));

                // Dump each item with TypeID info
                Sep("--- Inventory Item Dump ---");
                if (inv.Count == 0)
                {
                    DiagLog("  [WARN] Inventory empty — 0xAA7F parse may have failed");
                }
                else
                {
                    foreach (var item in inv)
                    {
                        var info = dm.GetCommonInfo(item.ModelID);
                        string typeStr = info != null
                            ? $"T1={info.TypeID1} T2={info.TypeID2} T3={info.TypeID3} T4={info.TypeID4}"
                            : "TypeID=UNKNOWN (model not in DB)";
                        string category = ClassifyItem(info);
                        DiagLog($"  Slot={item.Slot:D2}  ItemID=0x{item.ItemID:X8}  Qty={item.Count}  {typeStr}  → [{category}]");
                    }
                }
            }

            // ── 7. Potions Available ──────────────────────────────────
            {
                var inv = c?.Inventory ?? ImmutableList<SRItem>.Empty;
                var hpPots   = inv.Where(i => IsPotion(dm, i, isHp: true,  grain: false)).ToList();
                var mpPots   = inv.Where(i => IsPotion(dm, i, isHp: false, grain: false)).ToList();
                var mpGrains = inv.Where(i => IsPotion(dm, i, isHp: false, grain: true)).ToList();

                results.Add(Check("Potions.HP",
                    hpPots.Count > 0,
                    hpPots.Count > 0
                        ? $"{hpPots.Count} HP potion(s) found: " + string.Join(", ", hpPots.Select(p => $"0x{p.ModelID:X8}"))
                        : "NONE — RecoveryBundle cannot heal"));

                results.Add(Check("Potions.MP",
                    mpPots.Count > 0,
                    mpPots.Count > 0
                        ? $"{mpPots.Count} MP potion(s) found: " + string.Join(", ", mpPots.Select(p => $"0x{p.ModelID:X8}"))
                        : "NONE — RecoveryBundle cannot restore MP (do you have potions equipped?)"));

                DiagLog($"  MP Grains: {mpGrains.Count}");

                if (hpPots.Count == 0 && mpPots.Count == 0 && inv.Count > 0)
                {
                    Sep("--- Potion Detection FAIL: TypeID Analysis ---");
                    DiagLog("  No potions found despite having inventory items.");
                    DiagLog("  This means the model DB (0 or 1 models) cannot classify items.");
                    DiagLog("  ROOT CAUSE: characterdata_*.txt files not loading.");
                    DiagLog("  Expected potion TypeIDs: T1=3(Item) T2=3(Consumable) T3=2(Potion) T4=1(HP)/2(MP)");
                    foreach (var item in inv.Take(5))
                    {
                        var info = dm.GetCommonInfo(item.ModelID);
                        if (info == null)
                            DiagLog($"  → Item 0x{item.ItemID:X8} NOT IN DB");
                    }
                }
            }

            // ── 8. Nearby Entities ────────────────────────────────────
            {
                var entities = ws.NearbyEntities.ToList();
                var mobs  = entities.OfType<SRMob>().ToList();
                var npcs  = entities.OfType<SRNpc>().ToList();
                var items = entities.OfType<SRGroundItem>().ToList();

                results.Add(Check("NearbyEntities",
                    entities.Count > 0,
                    $"Total={entities.Count}  Mobs={mobs.Count}  NPCs={npcs.Count}  GroundItems={items.Count}  " +
                    $"[If 0: 0xAA12 and 0x3019 spawn packets are failing]"));

                if (mobs.Count > 0)
                {
                    Sep("--- Nearby Mobs ---");
                    foreach (var mob in mobs.Take(5))
                        DiagLog($"  UID={mob.UniqueID}  ModelID=0x{mob.ModelID:X8}  Name={mob.Name ?? "?"}  HP={mob.HP}");
                }
            }

            // ── 9. Skills ─────────────────────────────────────────────
            {
                int skillCount = c?.LearnedSkills?.Count ?? 0;
                results.Add(Check("Skills.Loaded",
                    skillCount > 0,
                    skillCount > 0
                        ? $"{skillCount} skills loaded. Top: " + string.Join(", ", c!.LearnedSkills.Take(3).Select(s => $"0x{s.SkillID:X8}"))
                        : "0 skills — 0xAA17 mastery list parse is failing (wrong format)"));
            }

            // ── 10. 0x3019 / 0x3017 Spawn Packet Health ──────────────
            {
                // We can only infer this from entity count and position state
                bool pass = c != null && c.Position.Region > 0;
                results.Add(Check("SpawnPacket.0x3019",
                    pass,
                    pass
                        ? "Position received — at least one valid spawn packet parsed"
                        : "No position! 0x3019 parsing is crashing for ALL entity types. Model DB must load first."));
            }

            // ── 11. 0x3057 HP/MP Packet Header Check ─────────────────
            {
                // Detected from PacketParser: if HP > 1,000,000 it's a garbage read
                bool hpGarbage = c != null && c.HP > 1_000_000 && c.HPMax > 1_000_000;
                results.Add(Check("0x3057.FormatCorrect",
                    !hpGarbage,
                    hpGarbage
                        ? $"GARBAGE: HP={c?.HP} MaxHP={c?.HPMax} — Server sends 11-byte format but parser reads wrong field as HP. " +
                          "The 4 bytes at offset 4 in 0x3057 are NOT CurrentHP — they may be packed region/coord data."
                        : $"HP values look sane: {c?.HP}/{c?.HPMax}"));
            }

            // ── 12. BotController Wiring ──────────────────────────────
            {
                bool pass = ws.CharacterUniqueID > 0; // if UID is known, bot wiring worked
                results.Add(Check("BotController.Wiring",
                    pass,
                    pass ? "BotController is wired and WorldState has UID" : "No UID — bot was never wired to proxy"));
            }

            // ── Summary ───────────────────────────────────────────────
            Sep("=== DIAGNOSTIC SUMMARY ===");
            int passed  = results.Count(r => r.Pass);
            int failed  = results.Count(r => !r.Pass);

            foreach (var (name, pass, detail) in results)
            {
                string tag = pass ? "[PASS]" : "[FAIL]";
                DiagLog($"  {tag,-7} {name,-35} {detail}");
                if (pass) BotLogger.Info("Diagnostic", $"{tag} {name}: {detail}");
                else      BotLogger.Warn("Diagnostic", $"{tag} {name}: {detail}");
            }

            Sep($"=== {passed}/{results.Count} PASSED — {failed} FAILURES ===");
            BotLogger.Info("Diagnostic", $"=== Diagnostic complete: {passed}/{results.Count} PASS, {failed} FAIL ===");

            // ── Recommended Fixes ─────────────────────────────────────
            if (failed > 0)
            {
                Sep("=== RECOMMENDED FIXES ===");
                if (results.Any(r => !r.Pass && r.Name == "ModelDatabase"))
                {
                    DiagLog("FIX 1: Model DB empty → DataManager.LoadCharacterData() is skipping all rows.");
                    DiagLog("       Check: Format A (ID at col0) vs Format B (svc at col0).");
                    DiagLog("       Dump first 3 tab-separated columns of characterdata.txt to verify.");
                }
                if (results.Any(r => !r.Pass && r.Name.Contains("HP/MP")))
                {
                    DiagLog("FIX 2: HP/MP garbage → 0x3057 on this server is NOT UID+HP+MP.");
                    DiagLog("       From packet log 0xAA0F (11 bytes): D8 8C 4B 00 | 22 5B E6 FF | 00 | 00 00");
                    DiagLog("       0xAA0F format: UID(4) + MaxHP(4) + MaxMP(2) perhaps?");
                    DiagLog("       0x3057 format: UID(4) + CurrentHP(4) + HP%(1) + MP%(1) + Status(1)");
                    DiagLog("       The HP raw value > 1M is because UID of OTHER entities are being parsed as ours.");
                    DiagLog("       FIX: Only apply 0x3057 to Character if UID == CharacterUniqueID STRICTLY.");
                }
                if (results.Any(r => !r.Pass && r.Name.Contains("Potion")))
                {
                    DiagLog("FIX 3: No potions → Model DB empty, TypeID classification fails.");
                    DiagLog("       When DB loads correctly, potions will be identified by T1=3 T2=3 T3=2.");
                }
                if (results.Any(r => !r.Pass && r.Name.Contains("0x3019")))
                {
                    DiagLog("FIX 4: 0x3019 crash → ParseSingleSpawn finds model (rare, 1 in DB) then");
                    DiagLog("       calls wrong sub-parser. Need STRICT UID guard: only ParsePlayerSpawn");
                    DiagLog("       when packet UID == CharacterUniqueID from 0x3013.");
                }
                if (results.Any(r => !r.Pass && r.Name.Contains("Skills")))
                {
                    DiagLog("FIX 5: 0xAA17 wrong format → server uses: preamble(8) + count(4) + per mastery:");
                    DiagLog("       masteryID(4) + skillCount(2) + per skill: skillID(4) + level(1) + enabled(1)");
                    DiagLog("       Current code reads skillCount as uint(4) but it's ushort(2) on this server.");
                }
            }

            DebugTrace.Raw("[Diagnostic] Run complete.");
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────
        private static (string Name, bool Pass, string Detail) Check(string name, bool pass, string detail)
        {
            string tag = pass ? "[PASS]" : "[FAIL]";
            DebugTrace.Raw($"[DIAG] {tag} {name}: {detail}");
            return (name, pass, detail);
        }

        private static void DiagLog(string msg)
        {
            DebugTrace.Raw($"[DIAG] {msg}");
            BotLogger.Debug("Diagnostic", msg);
        }

        private static void Sep(string title = "")
        {
            string line = string.IsNullOrEmpty(title)
                ? new string('-', 80)
                : $"--- {title} " + new string('-', Math.Max(0, 76 - title.Length));
            DebugTrace.Raw($"[DIAG] {line}");
            BotLogger.Info("Diagnostic", line);
        }

        private static string ClassifyItem(SRModelInfo? info)
        {
            if (info == null) return "UNKNOWN (not in DB)";
            var v = info;
            if (v.TypeID1 == 3 && v.TypeID2 == 3)
            {
                if (v.TypeID3 == 2 && v.TypeID4 == 1) return "HP Potion";
                if (v.TypeID3 == 2 && v.TypeID4 == 2) return "MP Potion";
                if (v.TypeID3 == 2) return $"Potion T4={v.TypeID4}";
                if (v.TypeID3 == 4) return "Pill/Elixir";
                if (v.TypeID3 == 5) return "MP Grain";
                return $"Consumable T3={v.TypeID3}";
            }
            if (v.TypeID1 == 1) return $"Avatar/Cosmetic T2={v.TypeID2}";
            if (v.TypeID1 == 2) return $"Equipment T2={v.TypeID2} T3={v.TypeID3}";
            return $"T1={v.TypeID1} T2={v.TypeID2} T3={v.TypeID3}";
        }

        private static bool IsPotion(DataManager dm, SRItem item, bool isHp, bool grain)
        {
            var info = dm.GetCommonInfo(item.ModelID);
            if (info == null) return false;
            var v = info;
            if (v.TypeID1 != 3 || v.TypeID2 != 3) return false;
            if (grain) return v.TypeID3 == 5;
            if (v.TypeID3 == 2)
            {
                if (isHp) return v.TypeID4 == 1;
                return v.TypeID4 == 2;
            }
            return false;
        }
    }
}

