using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;

namespace Stockflow.Simulation.Routing;

/// <summary>
/// Decides which output port a diverter (or similar 1-to-N component) should use
/// for a given entity. Implementations encapsulate routing strategies such as
/// round-robin, SKU-based, destination-based, or minimum-load.
/// </summary>
public interface IRoutingRule
{
    /// <summary>
    /// Returns the PortId the entity should be sent to.
    /// Called every tick while the entity is ready to transfer (Progress >= 1).
    /// The rule must not mutate internal state here — state advances only via
    /// <see cref="OnTransferSucceeded"/>.
    /// </summary>
    PortId SelectOutput(SimEntity entity, IReadOnlyList<PortId> outputPorts);

    /// <summary>
    /// Called by the component after a successful transfer to <paramref name="usedPort"/>.
    /// The rule uses this callback to advance its internal state (e.g. increment the
    /// round-robin index).
    /// </summary>
    void OnTransferSucceeded(PortId usedPort);
}
