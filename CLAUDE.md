# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

All commands should be run from the repository root or by passing the project path explicitly.

```bash
# Build entire solution
dotnet build Stockflow.slnx

# Build specific project
dotnet build Sources/Stockflow.Webserver/

# Run the web server
dotnet run --project Sources/Stockflow.Webserver/

# Run tests (xUnit, when added)
dotnet test
dotnet test --filter "FullyQualifiedName~MyTest"  # single test

# Release build
dotnet build --configuration Release Stockflow.slnx
```

Requires **.NET 10.0**.

## Architecture Overview

Stockflow is a warehouse logistics simulator with two deployment targets: consumer (Steam/Unity) and enterprise (B2B/Docker with WMS integration).

### Project Layout (`Sources/`)

| Project | Type | Role |
|---|---|---|
| `Stockflow.Simulation` | Class library | Pure simulation engine — **zero framework dependencies** |
| `Stockflow.Protocol` | Class library | Shared MessagePack-serializable message types |
| `Stockflow.Webserver` | ASP.NET Core app | WebSocket + REST API host |
| `Stockflow.Persistence` | Class library (planned) | EF Core DbContext and repositories |

Solution file: `Stockflow.slnx` (repository root)

### Simulation Engine internal layout (`Sources/Stockflow.Simulation/`)

| Folder | Contents |
|---|---|
| `Core/` | `SimulationEngine`, `SimulationState`, `StateDelta` |
| `Commands/` | `ICommand`, `CommandResult` |
| `Component/` | `ISimComponent`, `OneWayConveyor`, `Direction`, `Port`, `ComponentType` |
| `Entity/` | `ISimEntity` |
| `Grid/` | `GridManager`, `Cell`, `GridCoord` |
| `Modules/` | `IComponentModule` |
| `Routing/` | `RoutingGraph`, `Connection` |

### Key Architecture Constraints

1. **`Stockflow.Simulation` must stay zero-dependency** — no NuGet packages, no ASP.NET, no EF Core. All state mutations go through the simulation engine; clients are read-only.

2. **`Tick(float deltaTime)` — deltaTime is the caller's responsibility.** The hosted service computes it as `1f / tickRate * engine.TimeScale` and passes it in. The engine does not know its own tick rate.

3. **`SimulationEngine.Grid` vs `SimulationEngine.State`** — `Grid` is internal spatial infrastructure (placement, lookup). `State` is the observable snapshot to serialize and send to clients. Keep them separate.

4. **Commands flow:** `Webserver` translates `ClientMessage` (Protocol) → `ICommand` (Simulation) before calling `engine.ProcessCommand()`. The simulation never depends on the networking layer.

5. **Two communication channels:**
   - WebSocket (port 9600): binary MessagePack deltas at 10–100 Hz tick rate (~2–5 KB/delta)
   - REST API (port 9601): non-real-time operations (config, metrics export, WMS integration)

6. **Database never accessed in the hot path** — metrics are buffered in-memory and flushed asynchronously. EF Core repositories handle persistence; default DB is SQLite, swappable to PostgreSQL.

7. **Plugin system** — components implement `IStockFlowComponent` and are loaded at runtime via `PluginLoader` as DLLs.

### Data Flow

```
Client (Unity / Web / CLI)
    ↕ WebSocket (MessagePack deltas)
Stockflow.Webserver
    → Stockflow.Simulation (tick loop, state machine)
    → Stockflow.Persistence (async metrics/session flush)
    ↔ Stockflow.Protocol (shared message types)
```

### GitHub

Issues tracked at `mcauzzi/Stockflow`. Organised by milestone: F0 (foundations, current), F1A–D, F2, F3.

**Branch workflow:** main is kept up to date via rebase. Always create feature branches from `origin/main` after a `git fetch`:

```bash
git fetch origin main
git checkout -b claude/issue-N-short-desc origin/main
```

### Documentation

Detailed design documents (in Italian) are in `Docs/`:
- `ARCHITECTURE_StockFlow_v0.3.md` — full technical architecture, communication protocol, DB schema
- `GDD_StockFlow_v0.2.md` — game design document (mechanics, entities, missions)
- `SIMULATION_ENGINE_v0.1.md` — current implementation reference for `Stockflow.Simulation`
