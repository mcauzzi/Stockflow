using Stockflow.Protocol.Messages;
using Stockflow.Simulation.Component;
using SimComponentType = Stockflow.Simulation.Component.ComponentType;
using ISimComponent    = Stockflow.Simulation.Component.ISimComponent;

namespace Stockflow.Webserver.Serialization;

/// <summary>
/// Single source of truth for translating a simulation component into the wire format
/// used by both the WebSocket delta stream and the REST state endpoint.
/// </summary>
public static class ComponentSerializer
{
    public static string KindString(SimComponentType type) => type switch
    {
        SimComponentType.OneWayConveyor   => ComponentKinds.OneWayConveyor,
        SimComponentType.ConveyorTurn     => ComponentKinds.ConveyorTurn,
        SimComponentType.PackageGenerator => ComponentKinds.PackageGenerator,
        SimComponentType.PackageExit      => ComponentKinds.PackageExit,
        _                                 => type.ToString().ToLowerInvariant(),
    };

    public static Dictionary<string, string>? BuildProperties(ISimComponent c) => c switch
    {
        PackageGenerator gen => new()
        {
            ["spawnRate"] = gen.SpawnRate.ToString("F3"),
            ["sku"]       = gen.Sku,
            ["weight"]    = gen.Weight.ToString("F3"),
            ["size"]      = gen.Size.ToString("F3"),
            ["enabled"]   = gen.IsEnabled ? "true" : "false",
        },
        PackageExit exit => new()
        {
            ["totalProcessed"]     = exit.TotalProcessed.ToString(),
            ["throughput"]         = exit.Throughput.ToString("F3"),
            ["avgFulfillmentTime"] = exit.AvgFulfillmentTime.ToString("F3"),
        },
        ConveyorTurn turn => new()
        {
            ["turn"]  = turn.Turn == TurnSide.Right ? "right" : "left",
            ["speed"] = turn.Speed.ToString("F3"),
        },
        OneWayConveyor conv => new()
        {
            ["speed"] = conv.Speed.ToString("F3"),
        },
        _ => null,
    };
}
