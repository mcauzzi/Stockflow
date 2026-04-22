namespace Stockflow.Simulation.Core;

public class SimulationClock
{
    private float _simulatedTime;

    public float SimulatedTime => _simulatedTime;

    public float TimeScale { get; set; } = 1f;

    // Phase 2: enforced at 1x when external connections are active
    public bool IsLiveMode => TimeScale == 1f;

    // Caller already supplies a pre-scaled delta (1f/tickRate * TimeScale).
    public void Advance(float delta)
    {
        _simulatedTime += delta;
    }
}
