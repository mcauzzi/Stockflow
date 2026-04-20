# Stockflow.Protocol

Shared wire-format library. Everything that travels on the WebSocket between the Stockflow
server and a client (Unity, web dashboard, CLI, WMS adapter) is defined here.

This is the **only** project the clients are expected to reference — it contains no
simulation logic and no ASP.NET dependencies, just DTOs and MessagePack configuration.

---

## 1. Why MessagePack?

MessagePack is a binary serialization format — think "JSON, but smaller and faster, encoded
as bytes". It was chosen over JSON for the WebSocket channel because:

| Metric                       | JSON          | MessagePack   |
|------------------------------|---------------|---------------|
| Size (100-entity delta)      | 10–25 KB      | 2–5 KB        |
| Serialize / deserialize time | baseline      | 5–10× faster  |
| Binary payloads              | base64 only   | native        |
| Schema on the wire           | field names   | integer keys  |

The ASP.NET REST endpoints still use JSON — human readability matters there, and the volume
is low. MessagePack is only on the high-frequency WebSocket channel.

Library used: **[MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp)**
(package `MessagePack`). The same package works in Unity, so clients can deserialize
with zero code duplication.

---

## 2. The three attributes you need to know

### `[MessagePackObject]` — marks a class or struct as serializable

Every message type we send across the wire carries this attribute. It signals to the
MessagePack source generator that the type participates in serialization.

```csharp
[MessagePackObject]
public sealed class PlaceComponentMessage : ClientMessage { ... }
```

### `[Key(n)]` — assigns an integer tag to a field

Instead of storing field names (`"GridX"`, `"GridY"` …) on the wire, MessagePack stores
integer tags. That is the main reason payloads are 2–5× smaller than JSON.

```csharp
[Key(0)] public int GridX { get; init; }
[Key(1)] public int GridY { get; init; }
```

**Golden rule:** once a field ships in a release, its `Key` number is frozen forever.
Reordering, renumbering, or repurposing a key silently corrupts data for any peer running
an older build. New fields go on **new, unused** numbers. Removed fields leave their number
reserved (add a comment so nobody reuses it).

Keys inside a subclass start at `1` because `Key(0)` is already taken by the base class
(`ServerTime` for `ServerMessage`, `CommandId` for `ClientMessage`). Keep that offset in
mind when adding new members.

### `[Union(tag, typeof(Subclass))]` — polymorphism

A base class annotated with `[Union]` entries can be serialized and deserialized as any of
its registered subtypes. The tag (a small integer) is written on the wire as a
discriminator; the deserializer picks the right concrete type automatically.

```csharp
[Union(0, typeof(StateDeltaMessage))]
[Union(1, typeof(FullStateMessage))]
[Union(2, typeof(CommandResultMessage))]
public abstract class ServerMessage { ... }
```

A receiver does:

```csharp
ServerMessage msg = MessagePackSerializer.Deserialize<ServerMessage>(buffer, options);
switch (msg)
{
    case StateDeltaMessage delta:    /* apply delta */ break;
    case FullStateMessage full:      /* rebuild visuals */ break;
    case CommandResultMessage ack:   /* correlate via ack.CommandId */ break;
}
```

**Same compatibility rule as `Key`:** union tags are frozen once shipped. New message types
go on new tag numbers.

---

## 3. What's in the box

```
Stockflow.Protocol/
├── Messages/
│   ├── SharedTypes.cs         ← Vector3, enums, state DTOs used by messages
│   ├── ServerMessages.cs      ← Server → Client
│   └── ClientMessages.cs      ← Client → Server
└── Serialization/
    └── MessagePackConfig.cs   ← One-shot options/resolver setup
```

### 3.1 — Server → Client messages

| Message                | Union tag | When sent                                                |
|------------------------|-----------|----------------------------------------------------------|
| `StateDeltaMessage`    | 0         | Every tick (10–100 Hz). Only what changed.               |
| `FullStateMessage`     | 1         | Initial connection, after desync, on `RequestFullState`. |
| `CommandResultMessage` | 2         | Ack/nack for a client command, echoing its `CommandId`.  |

### 3.2 — Client → Server messages

| Message                     | Union tag | Purpose                                          |
|-----------------------------|-----------|--------------------------------------------------|
| `PlaceComponentMessage`     | 0         | Put a new component on the grid.                 |
| `RemoveComponentMessage`    | 1         | Delete an existing component by id.              |
| `ConfigureComponentMessage` | 2         | Change loose properties on a component.          |
| `ChangeSpeedMessage`        | 3         | Pause / 1x / 2x / 5x / 10x / Live.               |
| `RequestFullStateMessage`   | 4         | Ask the server for a fresh full snapshot.        |

All clients include a client-generated `CommandId`; the server echoes it back in
`CommandResultMessage` so the client can correlate request and response.

### 3.3 — Shared types

`SharedTypes.cs` holds everything the messages *contain*:

- `Vector3` — world-space position (server converts from its graph-based representation
  before broadcasting).
- `Direction`, `EntityStatus`, `SimSpeed` — wire-stable `byte` enums (closed sets that
  won't grow with plugins).
- `ComponentKinds` — static string constants for built-in component kinds. Components
  travel on the wire as `string Kind` rather than an enum so plugins can register new
  kinds without recompiling `Stockflow.Protocol`; once a kind string ships it becomes
  part of the wire contract and must not be renamed.
- `EntityState`, `ComponentState`, `SimEvent`, `MetricsSnapshot` — DTOs embedded in
  `StateDeltaMessage` / `FullStateMessage`.

---

## 4. Using the library

### 4.1 — One-time startup

Call `MessagePackConfig.Initialize()` once, before anything is serialized. It's idempotent,
so it's safe to call from multiple entry points.

```csharp
// In the server's Program.cs, or a Unity bootstrap MonoBehaviour's Awake()
Stockflow.Protocol.Serialization.MessagePackConfig.Initialize();
```

After this, the configured `MessagePackSerializerOptions` are both available as
`MessagePackConfig.Options` and installed as `MessagePackSerializer.DefaultOptions`.

### 4.2 — Sending (server side)

```csharp
var delta = new StateDeltaMessage
{
    ServerTime     = (float)stopwatch.Elapsed.TotalSeconds,
    SimulationTime = engine.Clock.SimulationTime,
    TimeScale      = engine.TimeScale,
    CreatedEntities = newEntities,
    UpdatedEntities = movedEntities,
    RemovedEntityIds = deadIds,
    // ...
};

// Serialize the BASE type so the union tag is written.
byte[] payload = MessagePackSerializer.Serialize<ServerMessage>(delta);
await socket.SendAsync(payload, WebSocketMessageType.Binary, ...);
```

**Always serialize against the base class (`ServerMessage` / `ClientMessage`)** so the
discriminator lands on the wire. Serializing against the concrete subtype skips the union
header, and the receiver won't be able to tell the messages apart.

### 4.3 — Receiving (client side)

```csharp
var msg = MessagePackSerializer.Deserialize<ServerMessage>(buffer.AsMemory(0, count));
switch (msg)
{
    case FullStateMessage f:     stateBuffer.ApplyFullState(f); break;
    case StateDeltaMessage d:    stateBuffer.ApplyDelta(d);     break;
    case CommandResultMessage r: pending.Resolve(r);            break;
}
```

### 4.4 — LZ4 compression is automatic

`MessagePackConfig` enables `Lz4BlockArray` compression. You do nothing different — the
library compresses on serialize and decompresses on deserialize as long as both peers use
the same options (which they will, since they both call `MessagePackConfig.Initialize`).

Deltas typically compress another 2–3× on top of MessagePack's binary layout.

---

## 5. Evolution rules (do not break the wire)

1. **Never renumber an existing `Key` or `Union` tag.** Append only.
2. **Never change an existing field's type.** Add a new field on a new key.
3. **Don't rely on field initializers to provide default values after deserialization.**
   If a sender legitimately omits a field (new sender, old field), the receiver gets
   `default(T)`, not the initializer value. In practice we always write every key, so this
   is a theoretical concern — but if you ever split the protocol across versions, reckon
   with it.
4. **Enum values are bytes with stable ordering.** Never reorder, never reuse a removed
   value. Add new enum members at the end.
5. **Component kinds (strings) are wire contracts too.** Once `"conveyor_oneway"` ships,
   it is immutable. Pick the string carefully the first time; add new kinds with new
   strings; never silently alias or rename. Plugins are expected to namespace their kinds
   (e.g. `"acme.custom_sorter"`) to avoid collisions with core.
6. **Adding a new message type:** allocate the next unused `Union` tag on the relevant
   base class, add the `[MessagePackObject]` subclass, done. Old peers that don't know
   the tag will throw on deserialize — that's the designed behaviour.

---

## 6. Cross-reference

- Architecture doc: [`Docs/ARCHITECTURE_StockFlow_v0.3.md`](../../Docs/ARCHITECTURE_StockFlow_v0.3.md)
  — §4.2 tick/network boundary, §7 protocol, §8 comms cycle, §12 latency & size budgets,
  §13 desync detection.
- Originating issue: [#9](https://github.com/mcauzzi/Stockflow/issues/9) —
  *StockFlow.Protocol — messaggi e tipi condivisi*.
