# SilkroadAIBot: Final Product Requirements & Design Specification

| Attribute | Details |
| :--- | :--- |
| **Product** | SilkroadAIBot — AI-Powered Game Automation Platform |
| **Version** | v2.0 Final (Target Release) |
| **Architecture** | MITM Proxy + MCP Orchestration + LLM Decision Layer |
| **Platform** | .NET 8.0 / Windows x86 / C++ VC++ 2022 |
| **Server Target** | RexallSRO (vSRO-Plus 1.188, Cap 110, Locale 0x16) |
| **Classification** | Private — Internal Development Reference |
| **Date** | April 27, 2026 |
| **Author** | Antigravity Technical Documentation Agent |

---

## Phase 1: Overview & Architecture

### 1. Executive Summary
SilkroadAIBot is a high-performance, AI-orchestrated automation platform for Silkroad Online private servers. It operates as a transparent man-in-the-middle proxy between the game client and server, intercepting, parsing, and acting on all game traffic without modifying the game client binary.

The final product is not a simple bot script. It is a layered intelligence system composed of three tiers:

| Tier | Component | Role |
| :--- | :--- | :--- |
| 1 — Network | MITM Proxy Engine | Intercepts and relays all game packets with full encryption support |
| 2 — State | World State & Parsers | Maintains a real-time model of the game world from packet data |
| 3 — Intelligence | MCP Orchestration + LLM | Makes high-level decisions using AI agents and tool calls |

### 2. System Architecture

#### 2.1 High-Level Architecture
The system follows a strict layered architecture. Each layer has a single responsibility and communicates only with adjacent layers through well-defined interfaces.

| Layer | Technology | Responsibility |
| :--- | :--- | :--- |
| Game Client | sro_client.exe (x86) | Renders the game, sends/receives raw TCP packets |
| RedirectorDLL | C++ / MinHook / VC++ 2022 | Hooks connect() in the client process, redirects to proxy port |
| Proxy Engine | C# / .NET 8.0 TCP | Full MITM: Blowfish, handshake, CRC, packet relay, locale patching |
| Packet Layer | C# SecurityAPI | Decrypts, parses, and re-encrypts all game packets |
| World State | C# ConcurrentDictionary | Real-time entity/character/map model updated by packet parsers |
| Bot Logic | C# Bundle System | Modular behavior bundles (Attack, Recovery, Loot, Navigation) |
| MCP Server | C# / ASP.NET Core | Exposes WorldState and bot actions as MCP tools over HTTP/SSE |
| AI Orchestrator | Anthropic Claude API | LLM agent that reads game state and issues bot commands via MCP |
| Dashboard UI | Windows Forms / .NET 8.0 | Real-time display of bot status, packet sniffer, map view |

#### 2.2 MITM Proxy Architecture
The proxy engine is the foundation of the entire system. It operates in two connection legs per session:
* **Leg 1 (Bot-as-Server):** The bot presents itself to the game client using a Blowfish-encrypted handshake, acting as a trusted game server. Seeds, CRC, and count bytes are synchronized from the real server.
* **Leg 2 (Bot-as-Client):** The bot maintains a separate connection to the real game server, forwarding client packets after inspection and optional patching.
* **Security Context Reset:** On Gateway→Agent server transition, the security context is fully reset (v1.6.6). A new Blowfish key, new seedCount, and new CRC seed are established from the Agent's 0x5000 handshake packet.
* **Token Reconciliation:** The vSRO-Plus 0xF000 gateway token is captured from the Gateway leg and re-injected into the Agent leg automatically.
* **Locale Enforcement:** 0x6100 and 0x6102 packets are patched from locale 0x00 to 0x16 on the fly. The 0x6103 Agent auth packet has byte[25] set to 0x16 as required by the server.
* **Keepalive Loop:** A proxy-side 0x2002 keepalive is sent every 5 seconds on the Agent leg, independent of client activity. This prevents server timeout during character selection.

#### 2.3 MCP Orchestration Architecture
The MCP (Model Context Protocol) server is the AI brain of the system. It exposes the bot's capabilities as structured tools that a Claude LLM agent can call in a ReAct loop. The orchestrator reads game state, reasons about the situation, and issues commands — exactly as a human player would.

| MCP Tool | Input Parameters | Description |
| :--- | :--- | :--- |
| `get_world_state` | — | Returns full WorldState snapshot: character HP/MP/pos, all nearby entities, active buffs, inventory |
| `get_nearby_entities` | type_filter, range | Returns filtered list of nearby mobs/NPCs/items with UID, ModelID, HP, position |
| `get_character_stats` | — | Returns character level, HP, MP, exp, skills, equipment |
| `move_to` | region, x, y | Issues 0x7021 movement packet. Waits for position confirmation. |
| `cast_skill` | skill_id, target_uid | Issues 0x7074 skill cast on specified target UID |
| `use_item` | slot | Issues 0x704C item use. Used for HP/MP potions. |
| `pick_up_item` | item_uid | Issues pet pickup command or direct pickup for specified ground item |
| `set_training_area` | center_x, center_y, radius | Defines the bot's patrol/grind zone |
| `get_session_log` | last_n_lines | Returns recent bot log entries for agent self-diagnosis |
| `get_inventory` | — | Returns all inventory slots with item name, quantity, rarity |
| `send_chat` | message, channel | Sends a chat message (used by Social Agent) |
| `stop_bot` | reason | Gracefully halts all bot bundles |
| `restart_bot` | — | Restarts all bundles from clean state |

#### 2.4 Data Flow
All data flows in a single direction through the system. The packet is the source of truth — all WorldState is derived exclusively from server packets, never from memory reading.
1.  **Server → Proxy:** Raw encrypted TCP bytes arrive on the Leg 2 socket.
2.  Security.Recv() decrypts with current Blowfish key.
3.  TransferIncoming() returns parsed Packet objects with opcode and payload.
4.  **Proxy → Parsers:** Each packet is dispatched by opcode to PacketParser or WorldStateAnalyzer.
    * 0x3015: ParseSpawn() → SpawnEntity() in WorldState
    * 0x3016: DespawnEntity() from WorldState
    * 0xB021: ParseEntityMovement() → update entity position
    * 0xAA17: ParseSkillList() → WorldState.Character.Skills
    * 0xAA7F: ParseInventory() → WorldState.Character.Inventory
5.  **WorldState → MCP Server:** The MCP server reads WorldState on each tool call. WorldState is thread-safe (ConcurrentDictionary).
6.  **MCP Server → LLM Agent:** Tool results are returned to the Claude agent in structured JSON.
7.  **LLM Agent → MCP Server:** Agent issues tool calls (move_to, cast_skill, etc.).
8.  **MCP Server → PacketSender:** PacketSender.Send*() methods construct and send the appropriate C→S packet.

---

## Phase 2: Core Infrastructure

### 3. Networking & Security Specification

#### 3.1 Packet Security Protocol
SilkroadAIBot implements the full Drew Benton SecurityAPI v1.6.6 with the following verified behaviors:

| Protocol Step | Opcode | Status | Notes |
| :--- | :--- | :--- | :--- |
| Identity exchange | 0x2001 | ✓ Working | SR_Client ↔ GatewayServer/AgentServer |
| Blowfish setup | 0x5000 | ✓ Working | Both legs. Full key negotiation. |
| Guard challenge | 0x2005, 0x6005 | ✓ Working | Instant flush enforced |
| Security challenge | 0xAA01 | ✓ Working | Instant flush enforced |
| Patch check | 0x6100/0xA100 | ✓ Working | Locale patched 0x00→0x16 |
| Server list | 0x6101/0xA101/0xA106 | ✓ Working | PK2 locale lock retained |
| vSRO-Plus token | 0xF000 | ✓ Working | Gateway token captured and re-injected |
| Login + captcha | 0x6102/0xA323/0x6323 | ✓ Working | Silent captcha echo |
| Redirect | 0xA102 | ✓ Working | Gateway teardown before agent connect |
| Agent auth | 0x6103/0xA103 | ✓ Working | Single byte[25]=0x16 patch only |
| Character list | 0x7007/0xB007 | ✓ Working | Client sends natively, proxy relays |
| Character data | 0x34A5/0x3013/0x34A6 | ✓ Working | Chunked assembly |
| Spawn confirm | 0x3012 | ✓ Working | Sent after 0x34A6 |

#### 3.2 Packet Parsers
The following server→client packets are parsed and stored in WorldState:

| Opcode | Name | Parser Status | Key Fields Extracted |
| :--- | :--- | :--- | :--- |
| 0x3015 | Entity Spawn | ✓ Complete | UID, ModelID, Region, X/Y/Z (float), Angle, HasDest, LifeState |
| 0x3016 | Entity Despawn | ✓ Complete | UID removed from WorldState |
| 0xB021 | Entity Movement | ✓ Complete | UID, Region (small/large), X/Y/Z, HasDest, DestX/Y |
| 0x3057 | HP/MP Update | ✓ Complete | EntityUID, HP, MaxHP, MP, MaxMP |
| 0x303D | Character Stats | ✓ Complete | Atk, Def, HP limits |
| 0xAA17 | Skill List | ✓ Complete | SkillID, CodeName, CastTime, Cooldown, MPUsage |
| 0xAA7F | Inventory | ✓ Complete | Slot, ItemID, Name, Quantity, Rarity |
| 0xAA78 | Hotbar | ✓ Complete | 382-byte de-obfuscated hotbar layout |
| 0x305C | XP Gain | ✓ Complete | PlayerUID recovered from this packet |
| 0x3056 | Level Up | ✓ Complete | New level, XP |
| 0x30C9 | Kill Confirmed | ✓ Complete | VictimUID |
| 0xB070 | Skill Cast | ✓ Complete | CasterUID, SkillID, TargetUID |
| 0xB071 | Skill Hit Result | ✓ Complete | Damage, result code |
| 0x34A5/6 | Char Data Begin/End | ✓ Complete | Chunked buffer assembly |
| 0x3013 | Char Data Chunk | ✓ Complete | Appended to assembly buffer |
| 0xB007 | Character List | ✓ Complete | Relayed natively to client |

### 4. Game Data Extraction

#### 4.1 PK2 Data Sources
All game data is extracted from the client's PK2 archive files at startup. Extracted data is cached in SQLite for subsequent runs.

| PK2 File | Internal Path | Data Loaded | Status |
| :--- | :--- | :--- | :--- |
| Media.pk2 | server_dep\silkroad\textdata\textdata_object.txt | 11,714 localized entity names | ✓ Working |
| Media.pk2 | server_dep\silkroad\textdata\characterdata_40000.txt | ~12,000 monsters/NPCs | ✓ Working |
| Media.pk2 | server_dep\silkroad\textdata\characterdata_50000.txt | ~7,000 monsters/NPCs | ✓ Working |
| Media.pk2 | server_dep\silkroad\textdata\itemdata_30000.txt | ~3,000 items | ✓ Working |
| Media.pk2 | server_dep\silkroad\textdata\itemdata_40000.txt | ~8,000 items | ✓ Working |
| Media.pk2 | server_dep\silkroad\textdata\itemdata_50000.txt | ~4,000 items | ✓ Working |
| Media.pk2 | server_dep\silkroad\textdata\itemdata_55000.txt | ~2,000 items | ✓ Working |
| Media.pk2 | server_dep\silkroad\textdata\skilldata_35000enc.txt | ~63,000 skills (XOR-decoded) | ✓ Working |
| Map.pk2 | *.nvm files | NavMesh regions for pathfinding | ✓ Working |

#### 4.2 Data Models
The following models are populated from PK2 extraction and used throughout the bot:

| Model | Key Fields | Source |
| :--- | :--- | :--- |
| SRModelInfo | ID, CodeName, Name, TypeID1-4, Level, Rarity, Rank, MaxDurability, IconPath | characterdata_*.txt + itemdata_*.txt + textdata_object.txt |
| SRSkill | ID, CodeName, Name, SkillType, CastTime, Cooldown, MPUsage, Range, IsSelfOnly, Level, IconPath | skilldata_35000enc.txt |
| SREntity | UniqueID, ModelRefID, Region, X/Y/Z, Angle, LifeState, MotionState, WalkSpeed, RunSpeed | 0x3015 spawn packets |
| SRCharacter | Name, Level, HP, MP, Exp, STR, INT, Skills, Inventory, Position, UniqueID | 0x3013 + 0xAA17 + 0xAA7F |
| SRItem | Slot, RefID, Name, Quantity, Rarity, Plus, IconPath | 0xAA7F inventory packets |
| TrainingArea | CenterX, CenterY, Radius, Region | User configuration / MCP tool |

### 7. Database Schema

#### 7.1 SQLite Tables
All persistent data is stored in a single SQLite database file (`silkroad_bot.db`) in the application directory.

| Table | Purpose | Key Columns |
| :--- | :--- | :--- |
| ref_models | Cached game object reference data | id, code_name, name, type_id1-4, level, rarity, icon_path |
| ref_skills | Cached skill reference data | id, code_name, name, cast_time, cooldown, mp_usage, range |
| characters | Per-character state persistence | name, level, hp, mp, exp, position_x, position_y, region, last_seen |
| settings | Bot configuration | key (TEXT PK), value (TEXT) |
| session_logs | Compressed session history | id, timestamp, level, source, message |
| training_areas | Saved training zone configs | name, region, center_x, center_y, radius, mob_filter |

---

## Phase 3: Bot Intelligence & Orchestration

### 5. Bot Logic & Bundle System

#### 5.1 Bundle Architecture
The bot logic is organized into independent Bundles that each run on a shared WorldState. Bundles are registered in BotController and executed sequentially in the main BotLoop (100ms tick). Each bundle implements IBundle with Start(), Stop(), and UpdateAsync(WorldState) methods.

| Bundle | Trigger Condition | Actions Taken |
| :--- | :--- | :--- |
| RecoveryBundle | HP < threshold OR MP < threshold | SendUseItem(hpPotionSlot) or SendUseItem(mpPotionSlot). Checks every tick. |
| AttackBundle | Alive mob within training area, no current target | SelectTarget → CastBuff → CastAttackSkill in configured sequence |
| LootBundle | Mob kill confirmed (0x30C9), ground items present | Pet pickup command or direct 0x7074 pickup. Item filter applied. |
| NavigationBundle | Character outside training area OR target unreachable | A* path via NavMesh → series of SendMovement() calls |
| SocialBundle | Nearby player sends chat message | LLM agent generates human-like response. Sent via SendChat() |
| PKerBundle | Player enters training area with PK flag | Configurable: flee to safe zone OR stop bot OR alert |
| SupplyBundle | Consumables below minimum threshold | Navigate to NPC, buy configured items, return to grind zone |
| AntiDetectionBundle | Time-based randomization | Random movement jitter, skill delay variance, AFK simulation |

#### 5.2 Combat System
The AttackBundle implements a priority-based skill rotation configured by the user:
* **Target Selection:** Nearest alive mob within training area radius that matches the configured mob whitelist. Priority can be set by ModelID or CodeName.
* **Buff Phase:** Buffs (identified by IsSelfOnly=true in SRSkill) are cast before attacking if their cooldown has expired.
* **Attack Rotation:** Skills are cast in configured priority order. The bot waits CastTime + network latency before casting the next skill.
* **Kill Detection:** 0x30C9 packet confirms the kill. The bot transitions to LootBundle, then re-selects a new target.
* **Death Handling:** 0x3057 HP=0 on player UID triggers resurrection logic. Packet 0x3053 is sent with configured revival type.

#### 5.3 Navigation System
The navigation system uses A* pathfinding on the vSRO NavMesh (.nvm files):
* **NavMesh Loading:** Region-based .nvm files loaded from Map.pk2 at startup. Local Maps/ folder used as fallback for missing regions.
* **Pathfinding:** A* with NavMesh cell adjacency. Each cell has collision flags and yaw data. Path smoothed with waypoint culling.
* **Movement Execution:** SendMovement() sends 0x7021 per waypoint. Bot waits for 0xB021 confirmation before next waypoint.
* **Stuck Detection:** Position delta checked every 3 seconds. If delta < 2 units, bot randomizes next waypoint to escape stuck state.

### 6. MCP Server Specification

#### 6.1 Overview
The MCP (Model Context Protocol) server is implemented as an ASP.NET Core application running alongside the bot. It exposes a Server-Sent Events (SSE) endpoint that Claude agents can connect to via the Anthropic API's `mcp_servers` parameter.

| Transport Details | Value |
| :--- | :--- |
| **Transport** | HTTP/SSE (Server-Sent Events) |
| **Port** | 5001 (configurable) |
| **Endpoint** | `http://localhost:5001/mcp/v1` |
| **Auth** | Bearer token (configurable, optional for local use) |
| **Protocol** | MCP 2024-11 (tool_use / tool_result) |
| **Max tool calls/turn** | 25 (configurable) |
| **State access** | Read: WorldState snapshot per call \| Write: PacketSender methods |

#### 6.2 Tool Definitions
Each MCP tool is defined with a JSON Schema input spec. The LLM agent receives the full schema on connection and uses it to construct valid tool calls.

| Tool Name | Input Schema | Output | Side Effect |
| :--- | :--- | :--- | :--- |
| `get_world_state` | `{}` | Full WorldState JSON: character, entities, position, buffs | None |
| `get_nearby_entities` | `{type?:string, range?:number}` | Array of entity objects with UID, name, HP%, position | None |
| `get_character_stats` | `{}` | HP, MP, Level, Exp, position, active_buffs | None |
| `get_inventory` | `{}` | Array of `{slot, name, quantity, rarity}` | None |
| `get_session_log` | `{lines?:number}` | Last N log entries as string array | None |
| `move_to` | `{region:int, x:float, y:float}` | Success/failure + new position | Sends 0x7021 |
| `cast_skill` | `{skill_id:int, target_uid?:int}` | Result: success/fail/on_cooldown | Sends 0x7074 |
| `use_item` | `{slot:int}` | Result: used/empty/failed | Sends 0x704C |
| `pick_up_item` | `{item_uid:int}` | Result: picked_up/out_of_range/failed | Sends pickup packet |
| `set_training_area` | `{x:float, y:float, radius:float}` | Confirmed new training area | Updates TrainingArea |
| `send_chat` | `{message:string, channel:string}` | Sent/failed | Sends chat packet |
| `stop_bot` | `{reason:string}` | Stopped | Halts all bundles |
| `restart_bot` | `{}` | Restarted | Restarts all bundles |

#### 6.3 Agent Orchestration Loop
The LLM agent operates in a standard ReAct (Reason + Act) loop. On each tick (configurable, default 5 seconds), the agent:
1.  Calls `get_world_state` to observe the current game situation.
2.  Reasons about the optimal next action based on the Strategic Directives Profile (character config).
3.  Issues one or more tool calls (`move_to`, `cast_skill`, `use_item`, etc.).
4.  Observes tool results and updates its reasoning accordingly.
5.  Logs its reasoning to the session log for diagnostic visibility.

#### 6.4 Strategic Directives Profile
Each character has a Strategic Directives Profile — a markdown configuration document injected as the system prompt for the MCP agent. It defines:
* **Combat priorities:** Skill rotation order, buff priority, mob target preferences
* **Survival thresholds:** HP/MP percentages that trigger immediate potion use or retreat
* **PKer response logic:** Flee radius, alert conditions, party member recognition
* **Inventory thresholds:** Minimum potion counts before supply run is triggered
* **Training area:** Default center coordinates, radius, preferred mob types
* **Social persona:** Name, communication style, topics to avoid, language

---

## Phase 4: User Interface & Experience

### 8. User Interface

#### 8.1 Dashboard Panels
| Panel | Contents |
| :--- | :--- |
| Status Bar | Character name, level, HP/MP bars, current target, bot state (Running/Stopped/Error) |
| Map View | Real-time 2D map of current region. Character position, nearby entities, training area circle, movement path. |
| Entity List | Live table of nearby entities: UID, ModelID, Name (from PK2), HP%, distance, type badge |
| Skill Configuration | Drag-and-drop skill rotation builder. Buff/attack categorization. Cooldown display. |
| Inventory Panel | Grid view of all inventory slots. Potion threshold sliders. Item filter configuration. |
| Packet Sniffer | Real-time C→S and S→C packet stream with opcode, size, hex dump. Filter by opcode. |
| Bot Log | Scrolling log panel. Level filter (INFO/WARN/ERROR). Auto-scroll toggle. |
| AI Agent Panel | Live display of MCP agent reasoning. Tool calls and results. Last decision explanation. |
| Settings | Account credentials, game path, proxy port, AI API key, bundle enable/disable toggles |

---

## Phase 5: Build, Deployment & Roadmap

### 9. Known Issues & Remaining Work

#### 9.1 Known Issues (Active)
| ID | Component | Issue | Priority |
| :--- | :--- | :--- | :--- |
| BUG-01 | PacketHandler | 0xB007 double-handler: ParseCharacterListAndSelect() and inline handler both fire. May cause duplicate selection. | High |
| BUG-02 | DataManager | SQLite bulk insert saves only 1 record. Transaction wrapping missing. | High |
| BUG-03 | DataManager | Duplicate PK2 loading: backslash and forward-slash path variants both succeed. Data count doubled. | Medium |
| BUG-04 | ParseSpawn | Non-standard entity types (players, COS/pets) cause EndOfStreamException on remaining fields. | Medium |
| BUG-05 | WorldStateAnalyzer | ParseCharacterSelectionAction reads CharCount as UInt32 → name is always garbage. | High |
| BUG-06 | WorldStateAnalyzer | Name-seeker byte scanner searches for 'Ai_DMG' (buff name, not char name). Must be removed. | High |
| BUG-07 | PacketSender | SendUseItem writes potionType as UShort (extra byte). Server rejects 0x704C. | High |
| BUG-08 | ProxyManager | _sessions list never pruned (only via manual Pruned log). Dead sessions accumulate. | Low |
| BUG-09 | Skills | Character skills always 0 at login. ParseSkillList and ParseCharacterData write to different collections. | Medium |

#### 9.2 Feature Roadmap
| Feature | Status | Target Version |
| :--- | :--- | :--- |
| Full map view with entity overlay | In Progress | v2.0 |
| Full skill rotation configurator UI | In Progress | v2.0 |
| A* pathfinding integration with NavMesh | In Progress | v2.0 |
| MCP Server (ASP.NET Core / SSE) | Planned | v2.1 |
| LLM Agent ReAct Loop (Claude API) | Planned | v2.1 |
| Strategic Directives Profile loader | Planned | v2.1 |
| Full player spawn parser (TypeID1=1) | Planned | v2.1 |
| COS/pet spawn parser (TypeID1=4) | Planned | v2.1 |
| Ground item spawn parser (TypeID1=3) | Planned | v2.1 |
| Supply run (auto NPC buy) | Planned | v2.2 |
| Social Agent (LLM chat responses) | Planned | v2.2 |
| Anti-detection jitter system | Planned | v2.2 |
| PKer response bundle | Planned | v2.2 |
| Multi-account management (multi-proxy) | Planned | v2.3 |

### 10. Build & Deployment

#### 10.1 Build Requirements
| Requirement | Version / Notes |
| :--- | :--- |
| Operating System | Windows 10/11 (x64 host, x86 target) |
| .NET SDK | 8.0 or later |
| Visual Studio | 2022 with 'Desktop development with C++' workload |
| Platform Target | x86 (required for sro_client.exe compatibility) |
| BouncyCastle | v2.3.0 (Blowfish/CRC cryptography) |
| Microsoft.Data.Sqlite | v8.0.2 (SQLite cache) |
| System.Text.Encoding.CodePages | v8.0.0 (EUC-KR/Korean encoding) |

#### 10.2 Deployment Steps
1.  Build solution in Release|x86 configuration.
2.  Place `RedirectorDLL.dll` in the same directory as `SilkroadAIBot.exe`.
3.  Configure settings: game path, credentials, proxy port (default: 15777), AI API key.
4.  On first run: DataManager extracts PK2 data and caches in SQLite (~30 seconds).
5.  Click Start Bot. The loader injects RedirectorDLL and launches sro_client.exe.
6.  Log in through the game client. The proxy handles handshake, locale, token, and auth automatically.
7.  Select character in the game client. Bot activates and begins the configured bundle sequence.
