# Silkroad AI Bot - Technical Report (v4.1.6)

## Overview
This report summarizes the major logic improvements and protocol adaptations implemented to support modern private Silkroad servers (REXALL, NOVA, EgY Guard) while maintaining universal compatibility with standard vSRO files.

## 1. Protocol & Security Adaptations

### 1.1 Target Selection Security (Opcode 0xB045)
- **Problem**: Modern guards require a dynamic "Security Hash" to be sent along with target selection and skill cast packets to prevent packet injection.
- **Solution**: Implemented a handler for `SERVER_CHARACTER_SELECTION_RESPONSE (0xB045)`. The bot now captures and stores the `LastSelectionHash` from this packet.
- **Outgoing Packets**: Updated `PacketSender` to append this hash to `0x7045` (Select Target) and `0x7001/0x7074` (Action/Skill) packets when present.

### 1.2 Character List Selection (Opcode 0xB007)
- **Problem**: Servers with custom guards often hide the character list or use modified structures that caused the bot to fail during auto-login.
- **Solution**: Refactored `ParseCharacterSelectionAction` to iterate through the character list and match the user's preferred character name. If no name is set, it defaults to the first available valid character.

### 1.3 Entity Vitality Sync (0x30BF)
- **Problem**: The bot would sometimes attempt to attack dead targets because "ObjectDie" packets were not correctly updating the entity state.
- **Solution**: Added `UpdateEntityLifeState` to `WorldState`. The bot now listens to `SERVER_ENTITY_DIE (0x30BF)` and sets the target's HP to 0 and state to `Dead`, immediately triggering a target search for a new live entity.

## 2. World State & Radar Improvements

### 2.1 Universal Entity Spawning (Heuristics)
- **Problem**: Private servers often use custom `0x3019` formats with "Protection Bytes" or shifted offsets.
- **Solution**: Implemented a heuristic check in `PacketParser` that detects shifted coordinate reads. If a `RegionID` appears invalid (>32000 in open world) and extra data is present, the parser performs a 1-byte skip to realign the stream.
- **Player Identification**: Added logic to distinguish players from NPCs even when custom model IDs are used, by checking for the presence of a character name string in the bionic detail block.

### 2.2 Coordinate Verification (0x3013)
- **Problem**: Standard identity packets (0x3013) were sometimes ignored, leading to the bot not knowing its own position.
- **Solution**: Implemented a pattern-search for `CharacterUniqueID` within the `0x3013` packet stream to ensure the bot's coordinates are always synchronized with the server's authoritative view.

## 3. Database & Management

### 3.1 Game-Specific Databases
- **Problem**: Settings and extracted data from different servers were conflicting.
- **Solution**: Implemented `ChangeDatabase(gameName)` in `DatabaseManager`. The bot now creates a separate `.db` file for each server (e.g., `bot_data_Rexall.db`, `bot_data_Nova.db`), preserving server-specific mappings.

### 3.2 Addon Telemetry
- **Problem**: Many servers use `0xAA` opcodes for custom features (Rankings, DPS meters) which were previously treated as "Unknown".
- **Solution**: Added ~20 custom opcodes from `vSRO-ServerAddon` to the opcode mapping for better logging and debugging visibility.

## 4. Current Status
- **Build**: Successful (0 Errors, 0 Regressions).
- **Radar**: Fully functional, correctly classifying mobs, players, and drops.
- **Combat**: Validated outgoing selection packets with security hashes.

> [!TIP]
> If the bot fails to select a character in a new server, check the `packets_YYYYMMDD.log` for opcode `0xB001` or `0xB007` to see if the server has shifted the character list format.
