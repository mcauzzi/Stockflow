using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Modules;
using Stockflow.Simulation.Routing;

namespace Stockflow.Simulation.Component;

public class MergeLogic : ISimComponent
{
    public  int                             Id       { get; }
    public  GridCoord                       Position { get; }
    public  Direction                       Facing   { get; private set; }
    public  ComponentType                   Type     => ComponentType.MergeLogic;
    public  IReadOnlyList<IComponentModule> Modules  { get; }
    public  SimEntity?                      Occupant { get; private set; }
    public  IReadOnlyList<Port>             Ports    => _ports;
    public  float                           Speed    { get; set; }
    public  MergeMode                       Mode     { get; set; }
    public  RoutingGraph                    Graph    { get; }

    private Port   _inPort0;
    private Port   _inPort1;
    private Port   _outPort;
    private Port[] _ports = [];
    private PortId _activePort;
    private int    _stallTicks;
    private const int StallThreshold = 30;

    private static readonly PortId _port0 = new(0);
    private static readonly PortId _port1 = new(1);
    private static readonly PortId _port2 = new(2);

    public MergeLogic(int id, GridCoord position, Direction facing, MergeMode mode, float speed,
                      RoutingGraph graph, IReadOnlyList<IComponentModule>? modules = null)
    {
        Id          = id;
        Position    = position;
        Mode        = mode;
        Speed       = speed;
        Graph       = graph;
        Modules     = modules ?? [];
        _activePort = _port0;
        SetFacing(facing);
    }

    public void SetFacing(Direction facing)
    {
        Facing   = facing;
        _inPort0 = new(_port0, Position + facing.Opposite().ToOffset(),  PortDirection.In);
        _inPort1 = new(_port1, Position + facing.RotateCCW().ToOffset(), PortDirection.In);
        _outPort = new(_port2, Position + facing.ToOffset(),             PortDirection.Out);
        _ports   = [_inPort0, _inPort1, _outPort];
    }

    public void Tick(float deltaTime)
    {
        if (Occupant == null)
        {
            _stallTicks++;
            if (_stallTicks >= StallThreshold)
            {
                _activePort = _activePort == _port0 ? _port1 : _port0;
                _stallTicks = 0;
            }
            return;
        }

        if (Occupant.Progress < 1.0f)
        {
            Occupant.Progress += Speed * deltaTime;
        }
        else
        {
            var next = Graph.GetNext(this, _outPort.Id);
            if (next != null && next.Value.To.TryAccept(Occupant, next.Value.ToPort))
            {
                foreach (var module in Modules)
                    module.OnEntityExit(Occupant);
                Occupant = null;
            }
        }
    }

    public bool TryAccept(SimEntity entity, PortId fromPort)
    {
        if (Occupant != null) return false;
        if (fromPort != _activePort) return false;

        Occupant                = entity;
        entity.CurrentComponent = this;
        entity.CurrentPort      = fromPort;
        entity.Progress         = 0.0f;
        _stallTicks             = 0;

        if (Mode == MergeMode.Alternating)
            _activePort = _activePort == _port0 ? _port1 : _port0;
        else
            _activePort = _port0;

        foreach (var module in Modules)
            module.OnEntityEnter(entity);

        return true;
    }
}
