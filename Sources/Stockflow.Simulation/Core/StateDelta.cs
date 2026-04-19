using Stockflow.Simulation.Entity;

namespace Stockflow.Simulation.Core;

// Delta tra due tick consecutivi — verrà espanso con delta completo (#8)
public sealed class StateDelta
{
    public float                    SimulationTime      { get; init; }
    public IReadOnlyList<int>       AddedComponentIds   { get; init; } = [];
    public IReadOnlyList<int>       RemovedComponentIds { get; init; } = [];
    public IReadOnlyList<EntityState> AddedEntityStates   { get; init; } = [];
    public IReadOnlyList<int>       RemovedEntityIds    { get; init; } = [];
}