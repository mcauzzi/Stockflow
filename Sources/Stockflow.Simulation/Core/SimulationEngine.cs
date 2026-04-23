using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;

namespace Stockflow.Simulation.Core;

public class SimulationEngine
{
    private HashSet<int>                 _knownComponentIds = new();
    private HashSet<int>                 _knownEntityIds    = new();
    private Dictionary<int, EntityState> _lastEntityStates  = new();
    private int                          _nextComponentId   = 1;

    public SimulationEngine(int width, int length, int height)
    {
        Clock = new SimulationClock();
        Grid  = new GridManager(width, length, height);
        Graph = new RoutingGraph();
        State = new();
    }

    public SimulationClock Clock          { get; }
    public float           TimeScale      { get => Clock.TimeScale; set => Clock.TimeScale = value; }
    public float           SimulationTime => Clock.SimulatedTime;
    public GridManager     Grid           { get; }
    public RoutingGraph    Graph          { get; }
    public SimulationState State          { get; }

    // deltaTime è calcolato dal caller: 1f / tickRate * engine.TimeScale
    public void Tick(float deltaTime)
    {
        Clock.Advance(deltaTime);
        foreach (var component in State.Components)
            component.Tick(deltaTime);
    }

    public CommandResult ProcessCommand(ICommand command) => command switch
    {
        PlacePackageGeneratorCommand cmd => PlacePackageGenerator(cmd),
        PlacePackageExitCommand      cmd => PlacePackageExit(cmd),
        ConfigureComponentCommand    cmd => ConfigureComponent(cmd),
        _                                => CommandResult.Fail($"Unknown command: {command.GetType().Name}"),
    };

    private CommandResult PlacePackageGenerator(PlacePackageGeneratorCommand cmd)
    {
        var gen = new PackageGenerator(_nextComponentId++, cmd.Position, cmd.Facing,
                                       cmd.SpawnRate, cmd.Sku, cmd.Weight, cmd.Size,
                                       Graph, State.Entities);
        if (!Grid.TryPlace(gen))
            return CommandResult.Fail($"Cell {cmd.Position} is occupied or out of bounds");
        State.Components.Add(gen);
        AutoConnect(gen);
        return CommandResult.Ok();
    }

    private CommandResult PlacePackageExit(PlacePackageExitCommand cmd)
    {
        var exit = new PackageExit(_nextComponentId++, cmd.Position, cmd.Facing, State.Entities);
        if (!Grid.TryPlace(exit))
            return CommandResult.Fail($"Cell {cmd.Position} is occupied or out of bounds");
        State.Components.Add(exit);
        AutoConnect(exit);
        return CommandResult.Ok();
    }

    private CommandResult ConfigureComponent(ConfigureComponentCommand cmd)
    {
        var component = State.Components.Find(c => c.Id == cmd.ComponentId);
        if (component is PackageGenerator gen)
        {
            if (cmd.Properties.TryGetValue("spawnRate", out var sr) && float.TryParse(sr, out var rate))
                gen.SpawnRate = rate;
            if (cmd.Properties.TryGetValue("sku", out var sku))
                gen.Sku = sku;
            if (cmd.Properties.TryGetValue("weight", out var wStr) && float.TryParse(wStr, out var w))
                gen.Weight = w;
            if (cmd.Properties.TryGetValue("size", out var sStr) && float.TryParse(sStr, out var s))
                gen.Size = s;
            if (cmd.Properties.TryGetValue("enabled", out var en) && bool.TryParse(en, out var enabled))
                gen.IsEnabled = enabled;
            return CommandResult.Ok();
        }
        return component is null
            ? CommandResult.Fail($"Component {cmd.ComponentId} not found")
            : CommandResult.Fail($"Component {cmd.ComponentId} ({component.Type}) is not configurable");
    }

    // When a component is placed, auto-wire it to any adjacent compatible ports.
    private void AutoConnect(ISimComponent newComp)
    {
        foreach (var port in newComp.Ports)
        {
            if (!Grid.TryGetCell(port.Position, out var cell) || cell.Component == null)
                continue;
            var neighbor = cell.Component;

            if (port.Direction == PortDirection.Out)
            {
                foreach (var np in neighbor.Ports)
                    if (np.Direction == PortDirection.In && np.Position == newComp.Position)
                        Graph.Connect(newComp, port.Id, neighbor, np.Id);
            }
            else if (port.Direction == PortDirection.In)
            {
                foreach (var np in neighbor.Ports)
                    if (np.Direction == PortDirection.Out && np.Position == newComp.Position)
                        Graph.Connect(neighbor, np.Id, newComp, port.Id);
            }
        }
    }

    public StateDelta GetStateDelta()
    {
        var currentComponents = State.Components.Select(c => c.Id).ToHashSet();
        var addedComponents   = currentComponents.Except(_knownComponentIds).ToList();
        var removedComponents = _knownComponentIds.Except(currentComponents).ToList();
        _knownComponentIds = currentComponents;

        var currentEntities  = State.Entities.Active;
        var addedEntities    = new List<EntityState>();
        var updatedEntities  = new List<EntityState>();

        foreach (var (id, entity) in currentEntities)
        {
            var snapshot = EntityState.From(entity);
            if (_knownEntityIds.Add(id))
            {
                addedEntities.Add(snapshot);
            }
            else if (_lastEntityStates.TryGetValue(id, out var prev) && prev != snapshot)
            {
                updatedEntities.Add(snapshot);
            }
            _lastEntityStates[id] = snapshot;
        }

        var removedEntities = new List<int>();
        _knownEntityIds.RemoveWhere(id =>
        {
            if (currentEntities.ContainsKey(id)) return false;
            removedEntities.Add(id);
            _lastEntityStates.Remove(id);
            return true;
        });

        return new StateDelta
        {
            SimulationTime      = Clock.SimulatedTime,
            AddedComponentIds   = addedComponents,
            RemovedComponentIds = removedComponents,
            AddedEntityStates   = addedEntities,
            UpdatedEntityStates = updatedEntities,
            RemovedEntityIds    = removedEntities,
        };
    }
}
