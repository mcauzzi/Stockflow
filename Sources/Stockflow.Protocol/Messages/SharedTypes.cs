using MessagePack;

namespace Stockflow.Protocol.Messages;

/// <summary>
/// 3D vector used for entity world-space positions sent to rendering clients.
/// The simulation itself represents position graph-based (component + port + progress);
/// the Webserver converts to Vector3 before broadcasting to clients.
/// </summary>
[MessagePackObject]
public readonly struct Vector3
{
    [Key(0)] public float X { get; init; }
    [Key(1)] public float Y { get; init; }
    [Key(2)] public float Z { get; init; }

    public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }

    public static Vector3 Zero => new(0f, 0f, 0f);
}

/// <summary>
/// Component kind. Kept as a Protocol-local enum (not a reference to the Simulation one)
/// so Unity clients can import only Stockflow.Protocol.dll.
/// Keep numeric values stable across releases — they are part of the wire format.
/// </summary>
public enum ComponentType : byte
{
    OneWayConveyor = 0,
}

/// <summary>
/// Cardinal direction a component faces. Wire-stable byte values.
/// </summary>
public enum Direction : byte
{
    North = 0,
    East  = 1,
    South = 2,
    West  = 3,
}

/// <summary>
/// Runtime status of a simulated entity (load unit). Wire-stable byte values.
/// </summary>
public enum EntityStatus : byte
{
    Idle   = 0,
    Moving = 1,
    Queued = 2,
}

/// <summary>
/// Simulation playback speed requested by the client. The server decides the actual
/// TimeScale multiplier; this enum only carries the user's intent.
/// Live forces 1x and is reserved for WMS-connected sessions.
/// </summary>
public enum SimSpeed : byte
{
    Paused    = 0,
    Normal    = 1,  // 1x
    Fast      = 2,  // 2x
    Faster    = 3,  // 5x
    UltraFast = 4,  // 10x
    Live      = 5,  // 1x locked, B2B mode
}

/// <summary>
/// Network snapshot of a single entity. Sent inside StateDeltaMessage and FullStateMessage.
/// World-space position is precomputed server-side for client rendering.
/// </summary>
[MessagePackObject]
public sealed record EntityState
{
    [Key(0)] public int          Id       { get; init; }
    [Key(1)] public string       Sku      { get; init; } = "";
    [Key(2)] public Vector3      Position { get; init; }
    [Key(3)] public EntityStatus Status   { get; init; }
}

/// <summary>
/// Network snapshot of a placed component.
/// </summary>
[MessagePackObject]
public sealed record ComponentState
{
    [Key(0)] public int           Id     { get; init; }
    [Key(1)] public ComponentType Type   { get; init; }
    [Key(2)] public int           GridX  { get; init; }
    [Key(3)] public int           GridY  { get; init; }
    [Key(4)] public Direction     Facing { get; init; }
}

/// <summary>
/// Generic simulation event (entity spawned, order completed, collision…).
/// Payload is opaque JSON so new event kinds can ship without bumping the protocol.
/// </summary>
[MessagePackObject]
public sealed record SimEvent
{
    [Key(0)] public string EventType  { get; init; } = "";
    [Key(1)] public float  SimTime    { get; init; }
    [Key(2)] public string PayloadJson { get; init; } = "";
}

/// <summary>
/// Aggregate KPI snapshot attached to each StateDeltaMessage so clients can update HUDs
/// without hitting the REST endpoint.
/// </summary>
[MessagePackObject]
public sealed record MetricsSnapshot
{
    [Key(0)] public double Throughput         { get; init; }
    [Key(1)] public double AvgFulfillmentTime { get; init; }
    [Key(2)] public double WarehouseSaturation { get; init; }
    [Key(3)] public int    ActiveOrders       { get; init; }
    [Key(4)] public int    CompletedOrders    { get; init; }
}
