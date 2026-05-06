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
        SimComponentType.MergeLogic       => ComponentKinds.MergeLogic,
        _                                 => type.ToString().ToLowerInvariant(),
    };

    public static Dictionary<string, string>? BuildProperties(ISimComponent c)
        => c.ExportProperties() is { Count: > 0 } props ? props : null;
}
