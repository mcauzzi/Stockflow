using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Core;
using Stockflow.Simulation.Grid;
using Stockflow.Webserver.Queue;
using SimDirection = Stockflow.Simulation.Component.Direction;
using SimComponentType = Stockflow.Simulation.Component.ComponentType;

namespace Stockflow.Webserver.Api;

public static class ApiEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/state",                  GetState);
        app.MapPost("/api/components",            PlaceComponent);
        app.MapPut("/api/components/{id:int}",    ConfigureComponent);
        app.MapDelete("/api/components/{id:int}", RemoveComponent);
    }

    // GET /api/state — full snapshot for polling clients
    private static IResult GetState(SimulationEngine engine) =>
        Results.Ok(new
        {
            simulationTime = engine.SimulationTime,
            gridWidth      = engine.Grid.Width,
            gridLength     = engine.Grid.Length,
            components     = engine.State.Components.Select(c => new
            {
                id         = c.Id,
                kind       = KindString(c.Type),
                gridX      = c.Position.X,
                gridY      = c.Position.Y,
                facing     = c.Facing.ToString(),
                properties = GetProperties(c),
            }),
            entities = engine.State.Entities.Active.Values.Select(e => new
            {
                id          = e.Id,
                sku         = e.Sku,
                componentId = e.CurrentComponent?.Id,
                progress    = e.Progress,
            }),
        });

    // POST /api/components — place a new component
    private static async Task<IResult> PlaceComponent(
        HttpRequest       request,
        IRestCommandQueue queue)
    {
        PlaceRequest? body;
        try
        {
            body = await System.Text.Json.JsonSerializer.DeserializeAsync<PlaceRequest>(
                request.Body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return Results.BadRequest("Invalid JSON body"); }

        if (body is null) return Results.BadRequest("Empty body");

        var dir = ParseDirection(body.Facing);
        var pos = new GridCoord(body.GridX, body.GridY);

        ICommand? cmd = body.Kind switch
        {
            "package_generator" => new PlacePackageGeneratorCommand(pos, dir,
                body.SpawnRate ?? 1f,
                body.Sku       ?? "PKG",
                body.Weight    ?? 1f,
                body.Size      ?? 1f),
            "package_exit" => new PlacePackageExitCommand(pos, dir),
            _ => null,
        };

        if (cmd is null) return Results.BadRequest($"Unknown component kind: {body.Kind}");
        queue.Enqueue(cmd);
        return Results.Accepted();
    }

    // PUT /api/components/{id} — configure properties (flat string dict in body)
    private static async Task<IResult> ConfigureComponent(
        int               id,
        HttpRequest       request,
        IRestCommandQueue queue)
    {
        Dictionary<string, string>? props;
        try
        {
            props = await System.Text.Json.JsonSerializer.DeserializeAsync<Dictionary<string, string>>(
                request.Body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return Results.BadRequest("Invalid JSON body"); }

        if (props is null || props.Count == 0) return Results.BadRequest("Empty properties");

        queue.Enqueue(new ConfigureComponentCommand(id, props));
        return Results.Accepted();
    }

    // DELETE /api/components/{id} — not yet implemented in the engine, queued for future
    private static IResult RemoveComponent(int id) =>
        Results.StatusCode(501); // Not Implemented

    // ── helpers ────────────────────────────────────────────────────────────

    private record PlaceRequest(
        string  Kind,
        int     GridX,
        int     GridY,
        string? Facing    = "North",
        float?  SpawnRate = null,
        string? Sku       = null,
        float?  Weight    = null,
        float?  Size      = null);

    private static SimDirection ParseDirection(string? s) => s switch
    {
        "East"  => SimDirection.East,
        "South" => SimDirection.South,
        "West"  => SimDirection.West,
        _       => SimDirection.North,
    };

    private static string KindString(SimComponentType type) => type switch
    {
        SimComponentType.PackageGenerator => "package_generator",
        SimComponentType.PackageExit      => "package_exit",
        SimComponentType.OneWayConveyor   => "conveyor_oneway",
        SimComponentType.ConveyorTurn     => "conveyor_turn",
        _                                 => type.ToString(),
    };

    private static Dictionary<string, string>? GetProperties(ISimComponent c)
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
