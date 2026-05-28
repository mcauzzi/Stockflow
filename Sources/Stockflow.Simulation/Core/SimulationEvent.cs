namespace Stockflow.Simulation.Core;

public enum SimulationEventType
{
    EntityTransferred,  // un'entità è passata da un componente al successivo
    ConveyorJammed,     // un'entità non ha potuto uscire per capacità piena
    ComponentError,     // il Tick di un componente ha lanciato un'eccezione (isolata dal loop)
}

// Evento discreto generato durante un tick — incluso nel delta per audit/animazioni client
public sealed class SimulationEvent
{
    public SimulationEventType Type        { get; init; }
    public int                 EntityId    { get; init; }
    public int?                ComponentId { get; init; }
    // Dettaglio testuale opzionale (es. messaggio d'eccezione per ComponentError).
    public string?             Message     { get; init; }
}
