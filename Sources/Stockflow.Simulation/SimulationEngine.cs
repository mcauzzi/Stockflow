namespace Stockflow.Simulation;

public class SimulationEngine
{
    public SimulationEngine()
    {
        State     = new();
        Timescale = 1;
    }
    public float Timescale { get; set; }
    public SimulationState State { get; private set; }
    public void Tick()
    {
        foreach (var entity in State.Entities)
        {
            entity.Simulate();
        }
    }
}