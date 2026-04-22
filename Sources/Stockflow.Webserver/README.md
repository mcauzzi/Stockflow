# Stockflow.Webserver

ASP.NET Core host that exposes the Stockflow simulation to clients (Unity, web, CLI, WMS).
This project is the **only** process that runs in production — everything else is either a
library it consumes (`Stockflow.Simulation`, `Stockflow.Protocol`, `Stockflow.Persistence`)
or a client that talks to it over the wire.

This README covers the F0 scaffolding landed with issue [#10](https://github.com/mcauzzi/Stockflow/issues/10):
hosting bootstrap, configuration, and WebSocket accept/route/session plumbing. The
simulation tick loop (`SimulationHostedService`) and REST endpoints arrive in later issues.

---

## 1. Two channels, one process

Per [ARCHITECTURE §7.1](../../Docs/ARCHITECTURE_StockFlow_v0.3.md), the server exposes two
logical channels:

| Channel | Port | Format | Purpose |
|---|---|---|---|
| WebSocket | 9600 | MessagePack (binary, LZ4) | Real-time sim deltas at 10–100 Hz, client commands |
| REST | 9601 | JSON | Scenarios, metrics export, WMS integration, health |

Both ports are bound by a single Kestrel host. The split is a deployment convention the
reverse proxy or firewall enforces — we do not route by local port inside the app today.

Defaults (see [`Configuration/ServerConfig.cs`](Configuration/ServerConfig.cs)):

| Key | Default | Notes |
|---|---|---|
| `Server:WebSocketPort` | `9600` | Binary + LZ4 MessagePack |
| `Server:RestPort` | `9601` | JSON |
| `Server:Mode` | `Local` | `Local` (Steam/localhost) or `Enterprise` (Docker/B2B) |
| `Server:TickRate` | `10` | Hz. Consumed by `SimulationHostedService` (issue #11). |

Any value is overridable via environment variable, e.g.
`STOCKFLOW_Server__WebSocketPort=9700`.

---

## 2. WebSocket pipeline

```
           HTTP upgrade at /ws
                   │
                   ▼
         WebSocketHandler.HandleAsync
                   │ accepts upgrade, creates session
                   ▼
      ┌──────── ClientSession ─────────┐
      │   ReceiveLoop  │   SendLoop    │
      │   (1 per sess) │   (1 per sess)│
      └────────┬───────┴───────┬───────┘
               │               ▲
               ▼               │
       MessageRouter      WebSocketHandler
       .RouteAsync        .BroadcastAsync
               │               ▲
               ▼               │
       ClientCommandQueue   SimulationHostedService (#11)
       ─ producer ─►        ─ consumer ─►
                                SimulationEngine.ProcessCommand
                                SimulationEngine.Tick
                                SimulationEngine.GetStateDelta
```

### 2.1 Receive path

1. `WebSocketHandler.HandleAsync` accepts the upgrade and spins up a `ClientSession`.
2. `ClientSession.ReceiveLoopAsync` reads binary frames until the peer closes.
3. Each complete frame is handed to `MessageRouter.RouteAsync`.
4. The router deserialises as `ClientMessage` via `MessagePackConfig.Options` and enqueues
   `(session, message)` onto `IClientCommandQueue`.
5. The simulation host service (landing in issue #11) drains the queue at the top of each
   tick and feeds messages to `SimulationEngine.ProcessCommand`.

Malformed frames are logged and dropped; they do **not** kill the session — a single bad
packet from one client never disconnects that client or any other.

### 2.2 Send path

`ClientSession` owns a single-reader `Channel<ServerMessage>`. Anything that needs to send
(tick broadcast, command ack, REST-initiated push) calls `SendAsync`; the dedicated send
loop drains the channel one frame at a time. This is required because a `WebSocket` only
allows one outstanding `SendAsync` at a time — without the channel, concurrent senders
from the sim thread and a REST callback would corrupt the stream.

`WebSocketHandler.BroadcastAsync` is the fan-out entry point for the sim tick: it iterates
all live sessions and enqueues the same message on each. A slow or dead peer is logged
and skipped; the others are not blocked.

---

## 3. Adding a REST endpoint

REST endpoints are just minimal-API `Map*` calls in `Program.cs`. For non-trivial
controllers, add them under `Rest/` following the same DI-first pattern as the WebSocket
classes. The only bootstrap endpoint today is `GET /api/health`, which reports the number
of connected clients.

---

## 4. Running locally

```bash
dotnet run --project Sources/Stockflow.Webserver/
```

Then:

- WebSocket: `ws://localhost:9600/ws`
- Health:    `http://localhost:9601/api/health`

---

## 5. Cross-reference

- [ARCHITECTURE §4.1, §7, §10, §14](../../Docs/ARCHITECTURE_StockFlow_v0.3.md) — channel
  split, protocol, tick loop, threading model.
- [Stockflow.Protocol README](../Stockflow.Protocol/README.md) — MessagePack configuration
  and wire evolution rules that govern every byte this server sends or receives.
- Originating issue: [#10](https://github.com/mcauzzi/Stockflow/issues/10) — F0 server
  hosting + WebSocket base.
