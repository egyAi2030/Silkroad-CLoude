# Silkroad Bot Manager: Headless Architecture Design

## 1. Overview
The **Headless Bot Manager** is designed to allow the execution of multiple Silkroad bot instances (8-32+) on a single machine by removing the Graphical User Interface (GUI) overhead.

## 2. Core Architecture
*   **Lite Core**: A console-based version of the `SilkroadAIBot` that contains only the Networking, Data, and Pathfinding logic.
*   **Central Manager (GUI)**: A single dashboard that monitors all running "Lite" processes.
*   **IPC (Inter-Process Communication)**: Uses Named Pipes or Local Sockets to send commands from the Manager to the Bots.

## 3. Benefits
*   **CPU Savings**: No UI thread or image rendering.
*   **RAM Savings**: No window handles or heavy UI controls.
*   **Stability**: If one bot's UI thread would have crashed, the headless core remains stable.

## 4. Implementation Steps
1. Create a `SilkroadAIBot.Headless` project.
2. Link existing `Data`, `Networking`, and `Logic` namespaces.
3. Implement a Command-Line Interface (CLI) for login and start/stop.
4. Build the Central Dashboard to spawn and track processes.

---
*Status: Planned for future update.*
