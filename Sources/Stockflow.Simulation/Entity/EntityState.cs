using Stockflow.Simulation.Component;

namespace Stockflow.Simulation.Entity;

// Snapshot serializzabile per sincronizzazione di rete — solo tipi primitivi.
// Record per equality strutturale: due snapshot uguali campo per campo sono identici.
public sealed record EntityState
{
    public int          Id                 { get; init; }
    public string       Sku                { get; init; } = "";
    public int          CurrentComponentId { get; init; }
    public PortId       CurrentPort        { get; init; }
    public float        Progress           { get; init; }
    public EntityStatus Status             { get; init; }

    public static EntityState From(SimEntity e) => new()
    {
        Id                 = e.Id,
        Sku                = e.Sku,
        CurrentComponentId = e.CurrentComponent.Id,
        CurrentPort        = e.CurrentPort,
        Progress           = e.Progress,
        Status             = e.Status,
    };
}
