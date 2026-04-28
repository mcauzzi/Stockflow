using Stockflow.Simulation.Core;

namespace Stockflow.Webserver.Sessions;

public sealed class SessionManager(SimulationEngine engine) : ISessionManager
{
    private readonly object _lock = new();

    private Guid?         _id;
    private SessionStatus _status;
    private DateTime      _startedAt;
    private DateTime?     _endedAt;
    private string?       _scenarioId;

    public SessionInfo? Current
    {
        get
        {
            lock (_lock)
            {
                return _id is null ? null : BuildInfo();
            }
        }
    }

    public SessionInfo Start()
    {
        lock (_lock)
        {
            if (_id is not null && _status == SessionStatus.Running)
                throw new SessionAlreadyActiveException(_id.Value);

            _id         = Guid.NewGuid();
            _status     = SessionStatus.Running;
            _startedAt  = DateTime.UtcNow;
            _endedAt    = null;
            _scenarioId = null;
            return BuildInfo();
        }
    }

    public bool Terminate(Guid id)
    {
        lock (_lock)
        {
            if (_id != id || _status != SessionStatus.Running) return false;
            _status  = SessionStatus.Terminated;
            _endedAt = DateTime.UtcNow;
            return true;
        }
    }

    public bool TryAttachScenario(Guid id, string scenarioId)
    {
        lock (_lock)
        {
            if (_id != id || _status != SessionStatus.Running) return false;
            _scenarioId = scenarioId;
            return true;
        }
    }

    private SessionInfo BuildInfo() => new(
        Id:             _id!.Value,
        Status:         _status,
        StartedAt:      _startedAt,
        EndedAt:        _endedAt,
        ScenarioId:     _scenarioId,
        SimulationTime: engine.SimulationTime);
}
