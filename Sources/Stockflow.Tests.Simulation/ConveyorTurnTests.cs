using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;

namespace Stockflow.Tests.Simulation;

public class ConveyorTurnTests
{
    [Fact]
    public void TryAccept_EmptyTurn_AcceptsEntity()
    {
        var turn   = new ConveyorTurn(1, new GridCoord(0, 0), Direction.North, TurnSide.Right, 1f, new RoutingGraph());
        var entity = new EntityManager().Spawn("X", 1f, 1f, 0f, turn, new PortId(0));

        Assert.True(turn.TryAccept(entity, new PortId(0)));
        Assert.Same(turn, entity.CurrentComponent);
    }

    [Fact]
    public void TryAccept_OccupiedTurn_ReturnsFalse()
    {
        var turn = new ConveyorTurn(1, new GridCoord(0, 0), Direction.North, TurnSide.Right, 1f, new RoutingGraph());
        var mgr  = new EntityManager();
        var e1   = mgr.Spawn("A", 1f, 1f, 0f, turn, new PortId(0));
        var e2   = mgr.Spawn("B", 1f, 1f, 0f, turn, new PortId(0));

        turn.TryAccept(e1, new PortId(0));
        Assert.False(turn.TryAccept(e2, new PortId(0)));
    }

    [Theory]
    [InlineData(Direction.North, TurnSide.Right, Direction.East)]
    [InlineData(Direction.North, TurnSide.Left,  Direction.West)]
    [InlineData(Direction.East,  TurnSide.Right, Direction.South)]
    [InlineData(Direction.West,  TurnSide.Left,  Direction.South)]
    public void ExitPort_IsOnCorrectSide(Direction facing, TurnSide turn, Direction expectedExit)
    {
        var pos     = new GridCoord(2, 2);
        var comp    = new ConveyorTurn(1, pos, facing, turn, 1f, new RoutingGraph());
        var outPort = comp.Ports.First(p => p.Direction == PortDirection.Out);

        Assert.Equal(pos + expectedExit.ToOffset(), outPort.Position);
    }

    [Fact]
    public void FourTurns_ClockwiseLoop_EntityCirculatesAndReturnsToStart()
    {
        //  CT1(0,1) →East→ CT2(1,1)
        //     ↑                 ↓
        //  CT4(0,0) ←West← CT3(1,0)
        //
        // With Speed=1 and deltaTime=1, each tick advances Progress by 1.
        // Components are ticked left-to-right: when CT1 transfers to CT2 in round N,
        // CT2 is ticked in the same round and already advances the entity.
        // One full lap therefore costs 5 ticks (not 4×2=8):
        //   i=0 CT1 advance,  i=1 CT1→CT2 advance,  i=2 CT2→CT3 advance,
        //   i=3 CT3→CT4 advance,  i=4 CT4→CT1 (entity back, Progress=0)

        var graph = new RoutingGraph();

        var ct1 = new ConveyorTurn(1, new GridCoord(0, 1), Direction.North, TurnSide.Right, 1f, graph);
        var ct2 = new ConveyorTurn(2, new GridCoord(1, 1), Direction.East,  TurnSide.Right, 1f, graph);
        var ct3 = new ConveyorTurn(3, new GridCoord(1, 0), Direction.South, TurnSide.Right, 1f, graph);
        var ct4 = new ConveyorTurn(4, new GridCoord(0, 0), Direction.West,  TurnSide.Right, 1f, graph);

        var outPort = new PortId(1);
        var inPort  = new PortId(0);
        graph.Connect(ct1, outPort, ct2, inPort);
        graph.Connect(ct2, outPort, ct3, inPort);
        graph.Connect(ct3, outPort, ct4, inPort);
        graph.Connect(ct4, outPort, ct1, inPort);

        var mgr    = new EntityManager();
        var entity = mgr.Spawn("BOX", 1f, 1f, 0f, ct1, inPort);
        ct1.TryAccept(entity, inPort);

        ISimComponent[] components = [ct1, ct2, ct3, ct4];

        // Two full laps = 10 ticks; entity should be back on ct1 with Progress=0 each lap.
        for (int i = 0; i < 10; i++)
            foreach (var c in components)
                c.Tick(1f);

        Assert.Same(ct1, entity.CurrentComponent);
        Assert.Equal(0f, entity.Progress);
    }
}
