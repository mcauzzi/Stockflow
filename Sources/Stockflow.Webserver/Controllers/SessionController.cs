using Microsoft.AspNetCore.Mvc;
using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Grid;
using Stockflow.Webserver.Queue;
using Stockflow.Webserver.Scenarios;
using Stockflow.Webserver.Serialization;
using Stockflow.Webserver.Sessions;

namespace Stockflow.Webserver.Controllers;

/// <summary>
/// Lifecycle della sessione di simulazione (modello F1A: singola sessione attiva).
/// Le operazioni mutative sull'engine vengono enqueueate al tick loop via
/// <see cref="IRestCommandQueue"/>.
/// </summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionController(
    ISessionManager              sessions,
    IScenarioRepository          scenarios,
    IRestCommandQueue            queue,
    ILogger<SessionController>   logger) : ControllerBase
{
    [HttpPost]
    public IActionResult Start()
    {
        try
        {
            var info = sessions.Start();
            logger.LogInformation("POST /api/sessions → started {Id}", info.Id);
            return CreatedAtAction(nameof(Get), new { id = info.Id }, info);
        }
        catch (SessionAlreadyActiveException ex)
        {
            logger.LogWarning("POST /api/sessions → 409 already active ({Id})", ex.CurrentId);
            return Conflict(new { errorMessage = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id)
    {
        var current = sessions.Current;
        if (current is null || current.Id != id)
        {
            logger.LogDebug("GET /api/sessions/{Id} → 404", id);
            return NotFound(new { errorMessage = $"Session '{id}' not found" });
        }
        return Ok(current);
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Terminate(Guid id)
    {
        if (!sessions.Terminate(id))
        {
            logger.LogWarning("DELETE /api/sessions/{Id} → 404", id);
            return NotFound(new { errorMessage = $"Session '{id}' not found or already terminated" });
        }
        logger.LogInformation("DELETE /api/sessions/{Id} → terminated", id);
        return NoContent();
    }

    [HttpPost("{id:guid}/scenario/load")]
    public IActionResult LoadScenario(Guid id, [FromBody] LoadScenarioRequest req)
    {
        var current = sessions.Current;
        if (current is null || current.Id != id || current.Status != SessionStatus.Running)
        {
            logger.LogWarning("POST /api/sessions/{Id}/scenario/load → 404 session", id);
            return NotFound(new { errorMessage = $"Session '{id}' not found or not running" });
        }

        Scenario? scenario;
        try
        {
            scenario = scenarios.Get(req.ScenarioId);
        }
        catch (InvalidScenarioIdException ex)
        {
            return BadRequest(new { errorMessage = ex.Message });
        }

        if (scenario is null)
        {
            logger.LogWarning("POST /api/sessions/{Id}/scenario/load → 404 scenario '{Sid}'", id, req.ScenarioId);
            return NotFound(new { errorMessage = $"Scenario '{req.ScenarioId}' not found" });
        }

        if (scenario.GridSize is null || scenario.GridSize.Width <= 0 || scenario.GridSize.Height <= 0)
        {
            logger.LogWarning("POST /api/sessions/{Id}/scenario/load → 400 missing/invalid gridSize", id);
            return BadRequest(new { errorMessage = "Scenario must define a positive gridSize" });
        }

        WarnAboutUnsupportedFields(scenario);

        var preplaced = new List<ICommand>();
        if (scenario.PreplacedComponents is not null)
        {
            for (var i = 0; i < scenario.PreplacedComponents.Count; i++)
            {
                var pp  = scenario.PreplacedComponents[i];
                var cmd = MapPreplaced(pp);
                if (cmd is null)
                {
                    logger.LogWarning(
                        "Scenario '{Sid}': skipped preplaced #{Index} unknown type '{Type}'",
                        scenario.Id, i, pp.Type);
                    continue;
                }
                preplaced.Add(cmd);
            }
        }

        queue.Enqueue(new LoadScenarioCommand(
            scenario.GridSize.Width,
            scenario.GridSize.Height,
            Floors:    1,
            Preplaced: preplaced));

        sessions.TryAttachScenario(id, scenario.Id);

        logger.LogInformation(
            "POST /api/sessions/{Id}/scenario/load → enqueued '{Sid}' ({W}x{H}, {N} preplaced)",
            id, scenario.Id, scenario.GridSize.Width, scenario.GridSize.Height, preplaced.Count);

        return AcceptedAtAction(nameof(Get), new { id }, sessions.Current);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static ICommand? MapPreplaced(PreplacedComponent pp)
    {
        if (pp.Position is null || pp.Position.Length < 2) return null;

        var pos = new GridCoord(pp.Position[0], pp.Position[1]);
        var dir = DirectionParser.Parse(pp.Direction);

        return pp.Type?.ToLowerInvariant() switch
        {
            "packagegenerator"  or "package_generator" => new PlacePackageGeneratorCommand(pos, dir),
            "packageexit"       or "package_exit"      => new PlacePackageExitCommand(pos, dir),
            "onewayconveyor"    or "one_way_conveyor" or "conveyor_straight"
                => new PlaceOneWayConveyorCommand(pos, dir),
            "conveyorturn"      or "conveyor_turn" or "conveyor_curve"
                => new PlaceConveyorTurnCommand(pos, dir),
            _ => null,
        };
    }

    private void WarnAboutUnsupportedFields(Scenario s)
    {
        if (s.Budget is not null)
            logger.LogWarning("Scenario '{Id}': budget is set but the simulation does not yet honor it", s.Id);
        if (s.AvailableComponents is { Count: > 0 })
            logger.LogWarning("Scenario '{Id}': availableComponents is set but the simulation does not yet honor it", s.Id);
        if (s.SkuCatalog is not null)
            logger.LogWarning("Scenario '{Id}': skuCatalog is set but the simulation does not yet honor it", s.Id);
        if (s.OrderProfile is not null)
            logger.LogWarning("Scenario '{Id}': orderProfile is set but the simulation does not yet honor it", s.Id);
        if (s.Objectives is { Count: > 0 })
            logger.LogWarning("Scenario '{Id}': objectives is set but the simulation does not yet honor it", s.Id);
        if (s.Duration is not null)
            logger.LogWarning("Scenario '{Id}': duration is set but the simulation does not yet honor it", s.Id);
    }
}

public sealed record LoadScenarioRequest(string ScenarioId);
