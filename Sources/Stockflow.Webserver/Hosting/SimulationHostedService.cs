using System.Diagnostics;
using Microsoft.Extensions.Options;
using Stockflow.Protocol.Messages;
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Core;
using Stockflow.Webserver.Configuration;
using Stockflow.Webserver.WebSocket;
using SimEntityState   = Stockflow.Simulation.Entity.EntityState;
using SimComponentType = Stockflow.Simulation.Component.ComponentType;
using ProtoEntityStatus = Stockflow.Protocol.Messages.EntityStatus;
using ProtoDirection    = Stockflow.Protocol.Messages.Direction;

namespace Stockflow.Webserver.Hosting;

public sealed class SimulationHostedService : BackgroundService
{
    // SimSpeed ordinal → TimeScale multiplier (matches SimSpeed enum order)
    private static readonly float[] TimeScaleBySpeed = [0f, 1f, 2f, 5f, 10f, 1f];

    private readonly SimulationEngine    _engine;
    private readonly IClientCommandQueue _queue;
    private readonly WebSocketHandler    _wsHandler;
    private readonly int                 _tickRate;
    private readonly ILogger<SimulationHostedService> _logger;
    private readonly Stopwatch           _wallClock = Stopwatch.StartNew();

    public SimulationHostedService(
        SimulationEngine                  engine,
        IClientCommandQueue               queue,
        WebSocketHandler                  wsHandler,
        IOptions<ServerConfig>            config,
        ILogger<SimulationHostedService>  logger)
    {
        _engine    = engine;
        _queue     = queue;
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
            await DrainCommandQueueAsync(stoppingToken).ConfigureAwait(false);

            _engine.Tick(1f / _tickRate * _engine.TimeScale);

            var delta = _engine.GetStateDelta();
            await _wsHandler.BroadcastAsync(BuildDeltaMessage(delta), stoppingToken)
                            .ConfigureAwait(false);
        }

        _logger.LogInformation("Simulation tick loop stopped");
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
                _engine.TimeScale = TimeScaleBySpeed[(int)changeSpeed.Speed];
                await session.SendAsync(Ack(msg.CommandId), ct).ConfigureAwait(false);
                return;

            default:
                // Concrete command translation is implemented in #33
                await session.SendAsync(
                    Nack(msg.CommandId, $"Not implemented: {msg.GetType().Name}"),
                    ct).ConfigureAwait(false);
                return;
        }
    }

    private StateDeltaMessage BuildDeltaMessage(StateDelta delta)
    {
        var byId = _engine.State.Components.ToDictionary(c => c.Id);
        return new StateDeltaMessage
        {
            ServerTime          = ServerTime,
            SimulationTime      = delta.SimulationTime,
            TimeScale           = _engine.TimeScale,
            CreatedEntities     = [..delta.AddedEntityStates.Select(ToProtoEntity)],
            UpdatedEntities     = [..delta.UpdatedEntityStates.Select(ToProtoEntity)],
            RemovedEntityIds    = [..delta.RemovedEntityIds],
            CreatedComponents   = [..delta.AddedComponentIds
                .Where(byId.ContainsKey)
                .Select(id => ToProtoComponent(byId[id]))],
            RemovedComponentIds = [..delta.RemovedComponentIds],
        };
    }

    private FullStateMessage BuildFullStateMessage() => new()
    {
        ServerTime     = ServerTime,
        SimulationTime = _engine.SimulationTime,
        TimeScale      = _engine.TimeScale,
        Components     = [.._engine.State.Components.Select(ToProtoComponent)],
        Entities       = [.._engine.State.Entities.Active.Values
            .Select(e => ToProtoEntity(SimEntityState.From(e)))],
    };

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

    private static Protocol.Messages.EntityState ToProtoEntity(SimEntityState s) => new()
    {
        Id       = s.Id,
        Sku      = s.Sku,
        Position = Vector3.Zero, // world-space interpolation: future milestone
        Status   = (ProtoEntityStatus)(int)s.Status,
    };

    private static ComponentState ToProtoComponent(ISimComponent c) => new()
    {
        Id     = c.Id,
        Kind   = KindString(c.Type),
        GridX  = c.Position.X,
        GridY  = c.Position.Y,
        Facing = (ProtoDirection)(int)c.Facing,
    };

    private static string KindString(SimComponentType type) => type switch
    {
        SimComponentType.OneWayConveyor => ComponentKinds.OneWayConveyor,
        _                               => type.ToString(),
    };
}
