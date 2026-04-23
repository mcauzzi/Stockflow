using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Stockflow.Protocol.Serialization;
using Stockflow.Simulation.Core;
using Stockflow.Webserver.Api;
using Stockflow.Webserver.Configuration;
using Stockflow.Webserver.Hosting;
using Stockflow.Webserver.Queue;
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
builder.Services.AddSingleton<IRestCommandQueue, RestCommandQueue>();
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton<WebSocketHandler>();
builder.Services.AddSingleton<SimulationEngine>(sp =>
{
    var cfg = sp.GetRequiredService<IOptions<ServerConfig>>().Value;
    return new SimulationEngine(cfg.GridWidth, cfg.GridLength, cfg.GridFloors);
});
builder.Services.AddHostedService<SimulationHostedService>();

builder.Services.AddControllers();

// Local mode: allow any origin so the Angular dev server (default :4200) can reach the REST API.
// Enterprise mode would restrict this to known origins via config.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

app.UseDefaultFiles();
app.UseStaticFiles();

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

app.MapControllers();
ApiEndpoints.Map(app);

app.Run();
