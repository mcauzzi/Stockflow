using Stockflow.Simulation.Component;

namespace Stockflow.Simulation.Routing;

public class RoutingGraph
{
    private readonly Dictionary<(ISimComponent, PortId), Connection> connections = new();

    public void Connect(ISimComponent from, PortId fromPort,
                        ISimComponent to,   PortId toPort)
    {
        connections[(from, fromPort)] = new Connection(from, fromPort, to, toPort);
    }

    public void Disconnect(ISimComponent component, PortId port)
    {
        connections.Remove((component, port));
    }

    public void DisconnectAll(ISimComponent component)
    {
        var keys = connections
            .Where(kv => kv.Key.Item1 == component || kv.Value.To == component)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in keys)
            connections.Remove(key);
    }

    // Dato un componente e la sua porta di uscita, chi c'è dall'altra parte?
    public Connection? GetNext(ISimComponent component, PortId outPort)
    {
        return connections.TryGetValue((component, outPort), out var connection)
                   ? connection
                   : null;
    }
}