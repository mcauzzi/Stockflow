using System.Net.WebSockets;
using System.Threading.Channels;
using MessagePack;
using Stockflow.Protocol.Messages;
using Stockflow.Protocol.Serialization;

namespace Stockflow.Webserver.WebSocket;

/// <summary>
/// One connected client. Owns the <see cref="System.Net.WebSockets.WebSocket"/>, the receive
/// loop and a single-writer send channel. The send channel serialises outbound frames so that
/// the simulation thread and any REST-triggered broadcast can call <see cref="SendAsync"/>
/// concurrently without stepping on each other (a <see cref="System.Net.WebSockets.WebSocket"/>
/// only allows one outstanding send at a time).
/// </summary>
public sealed class ClientSession : IAsyncDisposable
{
    private const int ReceiveBufferSize = 16 * 1024;

    private readonly System.Net.WebSockets.WebSocket _socket;
    private readonly Channel<ServerMessage>         _outbound;
    private readonly ILogger<ClientSession>         _logger;
    private readonly CancellationTokenSource        _lifetime = new();

    private Task? _sendLoop;

    public ClientSession(System.Net.WebSockets.WebSocket socket, ILogger<ClientSession> logger)
    {
        _socket   = socket;
        _logger   = logger;
        _outbound = Channel.CreateUnbounded<ServerMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Closed when the socket has been fully drained (receive + send loops ended).</summary>
    public CancellationToken ClosedToken => _lifetime.Token;

    /// <summary>
    /// Queue a message for delivery. Returns immediately; the dedicated send loop picks it up.
    /// Thread-safe — any caller (sim tick, REST endpoint, handler) may invoke this.
    /// </summary>
    public ValueTask SendAsync(ServerMessage message, CancellationToken cancellationToken = default)
        => _outbound.Writer.WriteAsync(message, cancellationToken);

    /// <summary>
    /// Run the receive + send loops until the socket closes or <paramref name="stoppingToken"/>
    /// fires. Returns when both loops have exited; the socket is left in whatever state the
    /// peer (or an exception) produced — <see cref="DisposeAsync"/> handles the final close.
    /// </summary>
    public async Task RunAsync(
        Func<ClientSession, ReadOnlyMemory<byte>, ValueTask> onBinaryFrame,
        CancellationToken stoppingToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _lifetime.Token);

        _sendLoop = Task.Run(() => SendLoopAsync(linked.Token), linked.Token);

        try
        {
            await ReceiveLoopAsync(onBinaryFrame, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            _outbound.Writer.TryComplete();
            _lifetime.Cancel();
            try { if (_sendLoop is not null) await _sendLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    private async Task ReceiveLoopAsync(
        Func<ClientSession, ReadOnlyMemory<byte>, ValueTask> onBinaryFrame,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var assembled = new MemoryStream();

        while (!cancellationToken.IsCancellationRequested
               && _socket.State == WebSocketState.Open)
        {
            assembled.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "peer closed",
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                assembled.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Binary)
            {
                _logger.LogWarning("Session {SessionId}: ignoring non-binary frame ({Type})",
                    Id, result.MessageType);
                continue;
            }

            try
            {
                await onBinaryFrame(this, assembled.ToArray()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session {SessionId}: frame handler threw", Id);
            }
        }
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in _outbound.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_socket.State != WebSocketState.Open) return;

                var payload = MessagePackSerializer.Serialize(message, MessagePackConfig.Options, cancellationToken);
                await _socket.SendAsync(
                    payload,
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId}: send loop failed", Id);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _outbound.Writer.TryComplete();
        _lifetime.Cancel();

        try
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "server shutdown",
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch { /* socket may already be dead */ }

        _socket.Dispose();
        _lifetime.Dispose();
    }
}
