using Stockflow.Simulation.Component;

namespace Stockflow.Simulation.Routing;

public readonly record struct Connection(
    ISimComponent From, PortId FromPort,
    ISimComponent To,   PortId ToPort
);