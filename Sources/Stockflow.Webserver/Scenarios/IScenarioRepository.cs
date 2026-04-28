namespace Stockflow.Webserver.Scenarios;

public interface IScenarioRepository
{
    IEnumerable<ScenarioSummary> List();
    Scenario?                    Get(string id);
    void                         Create(Scenario scenario);
    void                         Update(Scenario scenario);
    bool                         Delete(string id);
}

public sealed class ScenarioNotFoundException(string id)
    : Exception($"Scenario '{id}' not found");

public sealed class ScenarioAlreadyExistsException(string id)
    : Exception($"Scenario '{id}' already exists");

public sealed class InvalidScenarioIdException(string id)
    : Exception($"Scenario id '{id}' is invalid (allowed: a-z A-Z 0-9 . _ - up to 64 chars)");
