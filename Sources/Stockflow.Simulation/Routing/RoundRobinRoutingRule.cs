using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;

namespace Stockflow.Simulation.Routing;

/// <summary>
/// Routes entities to output ports in strict rotation order, regardless of entity
/// properties. After each successful transfer the index advances; blocked outputs
/// do not cause skipping — the entity waits until its target port is free.
/// </summary>
public sealed class RoundRobinRoutingRule : IRoutingRule
{
    private int _index;

    public PortId SelectOutput(SimEntity entity, IReadOnlyList<PortId> outputPorts)
        => outputPorts[_index % outputPorts.Count];

    public void OnTransferSucceeded(PortId usedPort)
        => _index++;
}
