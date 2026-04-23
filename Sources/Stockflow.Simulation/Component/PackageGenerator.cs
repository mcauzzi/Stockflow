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

    public void Tick(float deltaTime)
    {
        _simTime += deltaTime;

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

        _accumulated += deltaTime;
        if (_accumulated < 1f / SpawnRate) return;

        _accumulated -= 1f / SpawnRate;
        Occupant = _entities.Spawn(Sku, Weight, Size, _simTime, this, _outPort.Id);
    }

    // Generators are source-only — nothing enters them
    public bool TryAccept(SimEntity entity, PortId fromPort) => false;
}
