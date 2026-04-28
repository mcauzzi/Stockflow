namespace Stockflow.Webserver.Sessions;

public sealed record SessionInfo(
    Guid          Id,
    SessionStatus Status,
    DateTime      StartedAt,
    DateTime?     EndedAt,
    string?       ScenarioId,
    float         SimulationTime);
