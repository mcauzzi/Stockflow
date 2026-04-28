using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Core;
using Stockflow.Simulation.Grid;

namespace Stockflow.Tests.Simulation;

public class LoadScenarioCommandTests
{
    private static SimulationEngine MakeEngine() => new(10, 10, 1);

    [Fact]
    public void Load_ChangesGridDimensions()
    {
        var engine = MakeEngine();

        var result = engine.ProcessCommand(new LoadScenarioCommand(
            Width:     20,
            Length:    15,
            Floors:    2,
            Preplaced: []));

        Assert.True(result.Success);
        Assert.Equal(20, engine.Grid.Width);
        Assert.Equal(15, engine.Grid.Length);
        Assert.Equal(2,  engine.Grid.Height);
    }

    [Fact]
    public void Load_ClearsPreviousComponentsAndEntities()
    {
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageGeneratorCommand(
            new GridCoord(0, 0), Direction.North, SpawnRate: 1f));
        engine.Tick(1f);  // spawn an entity

        Assert.NotEmpty(engine.State.Components);
        Assert.NotEmpty(engine.State.Entities.Active);

        engine.ProcessCommand(new LoadScenarioCommand(5, 5, 1, []));

        Assert.Empty(engine.State.Components);
        Assert.Empty(engine.State.Entities.Active);
    }

    [Fact]
    public void Load_ResetsSimulationTime_PreservesTimeScale()
    {
        var engine = MakeEngine();
        engine.TimeScale = 5f;
        engine.Tick(2.5f);

        Assert.Equal(2.5f, engine.SimulationTime, precision: 5);

        engine.ProcessCommand(new LoadScenarioCommand(5, 5, 1, []));

        Assert.Equal(0f, engine.SimulationTime);
        Assert.Equal(5f, engine.TimeScale);  // velocità di playback è preferenza utente
    }

    [Fact]
    public void Load_PlacesPreplacedComponentsAtCorrectPositions()
    {
        var engine = MakeEngine();

        engine.ProcessCommand(new LoadScenarioCommand(10, 10, 1,
        [
            new PlacePackageGeneratorCommand(new GridCoord(0, 0), Direction.East),
            new PlaceOneWayConveyorCommand(new GridCoord(1, 0), Direction.East),
            new PlacePackageExitCommand(new GridCoord(2, 0), Direction.East),
        ]));

        Assert.Equal(3, engine.State.Components.Count);
        Assert.IsType<PackageGenerator>(engine.State.Components[0]);
        Assert.IsType<OneWayConveyor>(engine.State.Components[1]);
        Assert.IsType<PackageExit>(engine.State.Components[2]);
        Assert.Equal(new GridCoord(0, 0), engine.State.Components[0].Position);
        Assert.Equal(new GridCoord(1, 0), engine.State.Components[1].Position);
        Assert.Equal(new GridCoord(2, 0), engine.State.Components[2].Position);
    }

    [Fact]
    public void Load_WithEmptyPreplaced_Succeeds()
    {
        var engine = MakeEngine();

        var result = engine.ProcessCommand(new LoadScenarioCommand(8, 8, 1, []));

        Assert.True(result.Success);
        Assert.Empty(engine.State.Components);
    }

    [Theory]
    [InlineData( 0, 10, 1)]
    [InlineData(10,  0, 1)]
    [InlineData(10, 10, 0)]
    [InlineData(-1, 10, 1)]
    public void Load_WithNonPositiveDimensions_Fails(int width, int length, int floors)
    {
        var engine = MakeEngine();

        var result = engine.ProcessCommand(new LoadScenarioCommand(width, length, floors, []));

        Assert.False(result.Success);
        Assert.Contains("positive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WithOutOfBoundsPreplaced_Fails()
    {
        var engine = MakeEngine();

        var result = engine.ProcessCommand(new LoadScenarioCommand(5, 5, 1,
        [
            new PlacePackageGeneratorCommand(new GridCoord(99, 99), Direction.North),
        ]));

        Assert.False(result.Success);
        Assert.Contains("Preplaced", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_FollowedByGetStateDelta_ReportsOldIdsRemoved_NewIdsAdded()
    {
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageGeneratorCommand(
            new GridCoord(0, 0), Direction.North));
        var oldId = engine.State.Components[0].Id;
        engine.GetStateDelta();  // baseline: oldId è ora "known"

        engine.ProcessCommand(new LoadScenarioCommand(10, 10, 1,
        [
            new PlacePackageExitCommand(new GridCoord(2, 2), Direction.East),
        ]));

        var delta = engine.GetStateDelta();
        var newId = engine.State.Components[0].Id;

        Assert.Contains(oldId, delta.RemovedComponentIds);
        Assert.Contains(newId, delta.AddedComponentIds);
        Assert.NotEqual(oldId, newId);  // ID monotonici, no collision
    }

    [Fact]
    public void Load_AfterLoad_GridIsFreshForPlacements()
    {
        // Verifica che le celle del vecchio grid siano davvero rilasciate:
        // se non lo fossero, una placement sulla stessa coord fallirebbe.
        var engine = MakeEngine();
        engine.ProcessCommand(new PlacePackageGeneratorCommand(
            new GridCoord(3, 3), Direction.North));

        engine.ProcessCommand(new LoadScenarioCommand(10, 10, 1, []));

        var result = engine.ProcessCommand(new PlacePackageGeneratorCommand(
            new GridCoord(3, 3), Direction.North));

        Assert.True(result.Success);
    }
}
