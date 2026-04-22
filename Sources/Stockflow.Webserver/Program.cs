using Microsoft.AspNetCore.Server.Kestrel.Core;
using Stockflow.Protocol.Serialization;
using Stockflow.Webserver.Configuration;
using Stockflow.Webserver.WebSocket;

MessagePackConfig.Initialize();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<ServerConfig>()
    .Bind(builder.Configuration.GetSection(ServerConfig.SectionName))
    .ValidateDataAnnotations();

var serverConfig = builder.Configuration
    .GetSection(ServerConfig.SectionName)
    .Get<ServerConfig>() ?? new ServerConfig();

builder.WebHost.ConfigureKestrel(options =>
{
    // Port split per ARCHITECTURE §7.1: 9600 = real-time WebSocket, 9601 = REST/JSON.
    // Until we route by local port explicitly, both endpoints accept every path — the
    // separation is a deployment convention the reverse proxy or firewall enforces.
    options.ListenAnyIP(serverConfig.WebSocketPort, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(serverConfig.RestPort,      listen => listen.Protocols = HttpProtocols.Http1AndHttp2);
});

builder.Services.AddSingleton<IClientCommandQueue, ClientCommandQueue>();
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton<WebSocketHandler>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
});

app.Map("/ws", async (HttpContext context, WebSocketHandler handler, IHostApplicationLifetime lifetime) =>
{
    await handler.HandleAsync(context, lifetime.ApplicationStopping);
});

app.MapGet("/api/health", (WebSocketHandler handler) => Results.Ok(new
{
    status            = "ok",
    connectedClients  = handler.ConnectedClientCount,
}));

app.Run();
