using MessagePack;

namespace Stockflow.Protocol.Messages;

/// <summary>
/// Base type for every message the server sends to connected clients over the WebSocket.
/// MessagePack [Union] serialises the concrete subtype automatically — callers deserialise
/// as ServerMessage and pattern-match on the result.
/// Union keys are part of the wire format; never renumber an existing entry.
/// </summary>
[Union(0, typeof(StateDeltaMessage))]
[Union(1, typeof(FullStateMessage))]
[Union(2, typeof(CommandResultMessage))]
[MessagePackObject]
public abstract class ServerMessage
{
    /// <summary>Server wall-clock time (seconds since start) for latency diagnostics.</summary>
    [Key(0)] public float ServerTime { get; init; }
}

/// <summary>
/// Delta between two consecutive simulation ticks. The default per-tick broadcast carries
/// only what has changed — see Docs/ARCHITECTURE §7.3 and §12.3 for size budgets.
/// </summary>
[MessagePackObject]
public sealed class StateDeltaMessage : ServerMessage
{
    [Key(1)] public float SimulationTime { get; init; }
    [Key(2)] public float TimeScale      { get; init; }

    [Key(3)] public EntityState[]    UpdatedEntities    { get; init; } = [];
    [Key(4)] public EntityState[]    CreatedEntities    { get; init; } = [];
    [Key(5)] public int[]            RemovedEntityIds   { get; init; } = [];

    [Key(6)] public ComponentState[] UpdatedComponents  { get; init; } = [];
    [Key(7)] public ComponentState[] CreatedComponents  { get; init; } = [];
    [Key(8)] public int[]            RemovedComponentIds { get; init; } = [];

    [Key(9)]  public SimEvent[]       Events  { get; init; } = [];
    [Key(10)] public MetricsSnapshot? Metrics { get; init; }

    /// <summary>Optional rolling checksum of the full state, emitted every N ticks for desync detection.</summary>
    [Key(11)] public uint? StateChecksum { get; init; }
}

/// <summary>
/// Full authoritative snapshot. Sent on initial connection and whenever a client asks to
/// resync (see <see cref="RequestFullStateMessage"/>). Much larger than a delta; use sparingly.
/// </summary>
[MessagePackObject]
public sealed class FullStateMessage : ServerMessage
{
    [Key(1)] public float            SimulationTime { get; init; }
    [Key(2)] public float            TimeScale      { get; init; }
    [Key(3)] public EntityState[]    Entities       { get; init; } = [];
    [Key(4)] public ComponentState[] Components     { get; init; } = [];
    [Key(5)] public MetricsSnapshot? Metrics        { get; init; }
}

/// <summary>
/// Ack/nack for a command previously sent by the client.
/// The server echoes the client-supplied <c>CommandId</c> so the client can correlate.
/// </summary>
[MessagePackObject]
public sealed class CommandResultMessage : ServerMessage
{
    [Key(1)] public int     CommandId    { get; init; }
    [Key(2)] public bool    Success      { get; init; }
    [Key(3)] public string? ErrorMessage { get; init; }
}
