This project is a modular, AI-ready Silkroad Online bot built with .NET 8.0. It features a persistent status dashboard, a 15-module configuration system, and a robust A* Pathfinder engine mapped specifically to Silkroad's NavMesh structures.

**Current Version: v1.3.0 (Integration Release)**

## Build Instructions (Visual Studio)

Since this project uses modern .NET 8.0 features, please follow these steps to generate the executable:

1.  **Open the Solution**: Open `src/SilkroadAIBot/SilkroadAIBot.csproj` (or the solution if created) in Visual Studio 2022.
2.  **Restore Packages**: Visual Studio should automatically restore the required NuGet packages and dependencies (including `SRO.PK2API` and `SecurityAPI`).
3.  **Build Configuration**: Set the build configuration to **Release** and architecture to **x64** (or AnyCPU).
4.  **Publish/Build**: Go to `Build -> Build Solution`.
5.  **Output**: The resulting binaries will be located in `bin/Release/net8.0-windows/`.

## Live World-State Test Checklist

Follow these steps to verify the bot logic in a live environment:

1.  **Step 1 — Launch the app**: Run `SilkroadAIBot.exe` from the Release folder.
2.  **Step 2 — Set game directory**: Go to the **Setup & Login** view and select your Silkroad Online installation folder (must contain `sro_client.exe`).
3.  **Step 3 — Configure Connection**: 
    - **Proxy Mode**: Uses the **SBot-style Memory Loader**. No `Hosts` file editing is required.
    - **No-DC**: Check the "Enable No-DC" box to prevent the client from closing on disconnect.
4.  **Step 4 — Enter credentials and connect**: Click **START GAME** to launch the game with memory patches, or **START CLIENTLESS** for terminal mode.
5.  **Step 5 — Open Log tab and confirm INFO messages appear**: Switch to the **Logs** tab. You should see `[INFO] [Loader] Found Gateway IP at... Patching to 127.0.0.1`.
6.  **Step 6 — Enable Safety tab**: Go to **AI Modules -> Safety** and toggle **AutoHP at 80%**.
7.  **Step 7 — Enable Combat tab**: Toggle **Auto-Select Target** and **Auto-Cast Attacks**.
8.  **Step 8 — Start bot**: Click the main **START BOT** button.

## Key Features & AI Integration

*   **BotSettings Singleton**: All bot configuration is stored in `BotSettings.Instance`, providing a clean API for future AI plugin integration.
*   **Thread-Safe Logging**: `BotLogger` handles daily rolling file logs and a color-coded session display.
*   **Modular Architecture**: 12 independent bundles (`Loot`, `Combat`, `Supply`, etc.) run in parallel without blocking.
*   **A* Pathfinding**: Heuristic routing over native `Map.pk2` NavMesh cells.

---

## Changelog

### v1.3.0 (2026-04-14) - Integration Release
*   **Robust Packet Handling**: Implemented centralized `PacketParser` and `PacketSender` with coverage for over 20 critical S→C and C→S opcodes (Stats, Skills, Inventory, Combat, Entities).
*   **Consolidated UI**: Merged "Combat" and "Skills" tabs into a unified **"Skills & Combat"** view for better accessibility.
*   **Hunting Area Overhaul**: Moved Hunting Area settings (Set Center, Radius) to the front-page of the combat module; resolved validation issues.
*   **Automated Recovery**: Fully wired `RecoveryBundle` to use real packets for automated potion usage and resurrection.
*   **Bug Fixes**: Resolved critical 0x3013 (Character Data) assembly issues for large data streams.

### v1.2.0 (Historical) - Framework Release
*   Modular AI bundle architecture.
*   A* Pathfinder implementation.
*   Status dashboard UI.

---

> [!WARNING]
> This is an active development build. Opcode variations may exist across different server locales (vSRO/iSRO).
