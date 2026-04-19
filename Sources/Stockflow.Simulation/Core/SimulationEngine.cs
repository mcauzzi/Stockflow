using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Grid;

namespace Stockflow.Simulation.Core;

public class SimulationEngine
{
    private HashSet<int> _knownComponentIds = new();

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

    // Ritorna le differenze rispetto all'ultima chiamata — verrà espanso con entità (#6, #8)
    public StateDelta GetStateDelta()
    {
        var current = State.Components.Select(c => c.Id).ToHashSet();

        var added   = current.Except(_knownComponentIds).ToList();
        var removed = _knownComponentIds.Except(current).ToList();

        _knownComponentIds = current;

        return new StateDelta
        {
            SimulationTime      = Clock.SimulatedTime,
            AddedComponentIds   = added,
            RemovedComponentIds = removed,
        };
    }
}
