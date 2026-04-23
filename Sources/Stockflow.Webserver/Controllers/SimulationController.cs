using Microsoft.AspNetCore.Mvc;
using Stockflow.Protocol.Messages;
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Core;
using SimComponentType = Stockflow.Simulation.Component.ComponentType;
using ISimComponent = Stockflow.Simulation.Component.ISimComponent;

namespace Stockflow.Webserver.Controllers;

/// <summary>
/// REST façade for simulation control. The Angular console uses these endpoints instead of
/// encoding MessagePack/LZ4 commands over the WebSocket — the WebSocket channel remains
/// receive-only for the web client (state deltas + events).
///
/// Thread safety: <see cref="SimulationEngine.TimeScale"/> is a plain float; reading/writing
/// it from a REST handler while the tick loop runs is safe on modern x86/ARM (aligned float
/// writes are atomic). Grid-mutating commands are intentionally deferred to #33 where a
/// proper lock or command-queue pattern will be introduced.
/// </summary>
[ApiController]
[Route("api/sim")]
public sealed class SimulationController(SimulationEngine engine) : ControllerBase
{
    // Mirrors SimulationHostedService.TimeScaleBySpeed — keep in sync until #33 extracts it.
    private static readonly float[] TimeScaleBySpeed = [0f, 1f, 2f, 5f, 10f, 1f];

    // ── GET /api/sim/state ────────────────────────────────────────────────────
    // Full snapshot as JSON. Used by the Angular client for initial load and manual resyncs.
    // Components list is snapshotted with ToList() to avoid TOCTOU with the tick loop.
    [HttpGet("state")]
    public IActionResult GetState()
    {
        var components = engine.State.Components.ToList();
        var entities   = engine.State.Entities.Active;

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
                kind       = KindString(c.Type),
                gridX      = c.Position.X,
                gridY      = c.Position.Y,
                facing     = c.Facing.ToString(),
                occupant   = c.Occupant?.Id,
                properties = BuildProperties(c),
            }),
            entityCount = entities.Count,
        });
    }

    // ── GET /api/sim/metrics ──────────────────────────────────────────────────
    // Basic counters derivable from engine state. Full KPI metrics (throughput, SLA, etc.)
    // are computed by the metrics subsystem tracked in a future milestone.
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        var components = engine.State.Components.ToList();
        var gridCells  = engine.Grid.Width * engine.Grid.Length;

        return Ok(new
        {
            simulationTime   = engine.SimulationTime,
            timeScale        = engine.TimeScale,
            entityCount      = engine.State.Entities.Active.Count,
            componentCount   = components.Count,
            warehouseSaturation = gridCells > 0
                ? Math.Round((double)components.Count / gridCells * 100, 1)
                : 0.0,
        });
    }

    // ── POST /api/sim/speed ───────────────────────────────────────────────────
    // Body: { "speed": 0‥5 }   (SimSpeed ordinal: 0=Paused 1=Normal 2=Fast 3=Faster 4=UltraFast 5=Live)
    [HttpPost("speed")]
    public IActionResult ChangeSpeed([FromBody] ChangeSpeedRequest req)
    {
        if (req.Speed is < 0 or > 5)
            return BadRequest(new { success = false, errorMessage = "speed must be 0‥5" });

        engine.TimeScale = TimeScaleBySpeed[req.Speed];
        return Ok(new { success = true, speed = req.Speed, timeScale = engine.TimeScale });
    }

    // ── POST /api/sim/components ──────────────────────────────────────────────
    // Place a new component. Deferred to #33 — returns 501 with an explanation.
    [HttpPost("components")]
    public IActionResult PlaceComponent() =>
        StatusCode(StatusCodes.Status501NotImplemented, new
        {
            success = false,
            errorMessage = "PlaceComponent not yet implemented — tracked in issue #33.",
        });

    // ── DELETE /api/sim/components/{id} ───────────────────────────────────────
    [HttpDelete("components/{id:int}")]
    public IActionResult RemoveComponent(int id) =>
        StatusCode(StatusCodes.Status501NotImplemented, new
        {
            success = false,
            errorMessage = $"RemoveComponent({id}) not yet implemented — tracked in issue #33.",
        });

    // ── PATCH /api/sim/components/{id} ────────────────────────────────────────
    // Body: { "properties": { "key": "value" } }
    [HttpPatch("components/{id:int}")]
    public IActionResult ConfigureComponent(int id) =>
        StatusCode(StatusCodes.Status501NotImplemented, new
        {
            success = false,
            errorMessage = $"ConfigureComponent({id}) not yet implemented — tracked in issue #33.",
        });

    // ─────────────────────────────────────────────────────────────────────────

    private static string KindString(SimComponentType type) => type switch
    {
        SimComponentType.OneWayConveyor   => ComponentKinds.OneWayConveyor,
        SimComponentType.ConveyorTurn     => "conveyor_turn",
        SimComponentType.PackageGenerator => "package_generator",
        SimComponentType.PackageExit      => "package_exit",
        _                                 => type.ToString().ToLowerInvariant(),
    };

    private static Dictionary<string, string>? BuildProperties(ISimComponent c)
    {
        if (c is PackageGenerator gen)
            return new()
            {
                ["spawnRate"] = gen.SpawnRate.ToString("F3"),
                ["sku"]       = gen.Sku,
                ["weight"]    = gen.Weight.ToString("F3"),
                ["size"]      = gen.Size.ToString("F3"),
                ["enabled"]   = gen.IsEnabled ? "true" : "false",
            };
        if (c is PackageExit exit)
            return new()
            {
                ["totalProcessed"]     = exit.TotalProcessed.ToString(),
                ["throughput"]         = exit.Throughput.ToString("F3"),
                ["avgFulfillmentTime"] = exit.AvgFulfillmentTime.ToString("F3"),
            };
        return null;
    }
}

public sealed record ChangeSpeedRequest(int Speed);
