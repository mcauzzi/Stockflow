using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;

namespace Stockflow.Tests.Simulation;

public class PackageExitTests
{
    private static (PackageExit exit, EntityManager mgr) Make(int id = 1)
    {
        var mgr  = new EntityManager();
        var exit = new PackageExit(id, new GridCoord(0, 0), Direction.North, mgr);
        return (exit, mgr);
    }

    [Fact]
    public void TryAccept_EmptyExit_AcceptsEntityAndSetsOccupant()
    {
        var (exit, mgr) = Make();
        var entity      = mgr.Spawn("X", 1f, 1f, 0f, exit, new PortId(0));

        Assert.True(exit.TryAccept(entity, new PortId(0)));
        Assert.Same(entity, exit.Occupant);
        Assert.Same(exit, entity.CurrentComponent);
    }

    [Fact]
    public void TryAccept_OccupiedExit_ReturnsFalse()
    {
        var (exit, mgr) = Make();
        var e1          = mgr.Spawn("A", 1f, 1f, 0f, exit, new PortId(0));
        var e2          = mgr.Spawn("B", 1f, 1f, 0f, exit, new PortId(0));

        exit.TryAccept(e1, new PortId(0));
        Assert.False(exit.TryAccept(e2, new PortId(0)));
    }

    [Fact]
    public void Tick_WithOccupant_DespawnsEntityAndClearsSlot()
    {
        var (exit, mgr) = Make();
        var entity      = mgr.Spawn("PKG", 1f, 1f, 0f, exit, new PortId(0));
        exit.TryAccept(entity, new PortId(0));

        exit.Tick(1f);

        Assert.Null(exit.Occupant);
        Assert.DoesNotContain(entity.Id, mgr.Active.Keys);
    }

    [Fact]
    public void Tick_WithOccupant_IncrementsTotalProcessed()
    {
        var (exit, mgr) = Make();

        for (var i = 0; i < 3; i++)
        {
            var entity = mgr.Spawn("PKG", 1f, 1f, 0f, exit, new PortId(0));
            exit.TryAccept(entity, new PortId(0));
            exit.Tick(1f);
        }

        Assert.Equal(3, exit.TotalProcessed);
    }

    [Fact]
    public void Tick_WithNoOccupant_DoesNotChangeTotalProcessed()
    {
        var (exit, _) = Make();

        exit.Tick(1f);
        exit.Tick(1f);

        Assert.Equal(0, exit.TotalProcessed);
    }

    [Fact]
    public void Tick_AvgFulfillmentTime_ReflectsEntryToExitDuration()
    {
        var (exit, mgr) = Make();
        // Entity spawned at simTime=0, exit processes it at its own simTime=3
        var entity = mgr.Spawn("PKG", 1f, 1f, entryTime: 0f, exit, new PortId(0));
        exit.TryAccept(entity, new PortId(0));

        exit.Tick(3f); // _simTime becomes 3 → fulfillment = 3 - 0 = 3

        Assert.Equal(3f, exit.AvgFulfillmentTime, precision: 5);
    }

    [Fact]
    public void Tick_AvgFulfillmentTime_AveragesAcrossMultipleEntities()
    {
        var (exit, mgr) = Make();

        // Entity 1: entry=0, processed at _simTime=2 → fulfillment=2
        var e1 = mgr.Spawn("P1", 1f, 1f, entryTime: 0f, exit, new PortId(0));
        exit.TryAccept(e1, new PortId(0));
        exit.Tick(2f);

        // Entity 2: entry=0, processed at _simTime=2+4=6 → fulfillment=6
        var e2 = mgr.Spawn("P2", 1f, 1f, entryTime: 0f, exit, new PortId(0));
        exit.TryAccept(e2, new PortId(0));
        exit.Tick(4f);

        // avg = (2 + 6) / 2 = 4
        Assert.Equal(4f, exit.AvgFulfillmentTime, precision: 5);
    }

    [Fact]
    public void Throughput_AfterProcessingEntity_IsPositive()
    {
        var (exit, mgr) = Make();
        var entity      = mgr.Spawn("PKG", 1f, 1f, 0f, exit, new PortId(0));
        exit.TryAccept(entity, new PortId(0));

        exit.Tick(1f);

        Assert.True(exit.Throughput > 0f);
    }

    [Fact]
    public void Throughput_WithNoProcessedEntities_IsZero()
    {
        var (exit, _) = Make();

        exit.Tick(5f);

        Assert.Equal(0f, exit.Throughput);
    }

    [Fact]
    public void InPort_FacesOppositeOfFacingDirection()
    {
        var mgr  = new EntityManager();
        var exit = new PackageExit(1, new GridCoord(2, 2), Direction.East, mgr);
        var inPort = exit.Ports.Single(p => p.Direction == PortDirection.In);

        // East.Opposite() = West → InPort.Position = (2,2) + (-1,0) = (1,2)
        Assert.Equal(new GridCoord(1, 2), inPort.Position);
    }
}
