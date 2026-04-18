using Stockflow.Simulation.Grid;

namespace Stockflow.Simulation.Component;

public readonly record struct Port(
    PortId           Id,
    GridCoord     Position,      // cella su cui si affaccia
    PortDirection Direction  // In, Out, Bidirectional
);