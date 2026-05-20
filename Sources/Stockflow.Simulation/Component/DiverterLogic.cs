using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Modules;
using Stockflow.Simulation.Routing;

namespace Stockflow.Simulation.Component;

public class DiverterLogic : ISimComponent
{
    public  int                             Id       { get; }
    public  GridCoord                       Position { get; }
    public  Direction                       Facing   { get; }
    public  TurnSide                        Side     { get; }
    public  ComponentType                   Type     => ComponentType.DiverterLogic;
    public  IReadOnlyList<IComponentModule> Modules  { get; }
    public  SimEntity?                      Occupant { get; private set; }
    public  IReadOnlyList<Port>             Ports    { get; }
    public  float                           Speed    { get; set; }
    public  RoutingGraph                    Graph    { get; }
    public  IRoutingRule                    Rule     { get; private set; }

    private readonly Port    _inPort;
    private readonly Port    _outPort0;  // dritto
    private readonly Port    _outPort1;  // laterale (sinistra o destra in base a Side)
    private readonly PortId[] _outputPorts;

    private static readonly PortId _portIn   = new(0);
    private static readonly PortId _portOut0 = new(1);
    private static readonly PortId _portOut1 = new(2);

    public DiverterLogic(int id, GridCoord position, Direction facing, TurnSide side, float speed,
                         RoutingGraph graph, IRoutingRule? rule = null,
                         IReadOnlyList<IComponentModule>? modules = null)
    {
        Id       = id;
        Position = position;
        Facing   = facing;
        Side     = side;
        Speed    = speed;
        Graph    = graph;
        Rule     = rule ?? new RoundRobinRoutingRule();
        Modules  = modules ?? [];

        var lateralDir = side == TurnSide.Right ? facing.RotateCW() : facing.RotateCCW();
        _inPort      = new(_portIn,   Position + facing.Opposite().ToOffset(), PortDirection.In);
        _outPort0    = new(_portOut0, Position + facing.ToOffset(),             PortDirection.Out);
        _outPort1    = new(_portOut1, Position + lateralDir.ToOffset(),         PortDirection.Out);
        _outputPorts = [_portOut0, _portOut1];
        Ports        = [_inPort, _outPort0, _outPort1];
    }

    // --- ConfigSchema ---

    public static readonly PropertySchema[] Schema =
    [
        new("speed",   "Speed (m/s)",  PropertyType.Float, DefaultValue: "1",                       Min: "0.01", Max: "10"),
        new("routing", "Routing Rule", PropertyType.Enum,  DefaultValue: RoutingRuleFactory.RoundRobin,
            EnumValues: RoutingRuleFactory.AvailableRules),
        new("side",    "Lateral Side", PropertyType.Enum,  DefaultValue: "right",
            EnumValues: ["left", "right"], IsReadOnly: true),
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
                case "routing":
                    var newKey = value.ToLowerInvariant();
                    if (RoutingRuleFactory.KeyOf(Rule) != newKey)
                        Rule = RoutingRuleFactory.Create(newKey);
                    break;
            }
        }
        return null;
    }

    public Dictionary<string, string> ExportProperties() => new()
    {
        ["speed"]   = Speed.ToString("F3"),
        ["routing"] = RoutingRuleFactory.KeyOf(Rule),
        ["side"]    = Side == TurnSide.Right ? "right" : "left",
    };

    public void Tick(float deltaTime)
    {
        if (Occupant == null) return;

        if (Occupant.Progress < 1.0f)
        {
            Occupant.Progress += Speed * deltaTime;
            return;
        }

        var targetPort = Rule.SelectOutput(Occupant, _outputPorts);
        var next       = Graph.GetNext(this, targetPort);
        if (next == null) return;

        if (next.Value.To.TryAccept(Occupant, next.Value.ToPort))
        {
            foreach (var module in Modules)
                module.OnEntityExit(Occupant);
            Rule.OnTransferSucceeded(targetPort);
            Occupant = null;
        }
    }

    public bool TryAccept(SimEntity entity, PortId fromPort)
    {
        if (Occupant != null) return false;

        Occupant                = entity;
        entity.CurrentComponent = this;
        entity.CurrentPort      = fromPort;
        entity.Progress         = 0.0f;

        foreach (var module in Modules)
            module.OnEntityEnter(entity);

        return true;
    }
}
