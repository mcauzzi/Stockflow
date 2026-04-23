using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Core;
using Stockflow.Simulation.Grid;

namespace Stockflow.Tests.Simulation;

public class SimulationEngineCommandTests
{
    private static SimulationEngine MakeEngine() => new(10, 10, 1);

    // ── PlacePackageGenerator ──────────────────────────────────────────────

    [Fact]
    public void PlacePackageGenerator_AddsComponentToStateAndGrid()
    {
        var engine = MakeEngine();
        var cmd    = new PlacePackageGeneratorCommand(new GridCoord(3, 4), Direction.East);

        var result = engine.ProcessCommand(cmd);

        Assert.True(result.Success);
        Assert.Single(engine.State.Components);
        Assert.IsType<PackageGenerator>(engine.State.Components[0]);
        Assert.True(engine.Grid.TryGetCell(new GridCoord(3, 4), out var cell) && cell.IsOccupied);
    }

    [Fact]
    public void PlacePackageGenerator_DefaultParams_AreApplied()
    {
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageGeneratorCommand(new GridCoord(0, 0), Direction.North));

        var gen = (PackageGenerator)engine.State.Components[0];

        Assert.Equal(1f,    gen.SpawnRate);
        Assert.Equal("PKG", gen.Sku);
        Assert.True(gen.IsEnabled);
    }

    [Fact]
    public void PlacePackageGenerator_CustomParams_AreApplied()
    {
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageGeneratorCommand(
            new GridCoord(0, 0), Direction.North,
            SpawnRate: 3f, Sku: "PALLET", Weight: 10f, Size: 2f));

        var gen = (PackageGenerator)engine.State.Components[0];

        Assert.Equal(3f,      gen.SpawnRate);
        Assert.Equal("PALLET", gen.Sku);
        Assert.Equal(10f,     gen.Weight);
        Assert.Equal(2f,      gen.Size);
    }

    [Fact]
    public void PlacePackageGenerator_OnOccupiedCell_Fails()
    {
        var engine = MakeEngine();
        var pos    = new GridCoord(1, 1);
        engine.ProcessCommand(new PlacePackageGeneratorCommand(pos, Direction.North));

        var result = engine.ProcessCommand(new PlacePackageGeneratorCommand(pos, Direction.South));

        Assert.False(result.Success);
        Assert.Single(engine.State.Components);
    }

    // ── PlacePackageExit ──────────────────────────────────────────────────

    [Fact]
    public void PlacePackageExit_AddsComponentToStateAndGrid()
    {
        var engine = MakeEngine();

        var result = engine.ProcessCommand(new PlacePackageExitCommand(new GridCoord(5, 5), Direction.South));

        Assert.True(result.Success);
        Assert.IsType<PackageExit>(engine.State.Components[0]);
        Assert.True(engine.Grid.TryGetCell(new GridCoord(5, 5), out var cell) && cell.IsOccupied);
    }

    // ── ConfigureComponentCommand ─────────────────────────────────────────

    [Fact]
    public void ConfigurePackageGenerator_UpdatesSpawnRate()
    {
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageGeneratorCommand(new GridCoord(0, 0), Direction.North));
        var id = engine.State.Components[0].Id;

        var result = engine.ProcessCommand(new ConfigureComponentCommand(id,
            new Dictionary<string, string> { ["spawnRate"] = "4.5" }));

        Assert.True(result.Success);
        Assert.Equal(4.5f, ((PackageGenerator)engine.State.Components[0]).SpawnRate);
    }

    [Fact]
    public void ConfigurePackageGenerator_UpdatesAllProperties()
    {
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageGeneratorCommand(new GridCoord(0, 0), Direction.North));
        var id = engine.State.Components[0].Id;

        engine.ProcessCommand(new ConfigureComponentCommand(id, new Dictionary<string, string>
        {
            ["spawnRate"] = "2",
            ["sku"]       = "CRATE",
            ["weight"]    = "5",
            ["size"]      = "3",
            ["enabled"]   = "false",
        }));

        var gen = (PackageGenerator)engine.State.Components[0];
        Assert.Equal(2f,     gen.SpawnRate);
        Assert.Equal("CRATE", gen.Sku);
        Assert.Equal(5f,     gen.Weight);
        Assert.Equal(3f,     gen.Size);
        Assert.False(gen.IsEnabled);
    }

    [Fact]
    public void ConfigurePackageGenerator_IgnoresUnrecognisedKeys()
    {
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageGeneratorCommand(new GridCoord(0, 0), Direction.North));
        var id = engine.State.Components[0].Id;

        var result = engine.ProcessCommand(new ConfigureComponentCommand(id,
            new Dictionary<string, string> { ["unknownKey"] = "42" }));

        // Should succeed gracefully — unrecognised keys are silently ignored
        Assert.True(result.Success);
    }

    [Fact]
    public void ConfigureUnknownComponent_ReturnsFail()
    {
        var engine = MakeEngine();

        var result = engine.ProcessCommand(new ConfigureComponentCommand(999,
            new Dictionary<string, string> { ["spawnRate"] = "1" }));

        Assert.False(result.Success);
    }

    [Fact]
    public void ConfigurePackageExit_ReturnsFail()
    {
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageExitCommand(new GridCoord(0, 0), Direction.North));
        var id = engine.State.Components[0].Id;

        // PackageExit has no configurable parameters
        var result = engine.ProcessCommand(new ConfigureComponentCommand(id,
            new Dictionary<string, string> { ["anything"] = "1" }));

        Assert.False(result.Success);
    }

    // ── Auto-connect ──────────────────────────────────────────────────────

    [Fact]
    public void PlaceAdjacentGeneratorAndExit_AutoConnectsRouting()
    {
        // Generator at (1,1) facing North → OutPort.Position = (1,2)
        // Exit      at (1,2) facing North → InPort.Position  = (1,1)
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageGeneratorCommand(new GridCoord(1, 1), Direction.North));
        engine.ProcessCommand(new PlacePackageExitCommand(new GridCoord(1, 2), Direction.North));

        var gen  = (PackageGenerator)engine.State.Components[0];
        var exit = (PackageExit)engine.State.Components[1];

        var connection = engine.Graph.GetNext(gen, gen.Ports[0].Id);

        Assert.NotNull(connection);
        Assert.Same(exit, connection.Value.To);
    }

    [Fact]
    public void PlaceNonAdjacentComponents_DoNotAutoConnect()
    {
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageGeneratorCommand(new GridCoord(0, 0), Direction.North));
        engine.ProcessCommand(new PlacePackageExitCommand(new GridCoord(5, 5), Direction.North));

        var gen = (PackageGenerator)engine.State.Components[0];

        Assert.Null(engine.Graph.GetNext(gen, gen.Ports[0].Id));
    }

    // ── End-to-end tick ──────────────────────────────────────────────────

    [Fact]
    public void Tick_GeneratorConnectedToExit_EntityFlowsAndIsProcessed()
    {
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageGeneratorCommand(
            new GridCoord(1, 1), Direction.North, SpawnRate: 1f));
        engine.ProcessCommand(new PlacePackageExitCommand(new GridCoord(1, 2), Direction.North));

        var exit = (PackageExit)engine.State.Components[1];

        // Tick 1: generator spawns entity
        engine.Tick(1f);
        // Tick 2: generator pushes entity to exit; exit processes it
        engine.Tick(1f);

        Assert.Equal(1, exit.TotalProcessed);
    }
}
