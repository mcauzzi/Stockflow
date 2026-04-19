using Stockflow.Simulation.Entity;

namespace Stockflow.Simulation.Core;

// Delta tra due tick consecutivi — struttura fondamentale per comunicazione server→client
public sealed class StateDelta
{
    public float                         SimulationTime      { get; init; }
    public IReadOnlyList<int>            AddedComponentIds   { get; init; } = [];
    public IReadOnlyList<int>            RemovedComponentIds { get; init; } = [];
    public IReadOnlyList<EntityState>    AddedEntityStates   { get; init; } = [];
    public IReadOnlyList<EntityState>    UpdatedEntityStates { get; init; } = [];
    public IReadOnlyList<int>            RemovedEntityIds    { get; init; } = [];
    public IReadOnlyList<SimulationEvent> Events             { get; init; } = [];
}
