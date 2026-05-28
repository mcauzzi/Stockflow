using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;

namespace Stockflow.Tests.Simulation;

public class MergeLogicTests
{
    private static MergeLogic MakeMerge(
        MergeMode     mode  = MergeMode.Alternating,
        RoutingGraph? graph = null,
        TurnSide      side  = TurnSide.Left)
        => new(1, new GridCoord(0, 0), Direction.North, mode, side, 1f,
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

        // Stall for > 1 second (threshold) at 10 Hz
        for (int i = 0; i < 11; i++) merge.Tick(0.1f);

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

        // Stall for > 1 second (threshold) at 10 Hz
        for (int i = 0; i < 11; i++) merge.Tick(0.1f);

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
    public void StallTime_ResetOnAccept()
    {
        var graph = new RoutingGraph();
        var merge = MakeMerge(MergeMode.Priority, graph);
        var mgr   = new EntityManager();
        var exit  = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
        graph.Connect(merge, new PortId(2), exit, new PortId(0));

        // Stall for 0.5s (below 1s threshold) at 10 Hz
        for (int i = 0; i < 5; i++) merge.Tick(0.1f);

        var e1 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
        merge.TryAccept(e1, new PortId(0));
        merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

        // Stall for 0.9s after accept (below threshold — should not switch)
        for (int i = 0; i < 9; i++) merge.Tick(0.1f);

        var e2 = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(1));
        Assert.False(merge.TryAccept(e2, new PortId(1)));
    }

    [Fact]
    public void SetFacing_RebuildsPortPositions()
    {
        var merge = MakeMerge(); // default Facing=North, Position=(0,0)

        // Facing=North: port0=South=(0,1), port1=West=(-1,0), port2=North=(0,-1)
        Assert.Equal(new GridCoord(0,   1), merge.Ports[0].Position);
        Assert.Equal(new GridCoord(-1,  0), merge.Ports[1].Position);
        Assert.Equal(new GridCoord(0,  -1), merge.Ports[2].Position);

        merge.SetFacing(Direction.East);

        // Facing=East: port0=West=(-1,0), port1=North=(0,-1), port2=East=(1,0)
        Assert.Equal(new GridCoord(-1,  0), merge.Ports[0].Position);
        Assert.Equal(new GridCoord(0,  -1), merge.Ports[1].Position);
        Assert.Equal(new GridCoord(1,   0), merge.Ports[2].Position);
    }

    [Fact]
    public void Ports_FacingNorth_SideRight_CorrectPositions()
    {
        var merge = MakeMerge(side: TurnSide.Right);

        // Facing=North, Side=Right:
        // InPort0 (0): South = (0, 1)   — ingresso primario, invariato
        // InPort1 (1): East  = (1, 0)   — ingresso secondario a destra
        // OutPort (2): North = (0,-1)   — uscita, invariata
        Assert.Equal(new GridCoord(0,  1), merge.Ports[0].Position);
        Assert.Equal(PortDirection.In,     merge.Ports[0].Direction);

        Assert.Equal(new GridCoord(1,  0), merge.Ports[1].Position);
        Assert.Equal(PortDirection.In,     merge.Ports[1].Direction);

        Assert.Equal(new GridCoord(0, -1), merge.Ports[2].Position);
        Assert.Equal(PortDirection.Out,    merge.Ports[2].Direction);
    }

    [Fact]
    public void SetFacing_WithSideRight_UsesRotateCW()
    {
        var merge = MakeMerge(side: TurnSide.Right);
        merge.SetFacing(Direction.East);

        // Facing=East, Side=Right:
        // InPort0 (0): West  = (-1, 0)
        // InPort1 (1): South = (0,  1)   — RotateCW di East
        // OutPort (2): East  = ( 1, 0)
        Assert.Equal(new GridCoord(-1, 0), merge.Ports[0].Position);
        Assert.Equal(new GridCoord(0,  1), merge.Ports[1].Position);
        Assert.Equal(new GridCoord(1,  0), merge.Ports[2].Position);
    }

    [Theory]
    [InlineData(10f,  0.1f)]   // 10 Hz tick rate
    [InlineData(100f, 0.01f)]  // 100 Hz tick rate
    public void Alternating_StarvationSwitchesAtSameWallTime_RegardlessOfTickRate(
        float ticksPerSecond, float deltaTime)
    {
        var graph = new RoutingGraph();
        var merge = new MergeLogic(1, new GridCoord(0, 0), Direction.North,
                                   MergeMode.Alternating, TurnSide.Left, 1f, graph);
        var mgr   = new EntityManager();

        // Merge is empty; stall threshold = 1 second.
        // Tick for just under 1 second → port0 should still be active.
        var ticksBelowThreshold = (int)(0.9f / deltaTime);
        for (var i = 0; i < ticksBelowThreshold; i++)
            merge.Tick(deltaTime);

        var entity0 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
        Assert.True(merge.TryAccept(entity0, new PortId(0)),
            "Port0 should still be active before stall threshold");

        // Reset: create a fresh merge to test the "above threshold" side independently
        var merge2 = new MergeLogic(2, new GridCoord(0, 0), Direction.North,
                                    MergeMode.Alternating, TurnSide.Left, 1f, graph);

        // Tick for over 1 second → should switch to port1
        var ticksAboveThreshold = (int)(1.1f / deltaTime);
        for (var i = 0; i < ticksAboveThreshold; i++)
            merge2.Tick(deltaTime);

        var entity1 = mgr.Spawn("B", 1f, 1f, 0f, merge2, new PortId(1));
        Assert.True(merge2.TryAccept(entity1, new PortId(1)),
            $"Port1 should be active after stall threshold at {ticksPerSecond}Hz");
    }
}
