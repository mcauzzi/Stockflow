using Stockflow.Simulation.Core;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Modules;
using Stockflow.Simulation.Routing;

namespace Stockflow.Simulation.Component;

public class ConveyorTurn : ISimComponent
{
    public  int                             Id       { get; }
    public  GridCoord                       Position { get; }
    public  Direction                       Facing   { get; private set; }
    public  ComponentType                   Type     => ComponentType.ConveyorTurn;
    public  IReadOnlyList<IComponentModule> Modules  { get; }
    public  SimEntity?                      Occupant { get; private set; }
    private Port                            InPort   { get; set; }
    private Port                            OutPort  { get; set; }
    public  IReadOnlyList<Port>             Ports    { get; private set; }
    public  float                           Speed    { get; set; }
    public  TurnSide                        Turn     { get; set; }
    public  RoutingGraph                    Graph    { get; }

    private readonly List<SimulationEvent>  _pendingEvents = new();
    public  IReadOnlyList<SimulationEvent>  PendingEvents  => _pendingEvents;

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
        InPort   = default;
        OutPort  = default;
        Ports    = [];
        RebuildPorts();
    }

    private void RebuildPorts()
    {
        var exitFacing = Turn == TurnSide.Right ? Facing.RotateCW() : Facing.RotateCCW();
        InPort  = new(new(0), Position + Facing.Opposite().ToOffset(), PortDirection.In);
        OutPort = new(new(1), Position + exitFacing.ToOffset(),        PortDirection.Out);
        Ports   = [InPort, OutPort];
    }

    public bool SetFacing(Direction newFacing)
    {
        Facing = newFacing;
        RebuildPorts();
        return true;
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
        _pendingEvents.Clear();
        if (Occupant == null) return;
        if (!SimMath.ProgressComplete(Occupant.Progress))
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
                    _pendingEvents.Add(new SimulationEvent
                    {
                        Type        = SimulationEventType.EntityTransferred,
                        EntityId    = Occupant.Id,
                        ComponentId = Id,
                    });
                    foreach (var module in Modules)
                        module.OnEntityExit(Occupant);
                    Occupant = null;
                }
                else
                {
                    _pendingEvents.Add(new SimulationEvent
                    {
                        Type        = SimulationEventType.ConveyorJammed,
                        EntityId    = Occupant.Id,
                        ComponentId = Id,
                    });
                }
            }
            else
            {
                _pendingEvents.Add(new SimulationEvent
                {
                    Type        = SimulationEventType.ConveyorJammed,
                    EntityId    = Occupant.Id,
                    ComponentId = Id,
                });
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
