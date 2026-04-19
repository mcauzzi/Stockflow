using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;

namespace Stockflow.Simulation.Core;

public class SimulationState
{
    public SimulationState()
    {
        Components = new();
        Entities   = new();
    }

    public List<ISimComponent> Components { get; }
    public EntityManager       Entities   { get; }
}
