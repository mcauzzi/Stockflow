using System.Collections.Concurrent;
using Stockflow.Protocol.Messages;

namespace Stockflow.Webserver.WebSocket;

/// <summary>
/// Producer/consumer hand-off between the WebSocket receive loops (producers) and the
/// simulation tick loop (consumer). Architecture doc §10.1 specifies a
/// <c>ConcurrentQueue&lt;(ClientSession, ClientMessage)&gt;</c> drained at the top of every tick
/// — this class just formalises that contract as a DI-friendly singleton.
/// </summary>
public interface IClientCommandQueue
{
    void Enqueue(ClientSession session, ClientMessage message);
    bool TryDequeue(out (ClientSession Session, ClientMessage Message) item);
    int  Count { get; }
}

public sealed class ClientCommandQueue : IClientCommandQueue
{
    private readonly ConcurrentQueue<(ClientSession, ClientMessage)> _queue = new();

    public void Enqueue(ClientSession session, ClientMessage message)
        => _queue.Enqueue((session, message));

    public bool TryDequeue(out (ClientSession Session, ClientMessage Message) item)
    {
        if (_queue.TryDequeue(out var tuple))
        {
            item = tuple;
            return true;
        }
        item = default;
        return false;
    }

    public int Count => _queue.Count;
}
