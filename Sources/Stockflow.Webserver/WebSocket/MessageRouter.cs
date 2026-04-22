using MessagePack;
using Stockflow.Protocol.Messages;
using Stockflow.Protocol.Serialization;

namespace Stockflow.Webserver.WebSocket;

/// <summary>
/// Deserialises raw binary frames from a <see cref="ClientSession"/> into
/// <see cref="ClientMessage"/> instances and hands them to the
/// <see cref="IClientCommandQueue"/> for the simulation tick to consume. Lives on the hot
/// path: every received frame passes through here, but all heavy lifting (command validation
/// and state mutation) happens later on the sim thread.
/// </summary>
public sealed class MessageRouter
{
    private readonly IClientCommandQueue    _queue;
    private readonly ILogger<MessageRouter> _logger;

    public MessageRouter(IClientCommandQueue queue, ILogger<MessageRouter> logger)
    {
        _queue  = queue;
        _logger = logger;
    }

    public ValueTask RouteAsync(ClientSession session, ReadOnlyMemory<byte> payload)
    {
        ClientMessage? message;
        try
        {
            message = MessagePackSerializer.Deserialize<ClientMessage>(payload, MessagePackConfig.Options);
        }
        catch (MessagePackSerializationException ex)
        {
            _logger.LogWarning(ex,
                "Session {SessionId}: dropping malformed frame ({Bytes} bytes)",
                session.Id, payload.Length);
            return ValueTask.CompletedTask;
        }

        if (message is null)
        {
            _logger.LogWarning("Session {SessionId}: deserialised to null, dropping", session.Id);
            return ValueTask.CompletedTask;
        }

        _queue.Enqueue(session, message);
        return ValueTask.CompletedTask;
    }
}
