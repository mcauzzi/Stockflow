namespace Stockflow.Simulation.Core;

public enum SimulationEventType
{
    EntityTransferred,  // un'entità è passata da un componente al successivo
    ConveyorJammed,     // un'entità non ha potuto uscire per capacità piena
}

// Evento discreto generato durante un tick — incluso nel delta per audit/animazioni client
public sealed class SimulationEvent
{
    public SimulationEventType Type        { get; init; }
    public int                 EntityId    { get; init; }
    public int?                ComponentId { get; init; }
}
