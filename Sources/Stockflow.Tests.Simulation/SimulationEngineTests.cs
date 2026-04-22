using Stockflow.Simulation.Component;
using Stockflow.Simulation.Core;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;
using Stockflow.Tests.Simulation.Helpers;

namespace Stockflow.Tests.Simulation;

public class SimulationEngineTests
{
    private static SimulationEngine MakeEngine() => new(10, 10, 1);

    [Fact]
    public void Tick_AdvancesSimulationTime()
    {
        var engine = MakeEngine();

        engine.Tick(0.1f);
        engine.Tick(0.1f);

        Assert.Equal(0.2f, engine.SimulationTime, precision: 5);
    }

    [Fact]
    public void Tick_ExecutesAllRegisteredComponents()
    {
        var engine = MakeEngine();
        var stub1  = new StubComponent(1);
        var stub2  = new StubComponent(2);
        engine.State.Components.Add(stub1);
        engine.State.Components.Add(stub2);

        engine.Tick(1f);

        Assert.Equal(1, stub1.TickCount);
        Assert.Equal(1, stub2.TickCount);
    }

    [Fact]
    public void GetStateDelta_AfterAddingComponent_ReportsAdded()
    {
        var engine = MakeEngine();
        var stub   = new StubComponent(42);
        engine.State.Components.Add(stub);

        var delta = engine.GetStateDelta();

        Assert.Contains(42, delta.AddedComponentIds);
        Assert.Empty(delta.RemovedComponentIds);
    }

    [Fact]
    public void GetStateDelta_AfterRemovingComponent_ReportsRemoved()
    {
        var engine = MakeEngine();
        var stub   = new StubComponent(7);
        engine.State.Components.Add(stub);
        engine.GetStateDelta(); // baseline

        engine.State.Components.Remove(stub);
        var delta = engine.GetStateDelta();

        Assert.Contains(7, delta.RemovedComponentIds);
    }

    [Fact]
    public void GetStateDelta_AfterSpawningEntity_ReportsAdded()
    {
        var engine = MakeEngine();
        var comp   = new StubComponent(1);
        var entity = engine.State.Entities.Spawn("SKU", 1f, 1f, 0f, comp, new PortId(0));

        var delta = engine.GetStateDelta();

        Assert.Single(delta.AddedEntityStates, s => s.Id == entity.Id);
    }

    [Fact]
    public void GetStateDelta_AfterDespawningEntity_ReportsRemoved()
    {
        var engine = MakeEngine();
        var comp   = new StubComponent(1);
        var entity = engine.State.Entities.Spawn("SKU", 1f, 1f, 0f, comp, new PortId(0));
        engine.GetStateDelta(); // baseline

        engine.State.Entities.Despawn(entity.Id);
        var delta = engine.GetStateDelta();

        Assert.Contains(entity.Id, delta.RemovedEntityIds);
    }

    [Fact]
    public void TimeScale_ChangesAdvanceRate()
    {
        var engine = MakeEngine();
        engine.TimeScale = 2f;

        engine.Tick(0.1f);

        Assert.Equal(0.1f, engine.SimulationTime, precision: 5);
    }
}
