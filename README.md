# Stockflow

Warehouse logistics simulator with two deployment targets: a **consumer** game
(Steam / Unity) and an **enterprise** B2B product (Docker, WMS integration).

The repository hosts the **server-side simulation engine**, the **networking
host**, the **shared protocol**, and a **web console** (Angular). The Unity
client lives outside this repo and consumes the published `Stockflow.Protocol`
DLL.

> Detailed design docs (in Italian) live in [`Docs/`](Docs/). Guidance for working
> on the codebase is in [`CLAUDE.md`](CLAUDE.md).

## Requirements

- **.NET 10.0 SDK** — server, simulation, protocol, tests
- **Node.js + npm** — web console (`Sources/Stockflow.Console`, Angular 21)

## Project layout (`Sources/`)

| Project | Type | Role |
|---|---|---|
| `Stockflow.Simulation` | Class library | Pure simulation engine — **zero framework dependencies** |
| `Stockflow.Protocol` | Class library | Shared MessagePack message types |
| `Stockflow.Webserver` | ASP.NET Core | WebSocket + REST host |
| `Stockflow.Persistence` | Class library | EF Core persistence *(planned — currently a stub)* |
| `Stockflow.Console` | Angular app | Web console / debug client |
| `Stockflow.Tests.Simulation` | xUnit | Unit tests for the engine |
| `Stockflow.WsTestClient` | Console app | Manual WebSocket client |

Solution file: [`Stockflow.slnx`](Stockflow.slnx) (repository root).

The engine exposes its observable state as **deltas** streamed to clients over
WebSocket (binary MessagePack); clients are read-only and all state mutations go
through the engine via commands. See
[`Docs/ARCHITECTURE_StockFlow_v0.3.md`](Docs/ARCHITECTURE_StockFlow_v0.3.md).

## Build & run

```bash
# Build the whole solution
dotnet build Stockflow.slnx

# Run the web server (WebSocket :9600, REST :9601)
dotnet run --project Sources/Stockflow.Webserver/

# Run all engine tests
dotnet test Sources/Stockflow.Tests.Simulation/

# Run a single test by name
dotnet test --filter "FullyQualifiedName~MyTest"

# Web console (dev server)
cd Sources/Stockflow.Console && npm install && npm start
```

## Communication channels

- **WebSocket** (`:9600`) — binary MessagePack deltas at a 10–100 Hz tick rate.
- **REST** (`:9601`) — non-real-time operations (config, scenarios, metrics).

## Continuous integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) builds the solution, runs
the xUnit tests and publishes the `Stockflow.Protocol` DLL as an artifact (for
the Unity client) on every push and on pull requests to `main`/`develop`.

## Branch workflow

Feature branches are created from `origin/develop` (with `--no-track`), reviewed
via PR into `develop`, and only then promoted to `main`. Never merge feature
branches directly into `main`. Full details in [`CLAUDE.md`](CLAUDE.md).

## Status

Early development (milestone **F0**). The simulation engine (conveyors, turns,
diverter, merge, package generator/exit, routing graph) and the WebSocket/REST
host are in place; persistence, metrics collection, missions/orders, stacker
cranes and the Unity client are planned. Tracked at
[`mcauzzi/Stockflow`](https://github.com/mcauzzi/stockflow/issues) by milestone
(F0 → F1 → F2 → F3).
