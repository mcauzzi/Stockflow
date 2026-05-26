using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Modules;
using Stockflow.Simulation.Routing;

namespace Stockflow.Simulation.Component;

/// <summary>
/// Source component that spawns entities at a configurable rate and pushes them downstream.
/// Parameters can be changed at runtime via ConfigureComponentCommand.
/// </summary>
public class PackageGenerator : ISimComponent
{
    private readonly EntityManager _entities;
    private readonly Port          _outPort;
    private          float         _accumulated;
    private          float         _simTime;

    public int                             Id       { get; }
    public GridCoord                       Position { get; }
    public Direction                       Facing   { get; }
    public ComponentType                   Type     => ComponentType.PackageGenerator;
    public IReadOnlyList<IComponentModule> Modules  { get; }
    public SimEntity?                      Occupant { get; private set; }
    public IReadOnlyList<Port>             Ports    { get; }
    public RoutingGraph                    Graph    { get; }

    // Configurable parameters
    public float  SpawnRate { get; set; }   // entities per second
    public string Sku       { get; set; }
    public float  Weight    { get; set; }
    public float  Size      { get; set; }
    public bool   IsEnabled { get; set; } = true;

    public PackageGenerator(int id, GridCoord position, Direction facing,
                            float spawnRate, string sku, float weight, float size,
                            RoutingGraph graph,
                            EntityManager entities,
                            IReadOnlyList<IComponentModule>? modules = null)
    {
        Id        = id;
        Position  = position;
        Facing    = facing;
        SpawnRate = spawnRate;
        Sku       = sku;
        Weight    = weight;
        Size      = size;
        Graph     = graph;
        _entities = entities;
        Modules   = modules ?? [];
        _outPort  = new(new(0), Position + Facing.ToOffset(), PortDirection.Out);
        Ports     = [_outPort];
    }

    // --- ConfigSchema ---

    public static readonly PropertySchema[] Schema =
    [
        new("spawnRate", "Spawn Rate (pcs/s)", PropertyType.Float,  DefaultValue: "1",    Min: "0.01", Max: "100"),
        new("sku",       "SKU",                PropertyType.String, DefaultValue: "PKG"),
        new("weight",    "Weight (kg)",        PropertyType.Float,  DefaultValue: "1",    Min: "0.01", Max: "1000"),
        new("size",      "Size",               PropertyType.Float,  DefaultValue: "1",    Min: "0.01", Max: "10"),
        new("enabled",   "Enabled",            PropertyType.Bool,   DefaultValue: "true"),
    ];

    public IReadOnlyList<PropertySchema> ConfigSchema => Schema;

    public string? ApplyConfig(IReadOnlyDictionary<string, string> properties)
    {
        foreach (var (key, value) in properties)
        {
            var schema = Schema.FirstOrDefault(s => s.Key == key);
            if (schema is null || schema.IsReadOnly) continue;

            var error = schema.Validate(value);
            if (error is not null) return error;

            switch (key)
            {
                case "spawnRate":
                    SpawnRate = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "sku":
                    Sku = value;
                    break;
                case "weight":
                    Weight = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "size":
                    Size = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "enabled":
                    IsEnabled = bool.Parse(value);
                    break;
            }
        }
        return null;
    }

    public Dictionary<string, string> ExportProperties() => new()
    {
        ["spawnRate"] = SpawnRate.ToString("F3"),
        ["sku"]       = Sku,
        ["weight"]    = Weight.ToString("F3"),
        ["size"]      = Size.ToString("F3"),
        ["enabled"]   = IsEnabled ? "true" : "false",
    };

    public void Tick(float deltaTime)
    {
        // Try to push buffered entity downstream first (natural backpressure)
        if (Occupant != null)
        {
            var next = Graph.GetNext(this, _outPort.Id);
            if (next != null && next.Value.To.TryAccept(Occupant, next.Value.ToPort))
            {
                foreach (var m in Modules)
                    m.OnEntityExit(Occupant);
                Occupant = null;
            }
            return;
        }

        if (!IsEnabled || SpawnRate <= 0f) return;

        _simTime     += deltaTime;
        _accumulated += deltaTime;
        if (_accumulated < 1f / SpawnRate) return;

        _accumulated -= 1f / SpawnRate;
        Occupant = _entities.Spawn(Sku, Weight, Size, _simTime, this, _outPort.Id);
    }

    // Generators are source-only — nothing enters them
    public bool TryAccept(SimEntity entity, PortId fromPort) => false;
}
