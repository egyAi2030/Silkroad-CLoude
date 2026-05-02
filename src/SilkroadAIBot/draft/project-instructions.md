<!-- template-version: 2 -->
# SilkroadAIBot Project Instructions

## Core Principles

### I. Protocol Integrity & Safety
All packet parsers and senders MUST handle format variations (e.g., Rexall vs. standard vSRO) gracefully. Use size detection and safe-search approaches to prevent misalignment. Rationale: Silkroad servers often use non-standard protocol offsets; rigid parsing causes bot crashes.

### II. Test-Driven Development (TDD)
TDD is non-negotiable. Write tests for packet logic, bundle state transitions, and targeting algorithms BEFORE implementation. Rationale: High-complexity automation requires empirical proof of correctness to avoid character death or bans.

### III. Spec-Driven Development (SDD)
Follow the strict `Specify → Clarify → Plan → Tasks → Implement → QC` lifecycle for all features. Rationale: Complex state machines in botting require rigorous planning to avoid race conditions between bundles.

### IV. Observable State Transitions
Use `BotLogger` and `DebugTrace` for all world state changes and network events. Rationale: Debugging real-time network interactions is impossible without high-fidelity logs of what the bot "saw" and "decided."

### V. Agent Output Style
All agent output MUST be concise and outcome-oriented. This principle supersedes any verbose defaults.
- **Progress reports**: Facts and outcomes only — no narration.
- **Artifacts**: Emit required sections only.
- **Errors**: Problem → attempted fix → result.

## Technology Stack

- **Language/Runtime**: C# 12 / .NET 8.0-windows (x86)
- **Frameworks**: Windows Forms, metaswarm orchestration
- **Storage**: SQLite (Microsoft.Data.Sqlite 9.0)
- **Infrastructure**: Win32, SecurityAPI (Blowfish/Silkroad Security)

## Testing & Quality Policy

- **Coverage Target**: 80% (Core/Networking), 60% (Bot/UI)
- **Required QC Categories**: linting, static analysis, coverage
- **Test Strategy**: Unit tests for logic; Integration for packet flows; TDD mandatory
- **Linting / Formatting**: Roslyn Analyzers, nullable enable, standard C# conventions

## Source Code Layout

- **Policy**: PRESERVE_EXISTING_LAYOUT
- **Convention**: Core logic under `/Core`, Networking under `/Networking`, Bot logic under `/Bot`, Models under `/Models`.

## Development Workflow

- **Branching**: Feature branches from main, squash merge
- **Commit Convention**: Conventional Commits
- **CI Requirements**: `dotnet test` must pass; coverage thresholds met

## Governance

- Project instructions supersede all other documentation and practices.
- Amendments require a version bump with ISO-dated changelog entry.
- All implementations MUST pass the Instructions Check gate during planning.

**Version**: 1.0.0 | **Last Amended**: 2026-04-23
