using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Modules;

namespace Stockflow.Simulation.Component;

/// <summary>
/// Sink component that receives entities and records throughput metrics.
/// Metrics are exposed as properties and serialised into ComponentState.Properties
/// by the webserver so the frontend can display them.
/// </summary>
public class PackageExit : ISimComponent
{
    private const float ThroughputWindow = 10f; // rolling window in seconds

    private readonly EntityManager _entities;
    private readonly Port          _inPort;
    private readonly Queue<float>  _recentCompletionTimes = new();
    private          float         _simTime;
    private          float         _totalFulfillmentTime;

    public int                             Id       { get; }
    public GridCoord                       Position { get; }
    public Direction                       Facing   { get; }
    public ComponentType                   Type     => ComponentType.PackageExit;
    public IReadOnlyList<IComponentModule> Modules  { get; }
    public SimEntity?                      Occupant { get; private set; }
    public IReadOnlyList<Port>             Ports    { get; }

    // Read-only metrics visible to the frontend
    public int   TotalProcessed     { get; private set; }
    public float Throughput         => _recentCompletionTimes.Count > 0
                                           ? _recentCompletionTimes.Count / MathF.Min(_simTime, ThroughputWindow)
                                           : 0f;
    public float AvgFulfillmentTime => TotalProcessed > 0
                                           ? _totalFulfillmentTime / TotalProcessed
                                           : 0f;

    public PackageExit(int id, GridCoord position, Direction facing,
                       EntityManager entities,
                       IReadOnlyList<IComponentModule>? modules = null)
    {
        Id        = id;
        Position  = position;
        Facing    = facing;
        _entities = entities;
        Modules   = modules ?? [];
        _inPort   = new(new(0), Position + Facing.Opposite().ToOffset(), PortDirection.In);
        Ports     = [_inPort];
    }

    public void Tick(float deltaTime)
    {
        _simTime += deltaTime;

        if (Occupant == null) return;

        var fulfillment = _simTime - Occupant.EntryTime;
        TotalProcessed++;
        _totalFulfillmentTime += fulfillment;
        _recentCompletionTimes.Enqueue(_simTime);

        // Trim completions that have left the rolling window
        while (_recentCompletionTimes.Count > 0 && _simTime - _recentCompletionTimes.Peek() > ThroughputWindow)
            _recentCompletionTimes.Dequeue();

        foreach (var m in Modules)
            m.OnEntityExit(Occupant);

        _entities.Despawn(Occupant.Id);
        Occupant = null;
    }

    public bool TryAccept(SimEntity entity, PortId fromPort)
    {
        if (Occupant != null) return false;
        Occupant                = entity;
        entity.CurrentComponent = this;
        entity.CurrentPort      = fromPort;
        entity.Progress         = 0f;
        return true;
    }
}
