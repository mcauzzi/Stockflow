using MessagePack;

namespace Stockflow.Protocol.Messages;

/// <summary>
/// Base type for every message a client sends to the server.
/// Each concrete subtype is registered as a MessagePack Union — the discriminator byte
/// is part of the wire format, never renumber existing entries.
/// </summary>
[Union(0, typeof(PlaceComponentMessage))]
[Union(1, typeof(RemoveComponentMessage))]
[Union(2, typeof(ConfigureComponentMessage))]
[Union(3, typeof(ChangeSpeedMessage))]
[Union(4, typeof(RequestFullStateMessage))]
[MessagePackObject]
public abstract class ClientMessage
{
    /// <summary>
    /// Client-generated correlation id. The server echoes it back in <c>CommandResultMessage</c>
    /// so the client can match the response with the originating command.
    /// </summary>
    [Key(0)] public int CommandId { get; init; }
}

/// <summary>
/// Ask the server to place a new component at the given grid cell facing a given direction.
/// </summary>
[MessagePackObject]
public sealed class PlaceComponentMessage : ClientMessage
{
    [Key(1)] public string    Kind      { get; init; } = "";
    [Key(2)] public int       GridX     { get; init; }
    [Key(3)] public int       GridY     { get; init; }
    [Key(4)] public Direction Direction { get; init; }
}

/// <summary>
/// Ask the server to remove the component with the given id.
/// </summary>
[MessagePackObject]
public sealed class RemoveComponentMessage : ClientMessage
{
    [Key(1)] public int ComponentId { get; init; }
}

/// <summary>
/// Update loose properties on an existing component (speed, priority, routing rule…).
/// Properties are free-form strings so new fields can ship without a protocol bump —
/// the server parses them per component type.
/// </summary>
[MessagePackObject]
public sealed class ConfigureComponentMessage : ClientMessage
{
    [Key(1)] public int                        ComponentId { get; init; }
    [Key(2)] public Dictionary<string, string> Properties  { get; init; } = new();
}

/// <summary>
/// Request a new simulation speed. The server may refuse (returns <c>Success=false</c>)
/// if a WMS session forces <see cref="SimSpeed.Live"/>.
/// </summary>
[MessagePackObject]
public sealed class ChangeSpeedMessage : ClientMessage
{
    [Key(1)] public SimSpeed Speed { get; init; }
}

/// <summary>
/// Request an authoritative full state resync (e.g. after a checksum mismatch or reconnect).
/// The server replies with a <c>FullStateMessage</c>.
/// </summary>
[MessagePackObject]
public sealed class RequestFullStateMessage : ClientMessage
{
}
