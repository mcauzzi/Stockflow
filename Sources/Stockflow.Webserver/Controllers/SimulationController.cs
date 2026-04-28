using Microsoft.AspNetCore.Mvc;
using Stockflow.Protocol.Messages;
using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Core;
using Stockflow.Simulation.Grid;
using Stockflow.Webserver.Configuration;
using Stockflow.Webserver.Queue;
using Stockflow.Webserver.Serialization;

namespace Stockflow.Webserver.Controllers;

/// <summary>
/// REST façade for simulation control. The Angular console uses these endpoints instead of
/// encoding MessagePack/LZ4 commands over the WebSocket — the WebSocket channel remains
/// receive-only for the web client (state deltas + events).
///
/// Thread safety: <see cref="SimulationEngine.TimeScale"/> is a plain float; reading/writing
/// it from a REST handler while the tick loop runs is safe on modern x86/ARM (aligned float
/// writes are atomic). Grid-mutating commands are enqueued via <see cref="IRestCommandQueue"/>
/// and applied atomically at the start of the next simulation tick.
/// </summary>
[ApiController]
[Route("api/sim")]
public sealed class SimulationController(
    SimulationEngine              engine,
    IRestCommandQueue             queue,
    ILogger<SimulationController> logger) : ControllerBase
{
    // ── GET /api/sim/state ────────────────────────────────────────────────────
    [HttpGet("state")]
    public IActionResult GetState()
    {
        var components = engine.State.Components.ToList();
        var entities   = engine.State.Entities.Active;

        logger.LogDebug(
            "GET /api/sim/state → {ComponentCount} components, {EntityCount} entities, T={SimTime:F1}s",
            components.Count, entities.Count, engine.SimulationTime);

        return Ok(new
        {
            simulationTime = engine.SimulationTime,
            timeScale      = engine.TimeScale,
            gridWidth      = engine.Grid.Width,
            gridLength     = engine.Grid.Length,
            gridFloors     = engine.Grid.Height,
            components     = components.Select(c => new
            {
                id         = c.Id,
                kind       = ComponentSerializer.KindString(c.Type),
                gridX      = c.Position.X,
                gridY      = c.Position.Y,
                facing     = c.Facing.ToString(),
                occupant   = c.Occupant?.Id,
                properties = ComponentSerializer.BuildProperties(c),
            }),
            entities = entities.Values.Select(e => new
            {
                id          = e.Id,
                sku         = e.Sku,
                componentId = e.CurrentComponent?.Id,
                progress    = e.Progress,
            }),
        });
    }

    // ── GET /api/sim/metrics ──────────────────────────────────────────────────
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        var components = engine.State.Components.ToList();
        var gridCells  = engine.Grid.Width * engine.Grid.Length;

        logger.LogDebug(
            "GET /api/sim/metrics → {ComponentCount} components, {EntityCount} entities",
            components.Count, engine.State.Entities.Active.Count);

        return Ok(new
        {
            simulationTime      = engine.SimulationTime,
            timeScale           = engine.TimeScale,
            entityCount         = engine.State.Entities.Active.Count,
            componentCount      = components.Count,
            warehouseSaturation = gridCells > 0
                ? Math.Round((double)components.Count / gridCells * 100, 1)
                : 0.0,
        });
    }

    // ── POST /api/sim/speed ───────────────────────────────────────────────────
    [HttpPost("speed")]
    public IActionResult ChangeSpeed([FromBody] ChangeSpeedRequest req)
    {
        if (req.Speed < SpeedTable.MinSpeed || req.Speed > SpeedTable.MaxSpeed)
        {
            logger.LogWarning("POST /api/sim/speed → 400 invalid speed={Speed}", req.Speed);
            return BadRequest(new { success = false, errorMessage = $"speed must be {SpeedTable.MinSpeed}‥{SpeedTable.MaxSpeed}" });
        }

        var previous = engine.TimeScale;
        engine.TimeScale = SpeedTable.TimeScaleFor(req.Speed);

        logger.LogInformation(
            "POST /api/sim/speed → speed={Speed} timeScale={Previous}→{TimeScale}",
            req.Speed, previous, engine.TimeScale);

        return Ok(new { success = true, speed = req.Speed, timeScale = engine.TimeScale });
    }

    // ── POST /api/sim/components ──────────────────────────────────────────────
    [HttpPost("components")]
    public IActionResult PlaceComponent([FromBody] PlaceComponentRequest req)
    {
        var dir = DirectionParser.Parse(req.Facing);
        var pos = new GridCoord(req.GridX, req.GridY);

        ICommand? cmd = req.Kind switch
        {
            ComponentKinds.PackageGenerator => new PlacePackageGeneratorCommand(pos, dir,
                req.SpawnRate ?? 1f,
                req.Sku       ?? "PKG",
                req.Weight    ?? 1f,
                req.Size      ?? 1f),
            ComponentKinds.PackageExit    => new PlacePackageExitCommand(pos, dir),
            ComponentKinds.OneWayConveyor => new PlaceOneWayConveyorCommand(pos, dir, req.Speed ?? 1f),
            ComponentKinds.ConveyorTurn   => new PlaceConveyorTurnCommand(pos, dir,
                req.Turn == "Left" ? TurnSide.Left : TurnSide.Right,
                req.Speed ?? 1f),
            _ => null,
        };

        if (cmd is null)
        {
            logger.LogWarning(
                "POST /api/sim/components → 400 unknown kind={Kind}",
                req.Kind);
            return BadRequest(new { success = false, errorMessage = $"Unknown component kind: {req.Kind}" });
        }

        queue.Enqueue(cmd);

        logger.LogInformation(
            "POST /api/sim/components → enqueued {Kind} at ({X},{Y}) facing={Facing}",
            req.Kind, req.GridX, req.GridY, req.Facing ?? "North");

        return Accepted();
    }

    // ── PUT /api/sim/components/{id} ──────────────────────────────────────────
    [HttpPut("components/{id:int}")]
    public IActionResult ConfigureComponent(int id, [FromBody] Dictionary<string, string> props)
    {
        if (props is null || props.Count == 0)
        {
            logger.LogWarning("PUT /api/sim/components/{Id} → 400 empty properties", id);
            return BadRequest(new { success = false, errorMessage = "Empty properties" });
        }

        queue.Enqueue(new ConfigureComponentCommand(id, props));

        logger.LogInformation(
            "PUT /api/sim/components/{Id} → enqueued configure [{Keys}]",
            id, string.Join(", ", props.Keys));

        return Accepted();
    }

    // ── DELETE /api/sim/components/{id} ───────────────────────────────────────
    [HttpDelete("components/{id:int}")]
    public IActionResult RemoveComponent(int id)
    {
        queue.Enqueue(new RemoveComponentCommand(id));

        logger.LogInformation("DELETE /api/sim/components/{Id} → enqueued remove", id);
        return Accepted();
    }

}

public sealed record ChangeSpeedRequest(int Speed);

public sealed record PlaceComponentRequest(
    string  Kind,
    int     GridX,
    int     GridY,
    string? Facing    = "North",
    float?  SpawnRate = null,
    string? Sku       = null,
    float?  Weight    = null,
    float?  Size      = null,
    string? Turn      = null,
    float?  Speed     = null);
