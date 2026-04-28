namespace Stockflow.Webserver.Sessions;

/// <summary>
/// F1A: modello a singola sessione attiva. Il SimulationEngine resta un singleton
/// globale, qui teniamo solo i metadati ciclo-vita della sessione corrente.
/// Multi-session è scope F2.
/// </summary>
public interface ISessionManager
{
    SessionInfo? Current { get; }
    SessionInfo  Start();
    bool         Terminate(Guid id);
    bool         TryAttachScenario(Guid id, string scenarioId);
}

public sealed class SessionAlreadyActiveException(Guid currentId)
    : Exception($"A session ({currentId}) is already running. Terminate it before starting a new one.")
{
    public Guid CurrentId { get; } = currentId;
}
