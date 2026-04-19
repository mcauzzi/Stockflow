using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;

namespace Stockflow.Simulation.Core;

public class SimulationEngine
{
    private HashSet<int> _knownComponentIds = new();
    private HashSet<int> _knownEntityIds    = new();

    public SimulationEngine(int width, int length, int height)
    {
        Clock = new SimulationClock();
        Grid  = new GridManager(width, length, height);
        State = new();
    }

    public SimulationClock Clock          { get; }
    public float           TimeScale      { get => Clock.TimeScale; set => Clock.TimeScale = value; }
    public float           SimulationTime => Clock.SimulatedTime;
    public GridManager     Grid           { get; }
    public SimulationState State          { get; }

    // deltaTime è calcolato dal caller: 1f / tickRate * engine.TimeScale
    public void Tick(float deltaTime)
    {
        Clock.Advance(deltaTime);
        foreach (var component in State.Components)
            component.Tick(deltaTime);
    }

    // I comandi concreti vengono aggiunti man mano che vengono implementati (#33)
    public CommandResult ProcessCommand(ICommand command)
    {
        return CommandResult.Fail($"Unknown command: {command.GetType().Name}");
    }

    // Ritorna le differenze rispetto all'ultima chiamata — verrà espanso con delta completo (#8)
    public StateDelta GetStateDelta()
    {
        var currentComponents = State.Components.Select(c => c.Id).ToHashSet();
        var addedComponents   = currentComponents.Except(_knownComponentIds).ToList();
        var removedComponents = _knownComponentIds.Except(currentComponents).ToList();
        _knownComponentIds = currentComponents;

        var currentEntities = State.Entities.GetAll().ToDictionary(e => e.Id);
        var addedEntities   = currentEntities.Keys.Except(_knownEntityIds)
                                             .Select(id => EntityState.From(currentEntities[id]))
                                             .ToList();
        var removedEntities = _knownEntityIds.Except(currentEntities.Keys).ToList();
        _knownEntityIds = currentEntities.Keys.ToHashSet();

        return new StateDelta
        {
            SimulationTime      = Clock.SimulatedTime,
            AddedComponentIds   = addedComponents,
            RemovedComponentIds = removedComponents,
            AddedEntityStates   = addedEntities,
            RemovedEntityIds    = removedEntities,
        };
    }
}
