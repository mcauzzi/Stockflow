using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;

namespace Stockflow.Tests.Simulation;

public class DiverterLogicTests
{
    private static DiverterLogic MakeDiverter(
        RoutingGraph? graph = null,
        TurnSide      side  = TurnSide.Right)
        => new(1, new GridCoord(0, 0), Direction.North, side, 1f, graph ?? new RoutingGraph());

    [Fact]
    public void TryAccept_EmptySlot_AcceptsEntity()
    {
        var diverter = MakeDiverter();
        var entity   = new EntityManager().Spawn("A", 1f, 1f, 0f, diverter, new PortId(0));

        Assert.True(diverter.TryAccept(entity, new PortId(0)));
        Assert.Same(diverter, entity.CurrentComponent);
        Assert.Equal(0f, entity.Progress);
    }

    [Fact]
    public void TryAccept_OccupiedSlot_ReturnsFalse()
    {
        var diverter = MakeDiverter();
        var mgr      = new EntityManager();
        var e1       = mgr.Spawn("A", 1f, 1f, 0f, diverter, new PortId(0));
        var e2       = mgr.Spawn("B", 1f, 1f, 0f, diverter, new PortId(0));
        diverter.TryAccept(e1, new PortId(0));

        Assert.False(diverter.TryAccept(e2, new PortId(0)));
    }

    [Fact]
    public void Ports_FacingNorth_SideRight_CorrectPositions()
    {
        // Facing=North, Position=(0,0)
        // InPort  (0): South = (0, 1)
        // OutPort0(1): North = (0,-1)  — dritto
        // OutPort1(2): East  = (1, 0)  — laterale destra
        var diverter = MakeDiverter();

        Assert.Equal(new GridCoord(0,  1), diverter.Ports[0].Position);
        Assert.Equal(PortDirection.In,    diverter.Ports[0].Direction);

        Assert.Equal(new GridCoord(0, -1), diverter.Ports[1].Position);
        Assert.Equal(PortDirection.Out,    diverter.Ports[1].Direction);

        Assert.Equal(new GridCoord(1,  0), diverter.Ports[2].Position);
        Assert.Equal(PortDirection.Out,    diverter.Ports[2].Direction);
    }

    [Fact]
    public void Ports_FacingNorth_SideLeft_CorrectPositions()
    {
        var diverter = MakeDiverter(side: TurnSide.Left);

        Assert.Equal(new GridCoord(0,  1), diverter.Ports[0].Position); // InPort  → South
        Assert.Equal(PortDirection.In,     diverter.Ports[0].Direction);

        Assert.Equal(new GridCoord(0, -1), diverter.Ports[1].Position); // OutPort0 → North (dritto)
        Assert.Equal(PortDirection.Out,    diverter.Ports[1].Direction);

        Assert.Equal(new GridCoord(-1, 0), diverter.Ports[2].Position); // OutPort1 → West (sinistra)
        Assert.Equal(PortDirection.Out,    diverter.Ports[2].Direction);
    }

    [Fact]
    public void Tick_ProgressAdvances()
    {
        var diverter = MakeDiverter();
        var entity   = new EntityManager().Spawn("A", 1f, 1f, 0f, diverter, new PortId(0));
        diverter.TryAccept(entity, new PortId(0));

        diverter.Tick(0.5f);

        Assert.Equal(0.5f, entity.Progress);
    }

    [Fact]
    public void Tick_NoNext_EntityStays()
    {
        var diverter = MakeDiverter();
        var entity   = new EntityManager().Spawn("A", 1f, 1f, 0f, diverter, new PortId(0));
        diverter.TryAccept(entity, new PortId(0));
        diverter.Tick(1f);
        diverter.Tick(0f);

        Assert.Same(entity, diverter.Occupant);
    }

    [Fact]
    public void RoundRobin_FirstEntity_GoesToOutPort0()
    {
        var graph    = new RoutingGraph();
        var diverter = MakeDiverter(graph);
        var mgr      = new EntityManager();

        var exit0 = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
        var exit1 = new PackageExit(3, new GridCoord(1,  0), Direction.East,  mgr);
        graph.Connect(diverter, new PortId(1), exit0, new PortId(0));
        graph.Connect(diverter, new PortId(2), exit1, new PortId(0));

        var e1 = mgr.Spawn("A", 1f, 1f, 0f, diverter, new PortId(0));
        diverter.TryAccept(e1, new PortId(0));
        diverter.Tick(1f);
        diverter.Tick(0f);

        Assert.Null(diverter.Occupant);
        Assert.Same(exit0, e1.CurrentComponent);
    }

    [Fact]
    public void RoundRobin_SecondEntity_GoesToOutPort1()
    {
        var graph    = new RoutingGraph();
        var diverter = MakeDiverter(graph);
        var mgr      = new EntityManager();

        var exit0 = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
        var exit1 = new PackageExit(3, new GridCoord(1,  0), Direction.East,  mgr);
        graph.Connect(diverter, new PortId(1), exit0, new PortId(0));
        graph.Connect(diverter, new PortId(2), exit1, new PortId(0));

        var e1 = mgr.Spawn("A", 1f, 1f, 0f, diverter, new PortId(0));
        diverter.TryAccept(e1, new PortId(0));
        diverter.Tick(1f); diverter.Tick(0f);

        var e2 = mgr.Spawn("B", 1f, 1f, 0f, diverter, new PortId(0));
        diverter.TryAccept(e2, new PortId(0));
        diverter.Tick(1f); diverter.Tick(0f);

        Assert.Null(diverter.Occupant);
        Assert.Same(exit1, e2.CurrentComponent);
    }

    [Fact]
    public void RoundRobin_ThirdEntity_GoesToOutPort0Again()
    {
        var graph    = new RoutingGraph();
        var diverter = MakeDiverter(graph);
        var mgr      = new EntityManager();

        var exit0 = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
        var exit1 = new PackageExit(3, new GridCoord(1,  0), Direction.East,  mgr);
        graph.Connect(diverter, new PortId(1), exit0, new PortId(0));
        graph.Connect(diverter, new PortId(2), exit1, new PortId(0));

        for (int i = 0; i < 3; i++)
        {
            var e = mgr.Spawn($"E{i}", 1f, 1f, 0f, diverter, new PortId(0));
            diverter.TryAccept(e, new PortId(0));
            diverter.Tick(1f); diverter.Tick(0f);
            exit0.Tick(0f); exit1.Tick(0f);
        }

        Assert.Equal(2, exit0.TotalProcessed);
        Assert.Equal(1, exit1.TotalProcessed);
    }

    [Fact]
    public void RoundRobin_BlockedFirstOutput_EntityWaitsAndDoesNotSkip()
    {
        var graph    = new RoutingGraph();
        var diverter = MakeDiverter(graph);
        var mgr      = new EntityManager();

        // exit0 è collegato ma occupato — usiamo un conveyor già pieno
        var blocker   = new OneWayConveyor(2, new GridCoord(0, -1), Direction.North, 1f, graph);
        var occupying = mgr.Spawn("X", 1f, 1f, 0f, blocker, new PortId(0));
        blocker.TryAccept(occupying, new PortId(0));

        graph.Connect(diverter, new PortId(1), blocker, new PortId(0));
        // porta 2 non connessa

        var e1 = mgr.Spawn("A", 1f, 1f, 0f, diverter, new PortId(0));
        diverter.TryAccept(e1, new PortId(0));
        diverter.Tick(1f); diverter.Tick(0f);

        // Output attivo (port1) è bloccato → l'entità resta sul diverter
        Assert.Same(e1, diverter.Occupant);
    }

    // ── Schema / ApplyConfig / ExportProperties ──

    [Fact]
    public void Schema_ExposesRoutingAsConfigurableEnum()
    {
        var routing = DiverterLogic.Schema.Single(p => p.Key == "routing");

        Assert.Equal(PropertyType.Enum, routing.Type);
        Assert.False(routing.IsReadOnly);
        Assert.Contains(RoutingRuleFactory.RoundRobin, routing.EnumValues!);
        Assert.Equal(RoutingRuleFactory.RoundRobin, routing.DefaultValue);
    }

    [Fact]
    public void ExportProperties_IncludesRoutingKey()
    {
        var diverter = MakeDiverter();

        var exported = diverter.ExportProperties();

        Assert.Equal(RoutingRuleFactory.RoundRobin, exported["routing"]);
    }

    [Fact]
    public void ApplyConfig_AcceptsKnownRoutingRule()
    {
        var diverter = MakeDiverter();

        var error = diverter.ApplyConfig(new Dictionary<string, string>
        {
            ["routing"] = RoutingRuleFactory.RoundRobin,
        });

        Assert.Null(error);
        Assert.IsType<RoundRobinRoutingRule>(diverter.Rule);
    }

    [Fact]
    public void ApplyConfig_RejectsUnknownRoutingRule()
    {
        var diverter = MakeDiverter();
        var original = diverter.Rule;

        var error = diverter.ApplyConfig(new Dictionary<string, string>
        {
            ["routing"] = "minimum_load",
        });

        Assert.NotNull(error);
        Assert.Same(original, diverter.Rule);   // unchanged on validation failure
    }

    [Fact]
    public void ApplyConfig_SameRoutingRule_PreservesInternalState()
    {
        // Reapplying the same strategy must NOT reset the rule (e.g. the round-robin counter).
        var graph    = new RoutingGraph();
        var diverter = MakeDiverter(graph);
        var originalRule = diverter.Rule;

        var error = diverter.ApplyConfig(new Dictionary<string, string>
        {
            ["routing"] = RoutingRuleFactory.RoundRobin,
        });

        Assert.Null(error);
        Assert.Same(originalRule, diverter.Rule);
    }
}
