using Stockflow.Simulation.Component;

namespace Stockflow.Simulation.Entity;

public interface ISimEntity
{
    public int Id { get; init; }
    
    // Dove si trova
    public ISimComponent CurrentComponent { get; set; }
    public PortId        CurrentPort      { get; set; }
    public float         Progress         { get; set; }          // 0.0 ingresso → 1.0 uscita
    
    // Dove deve andare
    public ISimComponent?         DestinationComponent { get; set; }
    
    // Quando è entrata
    public float EntryTime { get; init; }
}