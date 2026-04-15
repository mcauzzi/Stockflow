namespace Stockflow.Simulation;

public class SimulationState
{
    public SimulationState()
    {
        Entities = new();
    }
    
    public List<Entity> Entities { get;  set; } = new();
}