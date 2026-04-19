using Stockflow.Simulation.Component;

namespace Stockflow.Simulation.Entity;

public class EntityManager
{
    private readonly Dictionary<int, SimEntity> _active = new();
    private readonly Queue<SimEntity>           _pool   = new();
    private          int                        _nextId = 1;

    public IReadOnlyDictionary<int, SimEntity> Active => _active;

    public IReadOnlyCollection<SimEntity> GetAll() => _active.Values;

    public IEnumerable<SimEntity> GetByComponent(int componentId)
        => _active.Values.Where(e => e.CurrentComponent.Id == componentId);

    public SimEntity Spawn(string sku, float weight, float size, float entryTime,
                           ISimComponent startComponent, PortId startPort)
    {
        var entity = _pool.Count > 0 ? _pool.Dequeue() : new SimEntity();

        entity.Id                   = _nextId++;
        entity.Sku                  = sku;
        entity.Weight               = weight;
        entity.Size                 = size;
        entity.EntryTime            = entryTime;
        entity.CurrentComponent     = startComponent;
        entity.CurrentPort          = startPort;
        entity.Progress             = 0f;
        entity.DestinationComponent = null;
        entity.Status               = EntityStatus.Idle;

        _active[entity.Id] = entity;
        return entity;
    }

    public bool Despawn(int id)
    {
        if (!_active.Remove(id, out var entity)) return false;
        entity.Reset();
        _pool.Enqueue(entity);
        return true;
    }
}
