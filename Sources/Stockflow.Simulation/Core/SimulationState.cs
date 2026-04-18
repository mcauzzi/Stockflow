using Stockflow.Simulation.Component;

namespace Stockflow.Simulation.Core;

public class SimulationState
{
    public SimulationState()
    {
        Components = new();
    }

    public List<ISimComponent> Components { get; }
}
