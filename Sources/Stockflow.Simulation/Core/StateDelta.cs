namespace Stockflow.Simulation.Core;

// Delta tra due tick consecutivi — verrà espanso con entità (#6) e delta completo (#8)
public sealed class StateDelta
{
    public float              SimulationTime      { get; init; }
    public IReadOnlyList<int> AddedComponentIds   { get; init; } = [];
    public IReadOnlyList<int> RemovedComponentIds { get; init; } = [];
}