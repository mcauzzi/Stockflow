using System.Diagnostics;
using Microsoft.Extensions.Options;
using Stockflow.Protocol.Messages;
using Stockflow.Simulation.Commands;
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Core;
using Stockflow.Simulation.Grid;
using Stockflow.Webserver.Configuration;
using Stockflow.Webserver.Queue;
using Stockflow.Webserver.Serialization;
using Stockflow.Webserver.WebSocket;
using SimEntityState    = Stockflow.Simulation.Entity.EntityState;
using SimComponentType  = Stockflow.Simulation.Component.ComponentType;
using SimDirection      = Stockflow.Simulation.Component.Direction;
using ProtoEntityStatus = Stockflow.Protocol.Messages.EntityStatus;
using ProtoDirection    = Stockflow.Protocol.Messages.Direction;

namespace Stockflow.Webserver.Hosting;

public sealed class SimulationHostedService : BackgroundService
{
    private readonly SimulationEngine    _engine;
    private readonly IClientCommandQueue _queue;
    private readonly IRestCommandQueue   _restQueue;
    private readonly WebSocketHandler    _wsHandler;
    private readonly int                 _tickRate;
    private readonly ILogger<SimulationHostedService> _logger;
    private readonly Stopwatch           _wallClock = Stopwatch.StartNew();

    public SimulationHostedService(
        SimulationEngine                  engine,
        IClientCommandQueue               queue,
        IRestCommandQueue                 restQueue,
        WebSocketHandler                  wsHandler,
        IOptions<ServerConfig>            config,
        ILogger<SimulationHostedService>  logger)
    {
        _engine    = engine;
        _queue     = queue;
        _restQueue = restQueue;
        _wsHandler = wsHandler;
        _tickRate  = config.Value.TickRate;
        _logger    = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / _tickRate));

        _logger.LogInformation("Simulation tick loop started at {TickRate} Hz", _tickRate);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            DrainRestQueue();
            await DrainCommandQueueAsync(stoppingToken).ConfigureAwait(false);

            _engine.Tick(1f / _tickRate * _engine.TimeScale);

            var delta = _engine.GetStateDelta();
            await _wsHandler.BroadcastAsync(BuildDeltaMessage(delta), stoppingToken)
                            .ConfigureAwait(false);
        }

        _logger.LogInformation("Simulation tick loop stopped");
    }

    private void DrainRestQueue()
    {
        while (_restQueue.TryDequeue(out var cmd))
            _engine.ProcessCommand(cmd);
    }

    private async ValueTask DrainCommandQueueAsync(CancellationToken ct)
    {
        while (_queue.TryDequeue(out var item))
            await HandleMessageAsync(item.Session, item.Message, ct).ConfigureAwait(false);
    }

    private async ValueTask HandleMessageAsync(ClientSession session, ClientMessage msg, CancellationToken ct)
    {
        switch (msg)
        {
            case RequestFullStateMessage:
                await session.SendAsync(BuildFullStateMessage(), ct).ConfigureAwait(false);
                return;

            case ChangeSpeedMessage changeSpeed:
                _engine.TimeScale = SpeedTable.TimeScaleFor((int)changeSpeed.Speed);
                await session.SendAsync(Ack(msg.CommandId), ct).ConfigureAwait(false);
                return;

            case PlaceComponentMessage place:
            {
                ICommand? cmd = place.Kind switch
                {
                    ComponentKinds.PackageGenerator => new PlacePackageGeneratorCommand(
                        new GridCoord(place.GridX, place.GridY),
                        (SimDirection)(int)place.Direction),
                    ComponentKinds.PackageExit => new PlacePackageExitCommand(
                        new GridCoord(place.GridX, place.GridY),
                        (SimDirection)(int)place.Direction),
                    _ => null,
                };
                if (cmd is null)
                {
                    await session.SendAsync(Nack(msg.CommandId, $"Unknown component kind: {place.Kind}"), ct).ConfigureAwait(false);
                    return;
                }
                var result = _engine.ProcessCommand(cmd);
                await session.SendAsync(result.Success ? Ack(msg.CommandId) : Nack(msg.CommandId, result.ErrorMessage!), ct).ConfigureAwait(false);
                return;
            }

            case ConfigureComponentMessage configure:
            {
                var result = _engine.ProcessCommand(new ConfigureComponentCommand(
                    configure.ComponentId, configure.Properties));
                await session.SendAsync(result.Success ? Ack(msg.CommandId) : Nack(msg.CommandId, result.ErrorMessage!), ct).ConfigureAwait(false);
                return;
            }

            default:
                await session.SendAsync(
                    Nack(msg.CommandId, $"Not implemented: {msg.GetType().Name}"),
                    ct).ConfigureAwait(false);
                return;
        }
    }

    private StateDeltaMessage BuildDeltaMessage(StateDelta delta)
    {
        var byId = _engine.State.Components.ToDictionary(c => c.Id);

        // Include PackageExit components in every delta so metrics stay current on the client
        var updatedComponents = _engine.State.Components
            .Where(c => c.Type == SimComponentType.PackageExit)
            .Select(ToProtoComponent)
            .ToArray();

        return new StateDeltaMessage
        {
            ServerTime          = ServerTime,
            SimulationTime      = delta.SimulationTime,
            TimeScale           = _engine.TimeScale,
            CreatedEntities     = [..delta.AddedEntityStates.Select(s => ToProtoEntity(s, byId))],
            UpdatedEntities     = [..delta.UpdatedEntityStates.Select(s => ToProtoEntity(s, byId))],
            RemovedEntityIds    = [..delta.RemovedEntityIds],
            CreatedComponents   = [..delta.AddedComponentIds
                .Where(byId.ContainsKey)
                .Select(id => ToProtoComponent(byId[id]))],
            RemovedComponentIds = [..delta.RemovedComponentIds],
            UpdatedComponents   = updatedComponents,
        };
    }

    private FullStateMessage BuildFullStateMessage()
    {
        var byId = _engine.State.Components.ToDictionary(c => c.Id);
        return new()
        {
            ServerTime     = ServerTime,
            SimulationTime = _engine.SimulationTime,
            TimeScale      = _engine.TimeScale,
            Components     = [.._engine.State.Components.Select(ToProtoComponent)],
            Entities       = [.._engine.State.Entities.Active.Values
                .Select(e => ToProtoEntity(SimEntityState.From(e), byId))],
        };
    }

    private CommandResultMessage Ack(int commandId) => new()
    {
        ServerTime = ServerTime,
        CommandId  = commandId,
        Success    = true,
    };

    private CommandResultMessage Nack(int commandId, string reason) => new()
    {
        ServerTime   = ServerTime,
        CommandId    = commandId,
        Success      = false,
        ErrorMessage = reason,
    };

    private float ServerTime => (float)_wallClock.Elapsed.TotalSeconds;

    private static Protocol.Messages.EntityState ToProtoEntity(
        SimEntityState s, IReadOnlyDictionary<int, ISimComponent> byId)
    {
        Vector3 pos = Vector3.Zero;
        if (byId.TryGetValue(s.CurrentComponentId, out var comp))
        {
            // Interpolate 2D grid position: entity moves from cell entry to exit along facing direction.
            var off = comp.Facing.ToOffset();
            pos = new Vector3(
                comp.Position.X + off.X * s.Progress,
                comp.Position.Y + off.Y * s.Progress,
                0f);
        }
        return new()
        {
            Id       = s.Id,
            Sku      = s.Sku,
            Position = pos,
            Status   = (ProtoEntityStatus)(int)s.Status,
        };
    }

    private static ComponentState ToProtoComponent(ISimComponent c) => new()
    {
        Id         = c.Id,
        Kind       = ComponentSerializer.KindString(c.Type),
        GridX      = c.Position.X,
        GridY      = c.Position.Y,
        Facing     = (ProtoDirection)(int)c.Facing,
        Properties = ComponentSerializer.BuildProperties(c),
    };
}
