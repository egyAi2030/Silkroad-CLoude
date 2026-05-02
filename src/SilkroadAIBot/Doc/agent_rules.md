# Agent Operating Rules - SilkroadAIBot

Any AI agent working in this directory MUST follow these rules strictly:

## 1. Build Requirements
- **Always Rebuild**: After any modification to the source code, the agent must attempt a full rebuild to verify syntax and logic integrity.
- **Clean Builds**: Before building, the `bin` and `obj` folders should be cleared to prevent stale assembly issues.
- **Version Tracking**: Every successful feature update must increment the version number in `MainForm.cs` and `SilkroadAIBot.csproj`.

## 2. Change Management
- **Change Reports**: For every significant update, a report must be generated in the `Doc\Reports` folder.
- **Code Snippets**: Reports must include snippets of the most critical changes.
- **Metadata**: Include the date, version number, and objective of the change.

## 3. UI/UX Standards
- **Premium Aesthetics**: All UI changes must follow the "Premium Dark" theme defined in `ThemeColors.cs`.
- **Phbot/Sbot Parity**: Logic and layout should strive for parity with professional Silkroad tools.

## 4. Error Handling
- **No Silencing**: Never catch exceptions without logging them via `BotLogger` or `CrashReporter`.
- **Verification**: Use the `BotDiagnostic` tool to verify data persistence after changes to the database layer.
