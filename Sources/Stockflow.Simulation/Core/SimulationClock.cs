namespace Stockflow.Simulation.Core;

public class SimulationClock
{
    private float _simulatedTime;

    public float SimulatedTime => _simulatedTime;
    public float TimeScale { get; set; } = 1f;

    // Phase 2: enforced at 1x when external connections are active.
    // Must be set explicitly via EnterLiveMode() / ExitLiveMode().
    public bool IsLiveMode { get; private set; }

    public void EnterLiveMode() => IsLiveMode = true;
    public void ExitLiveMode()  => IsLiveMode = false;

    // Caller already supplies a pre-scaled delta (1f/tickRate * TimeScale).
    public void Advance(float delta) => _simulatedTime += delta;

    // Azzera il tempo simulato preservando TimeScale (la velocità di playback
    // appartiene alla preferenza utente, non allo scenario).
    public void Reset()
    {
        _simulatedTime = 0f;
        IsLiveMode     = false;
    }
}
