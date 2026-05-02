# SilkroadAIBot Session Report — April 23, 2026

## 1. Executive Summary
This session focused on transitioning the SilkroadAIBot from a standalone WinForms application to an **AI-Controllable Headless Engine**. We successfully established a real-time data link between the AI (Gemini) and the game world, allowing for autonomous observation and locomotion.

---

## 2. Key Accomplishments

### ✅ AI Control Interface (REST API)
- Built a custom **ApiServer.cs** running on port `5999`.
- Implemented endpoints for `/status`, `/state`, `/move`, and `/attack`.
- This allows the AI to "see" world data (HP, MP, Position, Entities) and issue commands via PowerShell.

### ✅ Character Identity & Coordinate Fixes
- **Identity**: Resolved the bug where the character name was missing or hardcoded. The bot now correctly identifies the player as `AI_DMG`.
- **Global Coordinates**: Fixed the misalignment where the bot saw `(1047, -63)` while the client showed `(4293, 2541)`. Implemented dynamic offset calculation to align bot and client logic perfectly.

### ✅ Entity & Monster Identification
- Fixed a critical parser bug that mislabeled players as monsters.
- Implemented **Database-First Naming**, allowing the bot to identify mobs like `Demon Horse` and `MOB_TK_BONELORD` by looking up their `ModelID` in the `Media.pk2` data.
- Added **Addon Filtering** to ignore server-specific DPS and UI packets that were causing data corruption.

### ✅ Locomotion Synchronization (IPC)
- Established a **Named Pipe Bridge** (IPC) to synchronize AI actions with the visual game client.
- Confirmed that the AI can successfully move the character autonomously for 20+ steps with the client visually following.

---

## 3. Challenges & Failures

### ❌ Combat Execution (The "Attack" Problem)
- **Problem**: While the AI successfully detects monsters and sends the `0x7074` (Attack) and `0x7045` (Select) packets, the server is not always executing the damage.
- **Root Cause**: In **Proxy Mode**, the server often requires the attack packet to originate from the same sequence as the client's heartbeat. If the timing or `Security` object state is slightly off, the server silently ignores the injected attack packet.
- **Observation**: The bot "thinks" it attacked, but the client does not show the animation or damage.

### ❌ "Zombie" Process Conflicts
- Multiple clean builds were hampered by lingering `SilkroadAIBot.exe` processes locking the `Media.pk2` and `Redirector.dll` files. This required manual task killing.

---

## 4. Unfinished Business (Backlog)
1.  **Attack Packet Timing**: Refine the `PacketSender` to intercept and "piggyback" on the client's next valid sequence to ensure the server accepts injected attacks.
2.  **Autonomous Hunter AI**: Build the higher-level logic to loop through "Search -> Walk -> Kill -> Loot" without manual API calls.
3.  **NPC Interaction**: Implement the `0x7046` dialogue tree to allow the AI to talk to NPCs for supplies.

---

**Report Status**: Completed & Saved
**Bot Version**: v3.9.0 [STABLE]
