using System;
using Stockflow.Simulation.Core;
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

    private readonly EntityManager        _entities;
    private readonly Port                 _inPort;
    private readonly Func<float>?         _getSimTime;
    private readonly Queue<float>         _recentCompletionTimes = new();
    private readonly List<SimulationEvent> _pendingEvents = new();
    private          float                _simTime;
    private          float                _totalFulfillmentTime;

    public int                             Id            { get; }
    public GridCoord                       Position      { get; }
    public Direction                       Facing        { get; }
    public ComponentType                   Type          => ComponentType.PackageExit;
    public IReadOnlyList<IComponentModule> Modules       { get; }
    public SimEntity?                      Occupant      { get; private set; }
    public IReadOnlyList<Port>             Ports         { get; }
    public IReadOnlyList<SimulationEvent>  PendingEvents => _pendingEvents;

    private float CurrentSimTime => _getSimTime?.Invoke() ?? _simTime;

    // Read-only metrics visible to the frontend
    public int   TotalProcessed     { get; private set; }
    public float Throughput         => _recentCompletionTimes.Count > 0
                                           ? _recentCompletionTimes.Count / MathF.Min(CurrentSimTime, ThroughputWindow)
                                           : 0f;
    public float AvgFulfillmentTime => TotalProcessed > 0
                                           ? _totalFulfillmentTime / TotalProcessed
                                           : 0f;

    public PackageExit(int id, GridCoord position, Direction facing,
                       EntityManager entities,
                       Func<float>? getSimTime = null,
                       IReadOnlyList<IComponentModule>? modules = null)
    {
        Id          = id;
        Position    = position;
        Facing      = facing;
        _entities   = entities;
        _getSimTime = getSimTime;
        Modules     = modules ?? [];
        _inPort     = new(new(0), Position + Facing.Opposite().ToOffset(), PortDirection.In);
        Ports       = [_inPort];
    }

    // --- ConfigSchema ---

    public static readonly PropertySchema[] Schema =
    [
        new("totalProcessed",     "Total Processed",         PropertyType.Int,   IsReadOnly: true),
        new("throughput",         "Throughput (pcs/s)",       PropertyType.Float, IsReadOnly: true),
        new("avgFulfillmentTime", "Avg Fulfillment Time (s)", PropertyType.Float, IsReadOnly: true),
    ];

    public IReadOnlyList<PropertySchema> ConfigSchema => Schema;

    public string? ApplyConfig(IReadOnlyDictionary<string, string> properties)
    {
        // PackageExit has no writable properties — all are computed metrics.
        return null;
    }

    public Dictionary<string, string> ExportProperties() => new()
    {
        ["totalProcessed"]     = TotalProcessed.ToString(),
        ["throughput"]         = Throughput.ToString("F3"),
        ["avgFulfillmentTime"] = AvgFulfillmentTime.ToString("F3"),
    };

    public void Tick(float deltaTime)
    {
        _pendingEvents.Clear();
        _simTime += deltaTime;

        if (Occupant == null) return;

        var now         = CurrentSimTime;
        var fulfillment = now - Occupant.EntryTime;
        TotalProcessed++;
        _totalFulfillmentTime += fulfillment;
        _recentCompletionTimes.Enqueue(now);

        // Trim completions that have left the rolling window
        while (_recentCompletionTimes.Count > 0 && now - _recentCompletionTimes.Peek() > ThroughputWindow)
            _recentCompletionTimes.Dequeue();

        _pendingEvents.Add(new SimulationEvent
        {
            Type        = SimulationEventType.EntityTransferred,
            EntityId    = Occupant.Id,
            ComponentId = Id,
        });

        foreach (var m in Modules)
            m.OnEntityExit(Occupant);

        _entities.Despawn(Occupant.Id);
        Occupant = null;
    }

    public bool SetFacing(Direction newFacing) => false;

    public bool TryAccept(SimEntity entity, PortId fromPort)
    {
        if (Occupant != null) return false;
        Occupant                = entity;
        entity.CurrentComponent = this;
        entity.CurrentPort      = fromPort;
        entity.Progress         = 0f;
        foreach (var m in Modules)
            m.OnEntityEnter(entity);
        return true;
    }
}
