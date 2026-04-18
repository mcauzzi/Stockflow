namespace Stockflow.Simulation;

public class SimulationEngine
{
    private const float BaseTickDuration = 1.0f / 20; // 20 Hz

    public SimulationEngine(int width, int length, int height)
    {
        State     = new(width, length, height);
        Timescale = 1;
    }
    public float Timescale { get; set; }
    public SimulationState State { get; private set; }
    public void Tick()
    {
        var deltaTime = BaseTickDuration * Timescale;
        foreach (var entity in State.Components)
        {
            entity.Tick(deltaTime);
        }
    }
}