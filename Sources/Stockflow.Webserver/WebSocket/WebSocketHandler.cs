using System.Collections.Concurrent;
using Stockflow.Protocol.Messages;

namespace Stockflow.Webserver.WebSocket;

/// <summary>
/// Accepts WebSocket upgrade requests, owns the live set of <see cref="ClientSession"/>
/// instances, and provides a single broadcast fan-out point for the simulation tick loop.
/// Registered as a singleton; safe to call from any thread.
/// </summary>
public sealed class WebSocketHandler
{
    private readonly ConcurrentDictionary<Guid, ClientSession> _sessions = new();
    private readonly ILoggerFactory                            _loggerFactory;
    private readonly ILogger<WebSocketHandler>                 _logger;
    private readonly MessageRouter                             _router;

    public WebSocketHandler(
        MessageRouter              router,
        ILoggerFactory             loggerFactory,
        ILogger<WebSocketHandler>  logger)
    {
        _router        = router;
        _loggerFactory = loggerFactory;
        _logger        = logger;
    }

    public int ConnectedClientCount => _sessions.Count;

    /// <summary>
    /// Handle an incoming WebSocket request: accept the upgrade, register a session, and
    /// block until the connection closes. Call from an ASP.NET request delegate.
    /// </summary>
    public async Task HandleAsync(HttpContext context, CancellationToken applicationStopping)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var session = new ClientSession(socket, _loggerFactory.CreateLogger<ClientSession>());

        _sessions[session.Id] = session;
        _logger.LogInformation(
            "Client {SessionId} connected from {Remote} (total={Count})",
            session.Id,
            context.Connection.RemoteIpAddress,
            _sessions.Count);

        try
        {
            await session.RunAsync(_router.RouteAsync, applicationStopping).ConfigureAwait(false);
        }
        finally
        {
            _sessions.TryRemove(session.Id, out _);
            await session.DisposeAsync().ConfigureAwait(false);
            _logger.LogInformation(
                "Client {SessionId} disconnected (total={Count})",
                session.Id,
                _sessions.Count);
        }
    }

    /// <summary>
    /// Fan-out a server message to every connected client. Drops sends that fail without
    /// aborting the others — a slow or dead client must not stall the sim tick.
    /// </summary>
    public async ValueTask BroadcastAsync(ServerMessage message, CancellationToken cancellationToken = default)
    {
        foreach (var session in _sessions.Values)
        {
            try
            {
                await session.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Broadcast to session {SessionId} failed; continuing",
                    session.Id);
            }
        }
    }
}
