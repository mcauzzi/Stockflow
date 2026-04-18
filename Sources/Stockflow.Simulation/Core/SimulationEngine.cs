using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Grid;

namespace Stockflow.Simulation.Core;

public class SimulationEngine
{
    private float        _simulationTime;
    private HashSet<int> _knownComponentIds = new();

    public SimulationEngine(int width, int length, int height)
    {
        Grid  = new GridManager(width, length, height);
        State = new();
    }

    public float           TimeScale      { get; set; } = 1f;
    public float           SimulationTime => _simulationTime;
    public GridManager     Grid           { get; }
    public SimulationState State          { get; }

    // deltaTime è calcolato dal caller: 1f / tickRate * engine.TimeScale
    public void Tick(float deltaTime)
    {
        _simulationTime += deltaTime;
        foreach (var component in State.Components)
            component.Tick(deltaTime);
    }

    // I comandi concreti vengono aggiunti man mano che vengono implementati (#33)
    public CommandResult ProcessCommand(ICommand command)
    {
        return CommandResult.Fail($"Unknown command: {command.GetType().Name}");
    }

    // Ritorna le differenze rispetto all'ultima chiamata — verrà espanso con entità (#6, #8)
    public StateDelta GetStateDelta()
    {
        var current = State.Components.Select(c => c.Id).ToHashSet();

        var added   = current.Except(_knownComponentIds).ToList();
        var removed = _knownComponentIds.Except(current).ToList();

        _knownComponentIds = current;

        return new StateDelta
        {
            SimulationTime      = _simulationTime,
            AddedComponentIds   = added,
            RemovedComponentIds = removed,
        };
    }
}
