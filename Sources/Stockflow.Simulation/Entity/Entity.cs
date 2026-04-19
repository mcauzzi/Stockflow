using Stockflow.Simulation.Component;

namespace Stockflow.Simulation.Entity;

public class SimEntity
{
    public int     Id        { get; internal set; }
    public string  Sku       { get; internal set; } = "";
    public float   Weight    { get; internal set; }
    public float   Size      { get; internal set; }
    public float   EntryTime { get; internal set; }

    public ISimComponent  CurrentComponent     { get; set; } = null!;
    public PortId         CurrentPort          { get; set; }
    public float          Progress             { get; set; }   // 0.0 ingresso → 1.0 uscita

    public ISimComponent? DestinationComponent { get; set; }

    public EntityStatus Status { get; set; }

    // Rilascia i riferimenti per non trattenere oggetti vivi mentre l'entità è nel pool.
    // I campi value-type vengono riscritti integralmente da Spawn al prossimo riuso.
    internal void Reset()
    {
        Sku                  = "";
        CurrentComponent     = null!;
        DestinationComponent = null;
    }
}
