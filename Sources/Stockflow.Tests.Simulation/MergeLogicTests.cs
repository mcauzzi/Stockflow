using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;

namespace Stockflow.Tests.Simulation;

public class MergeLogicTests
{
    private static MergeLogic MakeMerge(
        MergeMode    mode  = MergeMode.Alternating,
        RoutingGraph? graph = null)
        => new(1, new GridCoord(0, 0), Direction.North, mode, 1f,
               graph ?? new RoutingGraph());

    [Fact]
    public void TryAccept_EmptySlot_In0_Accepts()
    {
        var merge  = MakeMerge();
        var entity = new EntityManager().Spawn("A", 1f, 1f, 0f, merge, new PortId(0));

        Assert.True(merge.TryAccept(entity, new PortId(0)));
        Assert.Same(merge, entity.CurrentComponent);
        Assert.Equal(0f, entity.Progress);
    }

    [Fact]
    public void TryAccept_EmptySlot_In1_Rejected_Initially()
    {
        var merge  = MakeMerge();
        var entity = new EntityManager().Spawn("A", 1f, 1f, 0f, merge, new PortId(1));

        Assert.False(merge.TryAccept(entity, new PortId(1)));
        Assert.Null(merge.Occupant);
    }

    [Fact]
    public void TryAccept_OccupiedSlot_ReturnsFalse()
    {
        var merge = MakeMerge();
        var mgr   = new EntityManager();
        var e1    = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
        var e2    = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(0));
        merge.TryAccept(e1, new PortId(0));

        Assert.False(merge.TryAccept(e2, new PortId(0)));
    }

    [Fact]
    public void Alternating_AfterIn0_ActiveSwitchesToIn1()
    {
        var graph = new RoutingGraph();
        var merge = MakeMerge(MergeMode.Alternating, graph);
        var mgr   = new EntityManager();
        var exit  = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
        graph.Connect(merge, new PortId(2), exit, new PortId(0));

        var e1 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
        merge.TryAccept(e1, new PortId(0));
        merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

        var e2 = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(0));
        Assert.False(merge.TryAccept(e2, new PortId(0)));
        var e3 = mgr.Spawn("C", 1f, 1f, 0f, merge, new PortId(1));
        Assert.True(merge.TryAccept(e3, new PortId(1)));
    }

    [Fact]
    public void Alternating_AfterIn1_ActiveSwitchesToIn0()
    {
        var graph = new RoutingGraph();
        var merge = MakeMerge(MergeMode.Alternating, graph);
        var mgr   = new EntityManager();
        var exit  = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
        graph.Connect(merge, new PortId(2), exit, new PortId(0));

        var e1 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
        merge.TryAccept(e1, new PortId(0));
        merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

        var e2 = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(1));
        merge.TryAccept(e2, new PortId(1));
        merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

        var e3 = mgr.Spawn("C", 1f, 1f, 0f, merge, new PortId(1));
        Assert.False(merge.TryAccept(e3, new PortId(1)));
        var e4 = mgr.Spawn("D", 1f, 1f, 0f, merge, new PortId(0));
        Assert.True(merge.TryAccept(e4, new PortId(0)));
    }

    [Fact]
    public void Priority_AfterIn0_ActiveStaysIn0()
    {
        var graph = new RoutingGraph();
        var merge = MakeMerge(MergeMode.Priority, graph);
        var mgr   = new EntityManager();
        var exit  = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
        graph.Connect(merge, new PortId(2), exit, new PortId(0));

        var e1 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
        merge.TryAccept(e1, new PortId(0));
        merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

        var e2 = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(1));
        Assert.False(merge.TryAccept(e2, new PortId(1)));
        var e3 = mgr.Spawn("C", 1f, 1f, 0f, merge, new PortId(0));
        Assert.True(merge.TryAccept(e3, new PortId(0)));
    }

    [Fact]
    public void Priority_StallThreshold_SwitchesToIn1()
    {
        var merge = MakeMerge(MergeMode.Priority);
        var mgr   = new EntityManager();

        for (int i = 0; i < 30; i++) merge.Tick(0f);

        var entity = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(1));
        Assert.True(merge.TryAccept(entity, new PortId(1)));
    }

    [Fact]
    public void Priority_AfterIn1Accepts_BackToIn0()
    {
        var graph = new RoutingGraph();
        var merge = MakeMerge(MergeMode.Priority, graph);
        var mgr   = new EntityManager();
        var exit  = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
        graph.Connect(merge, new PortId(2), exit, new PortId(0));

        for (int i = 0; i < 30; i++) merge.Tick(0f);

        var e1 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(1));
        merge.TryAccept(e1, new PortId(1));
        merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

        var e2 = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(1));
        Assert.False(merge.TryAccept(e2, new PortId(1)));
        var e3 = mgr.Spawn("C", 1f, 1f, 0f, merge, new PortId(0));
        Assert.True(merge.TryAccept(e3, new PortId(0)));
    }

    [Fact]
    public void Tick_ProgressAdvances()
    {
        var merge  = MakeMerge();
        var entity = new EntityManager().Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
        merge.TryAccept(entity, new PortId(0));

        merge.Tick(0.5f);

        Assert.Equal(0.5f, entity.Progress);
    }

    [Fact]
    public void Tick_EntityComplete_TransfersDownstream()
    {
        var graph      = new RoutingGraph();
        var merge      = MakeMerge(graph: graph);
        var downstream = new OneWayConveyor(2, new GridCoord(0, -1), Direction.North, 1f, graph);
        graph.Connect(merge, new PortId(2), downstream, new PortId(0));

        var entity = new EntityManager().Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
        merge.TryAccept(entity, new PortId(0));
        merge.Tick(1f);
        merge.Tick(0f);

        Assert.Null(merge.Occupant);
        Assert.Same(downstream, entity.CurrentComponent);
    }

    [Fact]
    public void Tick_NoNext_EntityStays()
    {
        var merge  = MakeMerge();
        var entity = new EntityManager().Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
        merge.TryAccept(entity, new PortId(0));
        merge.Tick(1f);
        merge.Tick(0f);

        Assert.Same(entity, merge.Occupant);
    }

    [Fact]
    public void StallTicks_ResetOnAccept()
    {
        var graph = new RoutingGraph();
        var merge = MakeMerge(MergeMode.Priority, graph);
        var mgr   = new EntityManager();
        var exit  = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
        graph.Connect(merge, new PortId(2), exit, new PortId(0));

        for (int i = 0; i < 15; i++) merge.Tick(0f);

        var e1 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
        merge.TryAccept(e1, new PortId(0));
        merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

        for (int i = 0; i < 29; i++) merge.Tick(0f);

        var e2 = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(1));
        Assert.False(merge.TryAccept(e2, new PortId(1)));
    }
}
