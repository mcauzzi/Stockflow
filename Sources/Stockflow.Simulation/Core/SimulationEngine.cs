using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;

namespace Stockflow.Simulation.Core;

public class SimulationEngine
{
    private HashSet<int>             _knownComponentIds = new();
    private HashSet<int>             _knownEntityIds    = new();
    private Dictionary<int, EntityState> _lastEntityStates  = new();

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

    public StateDelta GetStateDelta()
    {
        var currentComponents = State.Components.Select(c => c.Id).ToHashSet();
        var addedComponents   = currentComponents.Except(_knownComponentIds).ToList();
        var removedComponents = _knownComponentIds.Except(currentComponents).ToList();
        _knownComponentIds = currentComponents;

        var currentEntities  = State.Entities.Active;
        var addedEntities    = new List<EntityState>();
        var updatedEntities  = new List<EntityState>();

        foreach (var (id, entity) in currentEntities)
        {
            var snapshot = EntityState.From(entity);
            if (_knownEntityIds.Add(id))
            {
                addedEntities.Add(snapshot);
            }
            else if (_lastEntityStates.TryGetValue(id, out var prev) && prev != snapshot)
            {
                updatedEntities.Add(snapshot);
            }
            _lastEntityStates[id] = snapshot;
        }

        var removedEntities = new List<int>();
        _knownEntityIds.RemoveWhere(id =>
        {
            if (currentEntities.ContainsKey(id)) return false;
            removedEntities.Add(id);
            _lastEntityStates.Remove(id);
            return true;
        });

        return new StateDelta
        {
            SimulationTime      = Clock.SimulatedTime,
            AddedComponentIds   = addedComponents,
            RemovedComponentIds = removedComponents,
            AddedEntityStates   = addedEntities,
            UpdatedEntityStates = updatedEntities,
            RemovedEntityIds    = removedEntities,
        };
    }
}
