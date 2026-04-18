using Stockflow.Simulation.Component;
using Stockflow.Simulation.Grid;

namespace Stockflow.Simulation;

public class SimulationState
{
    public SimulationState(int width, int length, int height)
    {
        Grid       = new GridManager(width, length, height);
        Components = new();
    }

    public GridManager         Grid       { get; }
    public List<ISimComponent> Components { get; }
}