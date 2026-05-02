# SilkroadAIBot - Complete Project Documentation & Codebase

## 1. Project Overview
- **Project Name**: SilkroadAIBot
- **Description**: A modular, high-performance, and AI-ready Silkroad Online bot built with .NET 8.0. It utilizes a custom proxy-based man-in-the-middle architecture to intercept and manipulate game traffic without modifying the game client’s core logic.
- **Main Goals**: 
    - Provide a robust framework for automated hunting (grinding).
    - Implement human-like social intelligence using LLM-based agents.
    - Offer a game-like UI for easy configuration of skills and combat logic.
    - Ensure stability and performance in multi-client environments.
- **Current Version**: v1.3.0 (Integration Release)
- **Tech Stack**:
    - **Frontend**: Windows Forms (.NET 8.0) with custom dark themes.
    - **Backend**: C# (.NET 8.0), C++ (VC++ 2022).
    - **Database**: SQLite (via Microsoft.Data.Sqlite) for character and setting persistence.
    - **Networking**: Custom TCP Proxy with SecurityAPI implementation (Blowfish/CRC).
    - **Tools**: BouncyCastle (Cryptography), SRO.PK2API (Internal).

## 2. Project Structure & Organization
The SilkroadAIBot is organized into distinct logical layers to separate networking, data management, and AI logic. A detailed file-by-file breakdown can be found in the **[Project Structure Glossary](file:///d:/Anti/PROJECT_STRUCTURE_GLOSSARY.md)**.

### Core Architecture Layers:
- **[Bot/](file:///d:/Anti/src/SilkroadAIBot/Bot/)**: Contains the AI Brain, automated logic bundles (Attack, Recovery, Loot), and the Centralized World State.
- **[Networking/](file:///d:/Anti/src/SilkroadAIBot/Networking/)**: Handles packet parsing, encoding, and the Silkroad SecurityAPI (Handshakes/Blowfish).
- **[Data/](file:///d:/Anti/src/SilkroadAIBot/Data/)**: manages PK2 extraction, SQLite encyclopedia caching, and NavMesh navigation data.
- **[Proxy/](file:///d:/Anti/src/SilkroadAIBot/Proxy/)**: Implements the MITM (Man-in-the-Middle) TCP redirection logic.
- **[Models/](file:///d:/Anti/src/SilkroadAIBot/Models/)**: Defines the shared data entities (Character, Monster, Coordinate system).

```text
SilkroadAIBot/
├── README.md                          # Project overview and status
├── SilkroadAIBot.sln                  # Visual Studio Solution
├── src/                               # Source Code root
│   ├── SilkroadAIBot/                 # Main Application Project
│   │   ├── Bot/                       # Core Bot Logic (Brains & Bundles)
│   │   ├── Core/                      # Shared Utilities & Opcodes
│   │   ├── Data/                      # PK2, Database, and NavMesh
│   │   ├── Models/                    # Entity Definitions
│   │   ├── Networking/                # Packet Handling & SecurityAPI
│   │   ├── Proxy/                     # MITM Redirection Logic
│   │   ├── UI/                        # Windows Forms dashboard
│   ├── SilkroadAIBot.Contract/        # Shared Interfaces
│   ├── RedirectorDLL/                 # C++ socket redirection hook (MinHook)
│   ├── xBotLoader/                    # C++ Client Injector & Loader
├── resources/                         # Reference material and tools
└── logs/                              # Auto-generated bot logs
```

## 3. Architecture & Design Decisions
- **Bundle-Based Logic**: The bot logic is split into "Bundles" (e.g., `RecoveryBundle`, `AttackBundle`). Each bundle operates independently on a shared `WorldState` and `ClientlessConnection`. This allows for high modularity and easy extension.
- **Proxy Man-in-the-Middle**: Instead of memory reading or packet injection into the client process, the bot acts as a proxy. The `RedirectorDLL` hooks `connect()` in `sro_client.exe` and points it to the bot's local port.
- **Thread Safety**: `WorldState` uses `ConcurrentDictionary` for entities and `BotLogger` uses a `BlockingCollection` with a background worker thread to ensure the UI remains responsive even under heavy packet traffic.
- **Data-Driven**: Game data (skills, items, mastery) is loaded from the client's `Data.pk2` via `SRO.PK2API` for 100% accuracy relative to the server version.

## 4. Features & Functionality
- **Automated Recovery**: HP/MP/Pill management with threshold-based triggers.
- **Intelligent Combat**: Configurable attack sequences, buff priority, and target selection.
- **Loot Management**: Detailed item filters and pet-pickup integration.
- **A* Pathfinding**: Real-time obstacle avoidance using native NavMesh data extracted from `Map.pk2`.
- **Social Agent**: AI-powered chat responses and "human-like" behavior patterns.
- **Real-Time Packet Sniffer**: Integrated tool for developers to analyze C->S and S->C traffic on the fly.

## 5. Full Source Code (Core Files)

### 5.1 Project Configuration & Entry
#### [src/SilkroadAIBot/SilkroadAIBot.csproj](file:///d:/Anti/src/SilkroadAIBot/SilkroadAIBot.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <PlatformTarget>x86</PlatformTarget>
    <ApplicationIcon />
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BouncyCastle.Cryptography" Version="2.3.0" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.2" />
    <PackageReference Include="System.Text.Encoding.CodePages" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\RedirectorDLL\RedirectorDLL.vcxproj" />
    <ProjectReference Include="..\SRO.PK2API\SRO.PK2API\SRO.PK2API.csproj" />
  </ItemGroup>
</Project>
```

#### [src/SilkroadAIBot/Program.cs](file:///d:/Anti/src/SilkroadAIBot/Program.cs)
```csharp
using SilkroadAIBot.UI;
using System.Text;

namespace SilkroadAIBot
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Register CodePages for Silkroad's EUC-KR encoding support
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
```

### 5.2 Core Bot Logic
#### [src/SilkroadAIBot/Bot/BotController.cs](file:///d:/Anti/src/SilkroadAIBot/Bot/BotController.cs)
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SilkroadAIBot.Bot.Bundles;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Core.Settings;
using System.Linq;

namespace SilkroadAIBot.Bot
{
    public class BotController
    {
        private WorldState _worldState;
        private ClientlessConnection _connection;
        private SilkroadAIBot.Data.DatabaseManager _db;
        private List<IBundle> _bundles;
        private bool _isRunning;
        private DateTime _lastSaveTime;

        public bool IsRunning => _isRunning;

        public BotController(WorldState worldState, ClientlessConnection connection, SilkroadAIBot.Data.DatabaseManager db)
        {
            _worldState = worldState;
            _connection = connection;
            _db = db;
            _bundles = new List<IBundle>();
            _lastSaveTime = DateTime.Now;
        }

        public void AddBundle(IBundle bundle)
        {
            _bundles.Add(bundle);
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            BotLogger.Info("BotController", "Starting Bot Controller...");
            foreach (var bundle in _bundles) bundle.Start();
            _ = BotLoop();
        }

        public void Stop()
        {
            _isRunning = false;
            foreach (var bundle in _bundles) bundle.Stop();
        }

        private async Task BotLoop()
        {
            while (_isRunning)
            {
                try
                {
                    foreach (var bundle in _bundles)
                    {
                        await bundle.UpdateAsync(_worldState);
                    }

                    if ((DateTime.Now - _lastSaveTime).TotalMinutes >= 5)
                    {
                        await Task.Run(() => _db.UpdateCharacterState(_worldState.Character));
                        _lastSaveTime = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    BotLogger.Error("BotController", "Error in main BotLoop", ex);
                }
                await Task.Delay(100);
            }
        }
    }
}
```

#### [src/SilkroadAIBot/Bot/WorldState.cs](file:///d:/Anti/src/SilkroadAIBot/Bot/WorldState.cs)
```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SilkroadAIBot.Models;

namespace SilkroadAIBot.Bot
{
    public class WorldState
    {
        private ConcurrentDictionary<uint, SREntity> _entities = new ConcurrentDictionary<uint, SREntity>();
        public IEnumerable<SREntity> NearbyEntities => _entities.Values;

        private SRCharacter _character = new SRCharacter();
        public SRCharacter Character 
        { 
            get => _character; 
            set { _character = value; OnCharacterUpdated?.Invoke(); } 
        }
        
        public uint CurrentTargetID { get; set; }
        public TrainingArea TrainingArea { get; set; } = new TrainingArea();
        public uint CharacterUniqueID { get; set; } = 0;

        public event Action? OnCharacterUpdated, OnPositionUpdated, OnStatsUpdated;

        public void SpawnEntity(SREntity entity)
        {
            if (entity == null) return;
            _entities.AddOrUpdate(entity.UniqueID, entity, (key, oldValue) => entity);
        }

        public void DespawnEntity(uint uniqueID) => _entities.TryRemove(uniqueID, out _);
        public SREntity? GetEntity(uint uniqueID) { _entities.TryGetValue(uniqueID, out var entity); return entity; }
        public IEnumerable<T> GetEntities<T>() where T : SREntity => _entities.Values.OfType<T>();
    }
}
```

### 5.3 Networking & Security
#### [src/SilkroadAIBot/Networking/Opcodes.cs](file:///d:/Anti/src/SilkroadAIBot/Networking/Opcodes.cs)
```csharp
namespace SilkroadAIBot.Networking
{
    public static class Opcode
    {
        // Gateway Server
        public const ushort GATEWAY_CONNECTION = 0x2001;
        public const ushort GATEWAY_PATCH_CHECK = 0x6100;
        public const ushort GATEWAY_LOGIN_REQUEST = 0x6102;
        public const ushort GATEWAY_SERVER_LIST = 0xA101;
        public const ushort GATEWAY_LOGIN_RESPONSE = 0xA102;

        // Agent Server
        public const ushort AGENT_CHAR_SELECT_REQUEST = 0x7001;
        public const ushort AGENT_CHAR_SELECT_RESPONSE = 0xB001;
        public const ushort AGENT_GAME_JOIN = 0x3012;
        
        // Character Actions
        public const ushort ACTION_WALK = 0x7021;
        public const ushort ACTION_SKILL_CAST = 0x7074;
        public const ushort ACTION_ITEM_USE = 0x704C;
        public const ushort ACTION_PICKUP = 0x7074; // Action identifier differentiates
        
        // Info Opcodes
        public const ushort INFO_CHAR_DATA = 0x3013;
        public const ushort INFO_ENTITY_SPAWN = 0x3015;
        public const ushort INFO_ENTITY_DESPAWN = 0x3016;
        public const ushort INFO_ENTITY_MOVE = 0x30D1;
        public const ushort INFO_STATS_UPDATE = 0x303D;

        public static string GetName(ushort opcode)
        {
            return opcode switch {
                0x3013 => "Character Info",
                0x3015 => "Entity Spawn",
                0x7021 => "Movement",
                0x7074 => "Skill/Action",
                _ => $"Unknown (0x{opcode:X4})"
            };
        }
    }
}
```

### 5.4 Proxy & MITM
#### [src/RedirectorDLL/dllmain.cpp](file:///d:/Anti/src/RedirectorDLL/dllmain.cpp)
```cpp
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <stdio.h>
#include <process.h>

#pragma comment(lib, "ws2_32.lib")

// Hook targets
void* connect_addr = nullptr;
BYTE connect_orig[5];
int target_port = 15779;
int proxy_port = 31411;

void PlaceHook(void* addr, void* hook, BYTE* orig) {
    DWORD old;
    VirtualProtect(addr, 5, PAGE_EXECUTE_READWRITE, &old);
    memcpy(orig, addr, 5);
    DWORD rel = ((DWORD)hook - (DWORD)addr) - 5;
    *(BYTE*)addr = 0xE9;
    *(DWORD*)((BYTE*)addr + 1) = rel;
    VirtualProtect(addr, 5, old, &old);
}

int WSAAPI hooked_connect(SOCKET s, const struct sockaddr* name, int namelen) {
    if (name->sa_family == AF_INET) {
        struct sockaddr_in* addr = (struct sockaddr_in*)name;
        if (ntohs(addr->sin_port) == target_port) {
            addr->sin_port = htons(proxy_port);
            inet_pton(AF_INET, "127.0.0.1", &(addr->sin_addr));
        }
    }
    // Transparent call
    DWORD old;
    VirtualProtect(connect_addr, 5, PAGE_EXECUTE_READWRITE, &old);
    memcpy(connect_addr, connect_orig, 5);
    int res = connect(s, name, namelen);
    PlaceHook(connect_addr, hooked_connect, connect_orig);
    return res;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    if (ul_reason_for_call == DLL_PROCESS_ATTACH) {
        HMODULE hWs2 = GetModuleHandleA("ws2_32.dll");
        connect_addr = (void*)GetProcAddress(hWs2, "connect");
        if (connect_addr) PlaceHook(connect_addr, hooked_connect, connect_orig);
    }
    return TRUE;
}
```

### 5.5 UI Components
#### [src/SilkroadAIBot/UI/MainForm.cs](file:///d:/Anti/src/SilkroadAIBot/UI/MainForm.cs)
```csharp
using System;
using System.Windows.Forms;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.UI
{
    public partial class MainForm : Form
    {
        private WorldState _worldState = new WorldState();
        private BotController? _bot;
        private Panel _contentPanel = null!;

        public MainForm()
        {
            this.Text = "Silkroad AI Bot v1.3.0";
            this.Size = new System.Drawing.Size(1000, 700);
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            _contentPanel = new Panel { Dock = DockStyle.Fill };
            this.Controls.Add(_contentPanel);
            
            Button btnStart = new Button { Text = "Start Bot", Location = new System.Drawing.Point(10, 10) };
            btnStart.Click += (s, e) => {
                // Initialize modules and start loop
                BotLogger.Info("UI", "Bot Start Triggered.");
            };
            _contentPanel.Controls.Add(btnStart);
        }
    }
}
```

### 5.6 Project Solution
#### [SilkroadAIBot.sln](file:///d:/Anti/SilkroadAIBot.sln)
```text
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SilkroadAIBot", "src\SilkroadAIBot\SilkroadAIBot.csproj", "{GUID1}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SilkroadAIBot.Contract", "src\SilkroadAIBot.Contract\SilkroadAIBot.Contract.csproj", "{GUID2}"
EndProject
Project("{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}") = "RedirectorDLL", "src\RedirectorDLL\RedirectorDLL.vcxproj", "{GUID3}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|x86 = Debug|x86
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{GUID1}.Debug|x86.ActiveCfg = Debug|x86
		{GUID1}.Debug|x86.Build.0 = Debug|x86
		{GUID1}.Release|x86.ActiveCfg = Release|x86
		{GUID1}.Release|x86.Build.0 = Release|x86
	EndGlobalSection
EndGlobal
```

## 6. Build and Deployment
- **Requirements**: .NET 8.0 SDK, Visual Studio 2022 (with C++ Desktop development workload).
- **Configuration**: Must be built as **x86** to be compatible with the sro_client.exe.
- **Deployment**:
    1. Build the solution.
    2. Place `RedirectorDLL.dll` in the same folder as `SilkroadAIBot.exe`.
    3. Run `SilkroadAIBot.exe`, select game path, and launch the client.

---
*Documentation generated automatically by the Antigravity Technical Documentation Agent.*
---
