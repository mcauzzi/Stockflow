using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Modules;
using Stockflow.Simulation.Routing;

namespace Stockflow.Simulation.Component;

public class ConveyorTurn : ISimComponent
{
    public  int                             Id       { get; }
    public  GridCoord                       Position { get; }
    public  Direction                       Facing   { get; }
    public  ComponentType                   Type     => ComponentType.ConveyorTurn;
    public  IReadOnlyList<IComponentModule> Modules  { get; }
    public  SimEntity?                      Occupant { get; private set; }
    private Port                            InPort   { get; }
    private Port                            OutPort  { get; }
    public  IReadOnlyList<Port>             Ports    { get; }
    public  float                           Speed    { get; set; }
    public  TurnSide                        Turn     { get; set; }
    public  RoutingGraph                    Graph    { get; }

    public ConveyorTurn(int id, GridCoord position, Direction facing, TurnSide turn, float speed,
                        RoutingGraph graph, IReadOnlyList<IComponentModule>? modules = null)
    {
        Id       = id;
        Position = position;
        Facing   = facing;
        Turn     = turn;
        Speed    = speed;
        Graph    = graph;
        Modules  = modules ?? [];

        var exitFacing = turn == TurnSide.Right ? facing.RotateCW() : facing.RotateCCW();
        InPort  = new(new(0), Position + facing.Opposite().ToOffset(), PortDirection.In);
        OutPort = new(new(1), Position + exitFacing.ToOffset(),        PortDirection.Out);
        Ports   = [InPort, OutPort];
    }

    // --- ConfigSchema ---

    public static readonly PropertySchema[] Schema =
    [
        new("speed", "Speed (m/s)", PropertyType.Float, DefaultValue: "1",     Min: "0.01", Max: "10"),
        new("turn",  "Turn Side",   PropertyType.Enum,  DefaultValue: "right", EnumValues: ["left", "right"]),
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
                case "speed":
                    Speed = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "turn":
                    Turn = value.Equals("left", StringComparison.OrdinalIgnoreCase)
                               ? TurnSide.Left : TurnSide.Right;
                    break;
            }
        }
        return null;
    }

    public Dictionary<string, string> ExportProperties() => new()
    {
        ["speed"] = Speed.ToString("F3"),
        ["turn"]  = Turn == TurnSide.Right ? "right" : "left",
    };

    public void Tick(float deltaTime)
    {
        if (Occupant == null) return;
        if (Occupant.Progress < 1.0f)
        {
            Occupant.Progress = MathF.Min(Occupant.Progress + Speed * deltaTime, 1.0f);
        }
        else
        {
            var next = Graph.GetNext(this, OutPort.Id);
            if (next != null)
            {
                var nextComp = next.Value.To;
                if (nextComp.TryAccept(Occupant, next.Value.ToPort))
                {
                    foreach (var module in Modules)
                        module.OnEntityExit(Occupant);
                    Occupant = null;
                }
            }
        }
    }

    public bool TryAccept(SimEntity entity, PortId fromPort)
    {
        if (Occupant != null) return false;
        Occupant = entity;
        entity.CurrentComponent = this;
        entity.CurrentPort      = fromPort;
        entity.Progress         = 0.0f;

        foreach (var module in Modules)
            module.OnEntityEnter(entity);

        return true;
    }
}
