using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Modules;

namespace Stockflow.Tests.Simulation.Helpers;

internal sealed class StubComponent(int id = 0, GridCoord position = default) : ISimComponent
{
    public int                             Id       { get; } = id;
    public GridCoord                       Position { get; } = position;
    public Direction                       Facing   { get; } = Direction.North;
    public ComponentType                   Type     => ComponentType.OneWayConveyor;
    public IReadOnlyList<IComponentModule> Modules  => [];
    public SimEntity?                      Occupant { get; set; }
    public IReadOnlyList<Port>             Ports    => [];
    public int                             TickCount { get; private set; }

    public void Tick(float deltaTime) => TickCount++;

    public bool TryAccept(SimEntity entity, PortId fromPort)
    {
        if (Occupant != null) return false;
        Occupant                = entity;
        entity.CurrentComponent = this;
        entity.CurrentPort      = fromPort;
        entity.Progress         = 0f;
        return true;
    }
}
