using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Core;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Modules;
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

    [Fact]
    public void PlaceMergeLogic_AddsComponentToState()
    {
        var engine = new SimulationEngine(10, 10, 1);
        var cmd    = new PlaceMergeLogicCommand(new GridCoord(5, 5), Direction.North);

        engine.ProcessCommand(cmd);

        var comp = engine.State.Components.Find(c => c.Type == ComponentType.MergeLogic);
        Assert.NotNull(comp);
        Assert.Equal(new GridCoord(5, 5), comp.Position);
    }

    [Fact]
    public void ConfigureMergeLogic_UpdatesModeAndSpeed()
    {
        var engine = new SimulationEngine(10, 10, 1);
        engine.ProcessCommand(new PlaceMergeLogicCommand(new GridCoord(5, 5), Direction.North));
        var merge = (MergeLogic)engine.State.Components.Find(c => c.Type == ComponentType.MergeLogic)!;

        engine.ProcessCommand(new ConfigureComponentCommand(merge.Id, new Dictionary<string, string>
        {
            ["mode"]  = "priority",
            ["speed"] = "2.5",
        }));

        Assert.Equal(MergeMode.Priority, merge.Mode);
        Assert.Equal(2.5f, merge.Speed);
    }

    [Fact]
    public void ConfigureMergeLogic_SetFacing_RebuildsPortPositions()
    {
        var engine = new SimulationEngine(10, 10, 1);
        engine.ProcessCommand(new PlaceMergeLogicCommand(new GridCoord(5, 5), Direction.North));
        var merge = (MergeLogic)engine.State.Components.Find(c => c.Type == ComponentType.MergeLogic)!;

        engine.ProcessCommand(new ConfigureComponentCommand(merge.Id, new Dictionary<string, string>
        {
            ["facing"] = "East",
        }));

        Assert.Equal(Direction.East, merge.Facing);
        // Facing=East: outPort at (6,5)
        Assert.Equal(new GridCoord(6, 5), merge.Ports[2].Position);
    }

    [Fact]
    public void SimulationClock_IsLiveMode_FalseByDefault()
    {
        var clock = new SimulationClock();
        Assert.False(clock.IsLiveMode);
    }

    [Fact]
    public void SimulationClock_EnterLiveMode_SetsTrue()
    {
        var clock = new SimulationClock();
        clock.EnterLiveMode();
        Assert.True(clock.IsLiveMode);
    }

    [Fact]
    public void SimulationClock_ExitLiveMode_SetsFalse()
    {
        var clock = new SimulationClock();
        clock.EnterLiveMode();
        clock.ExitLiveMode();
        Assert.False(clock.IsLiveMode);
    }

    [Fact]
    public void SimulationClock_IsLiveMode_FalseAtTimeScaleOne()
    {
        var clock = new SimulationClock();
        clock.TimeScale = 1f;
        Assert.False(clock.IsLiveMode);
    }

    private sealed class OnTickSpyModule : IComponentModule
    {
        public int OnTickCallCount { get; private set; }
        public void OnEntityEnter(SimEntity e) { }
        public void OnEntityExit(SimEntity e)  { }
        public void OnTick(float dt)           => OnTickCallCount++;
    }

    [Fact]
    public void Tick_InvokesOnTickOnAllModules()
    {
        var engine = new SimulationEngine(10, 10, 1);
        var spy    = new OnTickSpyModule();
        var graph  = new RoutingGraph();
        var conv   = new OneWayConveyor(99, new GridCoord(3, 3), Direction.North, 1f, graph,
                                        modules: [spy]);
        engine.State.Components.Add(conv);

        engine.Tick(0.1f);
        engine.Tick(0.1f);

        Assert.Equal(2, spy.OnTickCallCount);
    }
}
