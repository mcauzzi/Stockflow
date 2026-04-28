using Microsoft.AspNetCore.Mvc;
using Stockflow.Webserver.Scenarios;

namespace Stockflow.Webserver.Controllers;

/// <summary>
/// CRUD sui file scenario (storage filesystem in <c>ContentRoot/Scenarios/</c>).
/// Per ARCHITECTURE §6 gli scenari sono file-based, NON passano per il database.
/// </summary>
[ApiController]
[Route("api/scenarios")]
public sealed class ScenarioController(
    IScenarioRepository         repository,
    ILogger<ScenarioController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        var summaries = repository.List().ToList();
        logger.LogDebug("GET /api/scenarios → {Count} scenarios", summaries.Count);
        return Ok(summaries);
    }

    [HttpGet("{id}")]
    public IActionResult Get(string id)
    {
        try
        {
            var scenario = repository.Get(id);
            if (scenario is null)
            {
                logger.LogDebug("GET /api/scenarios/{Id} → 404", id);
                return NotFound(new { errorMessage = $"Scenario '{id}' not found" });
            }
            return Ok(scenario);
        }
        catch (InvalidScenarioIdException ex)
        {
            logger.LogWarning("GET /api/scenarios/{Id} → 400 invalid id", id);
            return BadRequest(new { errorMessage = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult Create([FromBody] Scenario scenario)
    {
        try
        {
            repository.Create(scenario);
            logger.LogInformation("POST /api/scenarios → created '{Id}'", scenario.Id);
            return CreatedAtAction(nameof(Get), new { id = scenario.Id }, scenario);
        }
        catch (InvalidScenarioIdException ex)
        {
            logger.LogWarning("POST /api/scenarios → 400 invalid id '{Id}'", scenario.Id);
            return BadRequest(new { errorMessage = ex.Message });
        }
        catch (ScenarioAlreadyExistsException ex)
        {
            logger.LogWarning("POST /api/scenarios → 409 '{Id}' already exists", scenario.Id);
            return Conflict(new { errorMessage = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public IActionResult Update(string id, [FromBody] Scenario scenario)
    {
        if (!string.Equals(id, scenario.Id, StringComparison.Ordinal))
        {
            logger.LogWarning("PUT /api/scenarios/{Id} → 400 body id '{BodyId}' mismatch", id, scenario.Id);
            return BadRequest(new { errorMessage = $"Body id '{scenario.Id}' does not match route id '{id}'" });
        }

        try
        {
            repository.Update(scenario);
            logger.LogInformation("PUT /api/scenarios/{Id} → updated", id);
            return Ok(scenario);
        }
        catch (InvalidScenarioIdException ex)
        {
            return BadRequest(new { errorMessage = ex.Message });
        }
        catch (ScenarioNotFoundException ex)
        {
            logger.LogWarning("PUT /api/scenarios/{Id} → 404", id);
            return NotFound(new { errorMessage = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        try
        {
            if (!repository.Delete(id))
            {
                logger.LogWarning("DELETE /api/scenarios/{Id} → 404", id);
                return NotFound(new { errorMessage = $"Scenario '{id}' not found" });
            }
            logger.LogInformation("DELETE /api/scenarios/{Id} → deleted", id);
            return NoContent();
        }
        catch (InvalidScenarioIdException ex)
        {
            return BadRequest(new { errorMessage = ex.Message });
        }
    }
}
