using System.Collections.Concurrent;
using Stockflow.Simulation.Commands;

namespace Stockflow.Webserver.Queue;

/// <summary>
/// Thread-safe queue for commands originating from REST endpoints.
/// The simulation tick loop drains it at the start of each tick,
/// mirroring the existing WebSocket <see cref="WebSocket.IClientCommandQueue"/> pattern.
/// </summary>
public interface IRestCommandQueue
{
    void Enqueue(ICommand command);
    bool TryDequeue(out ICommand command);
}

public sealed class RestCommandQueue : IRestCommandQueue
{
    private readonly ConcurrentQueue<ICommand> _queue = new();

    public void Enqueue(ICommand command) => _queue.Enqueue(command);

    public bool TryDequeue(out ICommand command) => _queue.TryDequeue(out command!);
}
