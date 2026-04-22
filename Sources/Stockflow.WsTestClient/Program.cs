using System.Net.WebSockets;
using MessagePack;
using Stockflow.Protocol.Messages;
using Stockflow.Protocol.Serialization;

MessagePackConfig.Initialize();

const string DefaultUrl = "ws://localhost:9600/ws";
var url = args.Length > 0 ? args[0] : DefaultUrl;

using var ws  = new ClientWebSocket();
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"Connecting to {url}...");
try
{
    await ws.ConnectAsync(new Uri(url), cts.Token);
}
catch (Exception ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
    return 1;
}

Console.WriteLine("Connected.\n");
Console.WriteLine("Commands: full | speed <0-5> | quit");
Console.WriteLine("          (0=Paused 1=Normal 2=Fast 3=Faster 4=UltraFast 5=Live)\n");

int nextCommandId = 1;

var receiveTask = ReceiveLoopAsync(ws, cts.Token);
var inputTask   = InputLoopAsync(ws, cts.Token);

await Task.WhenAny(receiveTask, inputTask);
cts.Cancel();

try { await Task.WhenAll(receiveTask, inputTask); }
catch (OperationCanceledException) { }

if (ws.State == WebSocketState.Open)
    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);

Console.WriteLine("\nDisconnected.");
return 0;

// ── receive ───────────────────────────────────────────────────────────────────

async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken ct)
{
    var buffer = new byte[64 * 1024];
    using var assembled = new MemoryStream();

    while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
    {
        assembled.SetLength(0);
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) return;
            assembled.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        if (result.MessageType != WebSocketMessageType.Binary) continue;

        try
        {
            var msg = MessagePackSerializer.Deserialize<ServerMessage>(
                assembled.ToArray(), MessagePackConfig.Options, ct);
            PrintMessage(msg);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERR] Deserialize failed: {ex.Message}");
        }
    }
}

// ── input ─────────────────────────────────────────────────────────────────────

async Task InputLoopAsync(ClientWebSocket socket, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        string? line;
        try { line = await Task.Run(Console.ReadLine, ct); }
        catch (OperationCanceledException) { return; }

        if (line is null) return;

        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) continue;

        if (parts[0] is "quit" or "q" or "exit") { cts.Cancel(); return; }

        ClientMessage? msg = parts[0].ToLowerInvariant() switch
        {
            "full" => new RequestFullStateMessage { CommandId = nextCommandId++ },

            "speed" when parts.Length == 2 && byte.TryParse(parts[1], out var s) && s <= 5
                => new ChangeSpeedMessage { CommandId = nextCommandId++, Speed = (SimSpeed)s },

            _ => null,
        };

        if (msg is null) { Console.WriteLine("Unknown command."); continue; }

        var payload = MessagePackSerializer.Serialize(msg, MessagePackConfig.Options, ct);
        await socket.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, ct);
        Console.WriteLine($"[SENT] {msg.GetType().Name} id={msg.CommandId}");
    }
}

// ── print ─────────────────────────────────────────────────────────────────────

static void PrintMessage(ServerMessage msg)
{
    switch (msg)
    {
        case StateDeltaMessage d:
            Console.WriteLine(
                $"[DELTA] srv={d.ServerTime:F2}s sim={d.SimulationTime:F3}s scale={d.TimeScale}" +
                $"  +e={d.CreatedEntities.Length} ~e={d.UpdatedEntities.Length} -e={d.RemovedEntityIds.Length}" +
                $"  +c={d.CreatedComponents.Length} -c={d.RemovedComponentIds.Length}" +
                (d.Events.Length > 0 ? $"  events={d.Events.Length}" : ""));
            break;

        case FullStateMessage f:
            Console.WriteLine(
                $"[FULL]  srv={f.ServerTime:F2}s sim={f.SimulationTime:F3}s scale={f.TimeScale}" +
                $"  entities={f.Entities.Length}  components={f.Components.Length}");
            foreach (var c in f.Components)
                Console.WriteLine($"         component id={c.Id} kind={c.Kind} ({c.GridX},{c.GridY}) facing={c.Facing}");
            foreach (var e in f.Entities)
                Console.WriteLine($"         entity    id={e.Id} sku={e.Sku} status={e.Status}");
            break;

        case CommandResultMessage r:
            var status = r.Success ? "OK" : $"FAIL: {r.ErrorMessage}";
            Console.WriteLine($"[ACK]   id={r.CommandId} {status}");
            break;

        default:
            Console.WriteLine($"[???]   {msg.GetType().Name}");
            break;
    }
}
