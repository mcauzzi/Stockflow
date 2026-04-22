namespace Stockflow.Webserver.Configuration;

/// <summary>
/// Strongly-typed server configuration, bound from the <c>Server</c> section of
/// <c>appsettings.json</c> (and overridden via environment variables prefixed with
/// <c>STOCKFLOW_</c>, e.g. <c>STOCKFLOW_Server__WebSocketPort</c>).
/// </summary>
public sealed class ServerConfig
{
    public const string SectionName = "Server";

    /// <summary>Port exposing the MessagePack WebSocket channel (real-time sim deltas + commands).</summary>
    public int WebSocketPort { get; set; } = 9600;

    /// <summary>Port exposing the REST/JSON channel (scenarios, metrics export, WMS integration).</summary>
    public int RestPort { get; set; } = 9601;

    /// <summary>Deployment mode — drives auth, CORS and persistence defaults downstream.</summary>
    public ServerMode Mode { get; set; } = ServerMode.Local;

    /// <summary>Simulation tick rate in Hz.</summary>
    public int TickRate { get; set; } = 10;

    /// <summary>Grid dimensions for the simulation engine.</summary>
    public int GridWidth  { get; set; } = 50;
    public int GridLength { get; set; } = 50;
    public int GridFloors { get; set; } = 1;
}

public enum ServerMode
{
    /// <summary>Single-player / Steam deployment. No auth, localhost only.</summary>
    Local,

    /// <summary>On-premise / cloud B2B deployment. Auth + TLS expected upstream.</summary>
    Enterprise,
}
