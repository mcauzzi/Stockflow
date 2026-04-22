using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;

namespace Stockflow.Tests.Simulation;

public class OneWayConveyorTests
{
    private static readonly RoutingGraph SharedGraph = new();

    private static OneWayConveyor MakeConveyor(int id = 1, float speed = 1f, RoutingGraph? graph = null)
        => new(id, new GridCoord(0, 0), Direction.North, speed, graph ?? new RoutingGraph());

    private static SimEntity SpawnOn(OneWayConveyor conveyor)
    {
        var mgr    = new EntityManager();
        var entity = mgr.Spawn("SKU-A", 1f, 1f, 0f, conveyor, new PortId(0));
        conveyor.TryAccept(entity, new PortId(0));
        return entity;
    }

    [Fact]
    public void TryAccept_EmptyConveyor_AcceptsEntityAndSetsProgress()
    {
        var conveyor = MakeConveyor();
        var entity   = new EntityManager().Spawn("X", 1f, 1f, 0f, conveyor, new PortId(0));

        Assert.True(conveyor.TryAccept(entity, new PortId(0)));
        Assert.Same(conveyor, entity.CurrentComponent);
        Assert.Equal(0f, entity.Progress);
    }

    [Fact]
    public void TryAccept_OccupiedConveyor_ReturnsFalse()
    {
        var conveyor = MakeConveyor();
        var mgr      = new EntityManager();
        var e1       = mgr.Spawn("A", 1f, 1f, 0f, conveyor, new PortId(0));
        var e2       = mgr.Spawn("B", 1f, 1f, 0f, conveyor, new PortId(0));

        conveyor.TryAccept(e1, new PortId(0));
        Assert.False(conveyor.TryAccept(e2, new PortId(0)));
    }

    [Fact]
    public void Tick_EntityInTransit_AdvancesProgress()
    {
        var conveyor = MakeConveyor(speed: 1f);
        var entity   = SpawnOn(conveyor);

        conveyor.Tick(0.5f);

        Assert.Equal(0.5f, entity.Progress);
    }

    [Fact]
    public void Tick_EntityComplete_TransfersToNextConveyor()
    {
        var graph = new RoutingGraph();
        var c1    = new OneWayConveyor(1, new GridCoord(0, 0), Direction.North, 1f, graph);
        var c2    = new OneWayConveyor(2, new GridCoord(0, 1), Direction.North, 1f, graph);
        graph.Connect(c1, new PortId(1), c2, new PortId(0));

        var entity = SpawnOn(c1);
        c1.Tick(1f);   // advance to Progress = 1.0
        c1.Tick(0f);   // trigger transfer (Progress >= 1)

        Assert.Null(c1.Occupant);
        Assert.Same(c2, entity.CurrentComponent);
    }

    [Fact]
    public void Tick_EntityComplete_NoNextComponent_EntityStaysOnConveyor()
    {
        var conveyor = MakeConveyor();
        var entity   = SpawnOn(conveyor);

        conveyor.Tick(1f);
        conveyor.Tick(0f);

        Assert.Same(entity, conveyor.Occupant);
    }
}
