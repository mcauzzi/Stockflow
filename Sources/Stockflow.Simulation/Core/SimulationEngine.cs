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
            [typeof(PlaceMergeLogicCommand)] = (c, id) =>
            {
                var x = (PlaceMergeLogicCommand)c;
                return new MergeLogic(id, x.Position, x.Facing, x.Mode, x.Speed, Graph);
            },
        };
    }

    public SimulationClock Clock          { get; }
    public float           TimeScale      { get => Clock.TimeScale; set => Clock.TimeScale = value; }
    public float           SimulationTime => Clock.SimulatedTime;
    public GridManager     Grid           { get; private set; }
    public RoutingGraph    Graph          { get; private set; }
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
            LoadScenarioCommand       cmd => LoadScenario(cmd),
            _                              => CommandResult.Fail($"Unknown command: {command.GetType().Name}"),
        };
    }

    private CommandResult LoadScenario(LoadScenarioCommand cmd)
    {
        if (cmd.Width <= 0 || cmd.Length <= 0 || cmd.Floors <= 0)
            return CommandResult.Fail($"Scenario dimensions must be positive (got {cmd.Width}x{cmd.Length}x{cmd.Floors})");

        ResetAndLoad(cmd.Width, cmd.Length, cmd.Floors);

        for (var i = 0; i < cmd.Preplaced.Count; i++)
        {
            var pre    = cmd.Preplaced[i];
            var result = ProcessCommand(pre);
            if (!result.Success)
                return CommandResult.Fail($"Preplaced component {i} ({pre.GetType().Name}) failed: {result.ErrorMessage}");
        }

        return CommandResult.Ok();
    }

    // Wipe lo stato del mondo lasciando crescere i counter ID monotonici:
    // così GetStateDelta riporta correttamente le vecchie ID come rimosse e
    // le nuove come aggiunte, senza collisioni cross-scenario.
    private void ResetAndLoad(int width, int length, int floors)
    {
        Grid  = new GridManager(width, length, floors);
        Graph = new RoutingGraph();
        State.Components.Clear();
        State.Entities.Reset();
        Clock.Reset();
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
        if (component is null)
            return CommandResult.Fail($"Component {cmd.ComponentId} not found");

        // --- Special case: facing change requires RoutingGraph surgery ---
        if (cmd.Properties.TryGetValue("facing", out var facingStr) &&
            Enum.TryParse<Direction>(facingStr, ignoreCase: true, out var newDir) &&
            newDir != component.Facing)
        {
            switch (component)
            {
                case MergeLogic merge:
                    Graph.DisconnectAll(merge);
                    merge.SetFacing(newDir);
                    AutoConnect(merge);
                    break;
                default:
                    return CommandResult.Fail($"Component type {component.Type} does not support runtime facing change");
            }
        }

        // --- Generic schema-driven config for all other properties ---
        var propsToApply = cmd.Properties
            .Where(kv => kv.Key != "facing")
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        if (propsToApply.Count == 0)
            return CommandResult.Ok();

        var error = component.ApplyConfig(propsToApply);
        return error is null ? CommandResult.Ok() : CommandResult.Fail(error);
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
