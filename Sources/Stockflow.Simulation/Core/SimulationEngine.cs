using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;

namespace Stockflow.Simulation.Core;

public class SimulationEngine
{
    private readonly Dictionary<Type, Func<ICommand, int, ISimComponent>> _placementFactories;
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

        _placementFactories = new()
        {
            [typeof(PlaceOneWayConveyorCommand)] = (c, id) =>
            {
                var x = (PlaceOneWayConveyorCommand)c;
                return new OneWayConveyor(id, x.Position, x.Facing, x.Speed, Graph);
            },
            [typeof(PlaceConveyorTurnCommand)] = (c, id) =>
            {
                var x = (PlaceConveyorTurnCommand)c;
                return new ConveyorTurn(id, x.Position, x.Facing, x.Turn, x.Speed, Graph);
            },
            [typeof(PlacePackageGeneratorCommand)] = (c, id) =>
            {
                var x = (PlacePackageGeneratorCommand)c;
                return new PackageGenerator(id, x.Position, x.Facing,
                                            x.SpawnRate, x.Sku, x.Weight, x.Size,
                                            Graph, State.Entities);
            },
            [typeof(PlacePackageExitCommand)] = (c, id) =>
            {
                var x = (PlacePackageExitCommand)c;
                return new PackageExit(id, x.Position, x.Facing, State.Entities);
            },
        };
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

    public CommandResult ProcessCommand(ICommand command)
    {
        if (_placementFactories.TryGetValue(command.GetType(), out var factory))
            return PlaceComponent(command, factory);

        return command switch
        {
            ConfigureComponentCommand cmd => ConfigureComponent(cmd),
            RemoveComponentCommand    cmd => RemoveComponent(cmd),
            _                              => CommandResult.Fail($"Unknown command: {command.GetType().Name}"),
        };
    }

    private CommandResult PlaceComponent(ICommand cmd, Func<ICommand, int, ISimComponent> factory)
    {
        var component = factory(cmd, _nextComponentId++);
        if (!Grid.TryPlace(component))
            return CommandResult.Fail($"Cell {component.Position} is occupied or out of bounds");
        State.Components.Add(component);
        AutoConnect(component);
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
        if (component is OneWayConveyor conv)
        {
            if (cmd.Properties.TryGetValue("speed", out var sp) && float.TryParse(sp, out var speed) && speed > 0)
                conv.Speed = speed;
            return CommandResult.Ok();
        }
        if (component is ConveyorTurn turn)
        {
            if (cmd.Properties.TryGetValue("speed", out var sp) && float.TryParse(sp, out var speed) && speed > 0)
                turn.Speed = speed;
            return CommandResult.Ok();
        }
        return component is null
            ? CommandResult.Fail($"Component {cmd.ComponentId} not found")
            : CommandResult.Fail($"Component {cmd.ComponentId} ({component.Type}) is not configurable");
    }

    private CommandResult RemoveComponent(RemoveComponentCommand cmd)
    {
        var component = State.Components.Find(c => c.Id == cmd.ComponentId);
        if (component is null)
            return CommandResult.Fail($"Component {cmd.ComponentId} not found");

        foreach (var entity in State.Entities.GetByComponent(cmd.ComponentId).ToList())
            State.Entities.Despawn(entity.Id);

        Graph.DisconnectAll(component);
        Grid.TryRemove(component.Position);
        State.Components.Remove(component);
        return CommandResult.Ok();
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
