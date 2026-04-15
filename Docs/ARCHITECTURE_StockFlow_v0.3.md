# STOCK FLOW — Architettura Tecnica

**Versione:** 0.3 — Persistenza dati e EF Core
**Data:** Aprile 2026
**Documento collegato:** GDD Stock Flow v0.2

---

## 1. Principio Fondamentale

Il Simulation Engine è l'unica fonte di verità. I client non decidono mai nulla, leggono e basta.

Non esiste stato autoritativo nei client. Non esiste logica di business in Unity. Se qualcosa accade nel magazzino simulato, accade prima nel server e poi viene comunicato ai client. Mai il contrario.

---

## 2. Architettura ad Alto Livello

Il Simulation Engine vive in un **processo server standalone** scritto in .NET 8+ puro, completamente svincolato da Unity. I client (Unity, web, CLI) si connettono al server via rete.

```
┌─────────────────────────────────────────────────────┐
│              STOCK FLOW SERVER (.NET 8+)             │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │           Simulation Engine                  │    │
│  │  Grid ─ Entities ─ Routing ─ Missions        │    │
│  │  Metrics ─ Clock ─ Plugins ─ Commands        │    │
│  └──────────────────┬──────────────────────────┘    │
│                     │                               │
│  ┌──────────────────┴──────────────────────────┐    │
│  │           Communication Layer                │    │
│  │                                              │    │
│  │  WebSocket ─── stato real-time (delta)       │    │
│  │               comandi dai client             │    │
│  │                                              │    │
│  │  REST/gRPC ─── config, scenari, metriche     │    │
│  │               export, sessioni               │    │
│  │               API WMS (Fase 2)               │    │
│  └──────────────────────────────────────────────┘    │
│                                                     │
│  ┌──────────────────────────────────────────────┐    │
│  │           Persistence Layer                   │    │
│  │  EF Core ─── SQLite (default)                │    │
│  │              PostgreSQL (enterprise, Fase 2)  │    │
│  └──────────────────────────────────────────────┘    │
│                                                     │
│  ┌──────────────────────────────────────────────┐    │
│  │           External Adapters (Fase 2)         │    │
│  │  REST WMS ─ WebSocket WMS ─ MQTT ─ OPCUA    │    │
│  └──────────────────────────────────────────────┘    │
│                                                     │
└──────────────┬──────────────┬───────────────────────┘
               │              │
         WebSocket       REST/gRPC
               │              │
    ┌──────────┴──┐    ┌──────┴──────┐    ┌───────────┐
    │ Client      │    │ Client      │    │ WMS       │
    │ Unity 3D    │    │ Web         │    │ Esterno   │
    │ (rendering) │    │ (dashboard) │    │ (Fase 2)  │
    └─────────────┘    └─────────────┘    └───────────┘
```

### 2.1 — Perché un server separato

**Libertà .NET completa:** Unity è limitato a un sottoinsieme di .NET (storicamente .NET Standard 2.1, ora .NET 6 parziale). Il server standalone usa .NET 8/9 con tutte le API moderne: System.Threading.Channels, Span<T> ad alte performance, source generators, ASP.NET hosting nativo per REST, Kestrel per WebSocket.

**Deployment flessibile:** il server può girare come processo locale (gioco Steam), come servizio in un server aziendale (B2B), come container Docker in cloud, o headless in CI per test automatici.

**Client intercambiabili:** Unity è solo uno dei possibili client. Un client web (React/Vue) per dashboard e metriche 2D. Un CLI per test automatici. L'interfaccia WMS. Tutti parlano lo stesso protocollo.

**Scalabilità B2B:** un server può gestire più sessioni di simulazione contemporanee per clienti enterprise. Impossibile con Unity.

**Modello ArmA/VBS rafforzato:** il cuore del prodotto è il server. I client sono skin intercambiabili. Il valore reale è nell'engine, non nel rendering.

---

## 3. Struttura dei Progetti

La soluzione è composta da quattro progetti separati in una solution .NET.

```
StockFlow/
│
├── StockFlow.sln                        ← Solution .NET
│
├── src/
│   │
│   ├── StockFlow.Simulation/            ← Class library .NET 8, C# puro
│   │   ├── StockFlow.Simulation.csproj
│   │   ├── Core/
│   │   │   ├── SimulationEngine.cs      ← Entry point, tick loop
│   │   │   ├── SimulationState.cs       ← Snapshot immutabile
│   │   │   ├── StateDelta.cs            ← Delta tra due tick (per rete)
│   │   │   └── SimulationClock.cs       ← Tempo simulato, velocità
│   │   ├── Grid/
│   │   │   ├── GridManager.cs
│   │   │   ├── Cell.cs
│   │   │   └── GridCoord.cs
│   │   ├── Entities/
│   │   │   ├── Entity.cs
│   │   │   ├── EntityManager.cs
│   │   │   └── EntityState.cs
│   │   ├── Components/
│   │   │   ├── ISimComponent.cs
│   │   │   ├── ConveyorLogic.cs
│   │   │   ├── CurveConveyorLogic.cs
│   │   │   ├── MergeLogic.cs
│   │   │   ├── DiverterLogic.cs
│   │   │   ├── AccumulatorLogic.cs
│   │   │   └── StackerCraneLogic.cs
│   │   ├── Routing/
│   │   │   ├── RoutingGraph.cs
│   │   │   ├── RoutingRule.cs
│   │   │   └── PathFinder.cs
│   │   ├── Missions/
│   │   │   ├── Mission.cs
│   │   │   ├── MissionController.cs
│   │   │   └── OrderManager.cs
│   │   ├── Metrics/
│   │   │   ├── MetricsCollector.cs
│   │   │   └── MetricsSnapshot.cs
│   │   ├── Commands/
│   │   │   ├── ICommand.cs
│   │   │   ├── PlaceComponentCommand.cs
│   │   │   ├── RemoveComponentCommand.cs
│   │   │   ├── ConfigureComponentCommand.cs
│   │   │   └── ChangeSpeedCommand.cs
│   │   └── Plugins/                     ← Fase 2
│   │       ├── IStockFlowComponent.cs
│   │       ├── IExternalAdapter.cs
│   │       └── PluginLoader.cs
│   │
│   ├── StockFlow.Server/               ← Executable .NET 8 (ASP.NET)
│   │   ├── StockFlow.Server.csproj     ← Referenzia Simulation, Persistence, Protocol
│   │   ├── Program.cs                  ← Entry point, hosting, DI
│   │   ├── Configuration/
│   │   │   ├── ServerConfig.cs
│   │   │   └── appsettings.json
│   │   ├── WebSocket/
│   │   │   ├── WebSocketHandler.cs
│   │   │   ├── ClientSession.cs
│   │   │   └── MessageRouter.cs
│   │   ├── Api/
│   │   │   ├── ScenarioController.cs
│   │   │   ├── MetricsController.cs
│   │   │   ├── SessionController.cs
│   │   │   └── WmsController.cs        ← Fase 2
│   │   ├── Protocol/
│   │   │   ├── Messages.cs
│   │   │   ├── MessageSerializer.cs
│   │   │   └── DeltaCompressor.cs
│   │   └── Hosting/
│   │       ├── SimulationHostedService.cs
│   │       ├── MetricsFlushService.cs   ← Background flush metriche su DB
│   │       └── HeadlessMode.cs
│   │
│   ├── StockFlow.Persistence/          ← Class library, EF Core + DbContext
│   │   ├── StockFlow.Persistence.csproj
│   │   ├── StockFlowDbContext.cs
│   │   ├── Entities/
│   │   │   ├── MetricRecord.cs
│   │   │   ├── SessionRecord.cs
│   │   │   ├── OrderRecord.cs
│   │   │   └── AuditLogEntry.cs         ← Fase 2
│   │   ├── Repositories/
│   │   │   ├── IMetricsRepository.cs
│   │   │   ├── MetricsRepository.cs
│   │   │   ├── ISessionRepository.cs
│   │   │   └── SessionRepository.cs
│   │   ├── Configuration/
│   │   │   ├── MetricRecordConfig.cs    ← Fluent API config
│   │   │   └── SessionRecordConfig.cs
│   │   └── Migrations/                  ← EF Core migrations
│   │
│   └── StockFlow.Protocol/             ← Class library condivisa
│       ├── StockFlow.Protocol.csproj
│       ├── Messages/
│       │   ├── ServerMessages.cs
│       │   ├── ClientMessages.cs
│       │   └── SharedTypes.cs
│       └── Serialization/
│           └── MessagePackConfig.cs
│
├── tests/
│   ├── StockFlow.Simulation.Tests/
│   ├── StockFlow.Server.Tests/
│   └── StockFlow.Persistence.Tests/
│
├── clients/
│   ├── unity/
│   │   └── StockFlowClient/
│   │       ├── Assets/
│   │       │   ├── Plugins/
│   │       │   │   └── StockFlow.Protocol.dll
│   │       │   ├── Networking/
│   │       │   │   ├── ServerConnection.cs
│   │       │   │   ├── StateBuffer.cs
│   │       │   │   └── CommandSender.cs
│   │       │   ├── Bridge/
│   │       │   │   ├── SimulationBridge.cs
│   │       │   │   ├── EntityPool.cs
│   │       │   │   └── ComponentViewRegistry.cs
│   │       │   ├── Visuals/
│   │       │   │   ├── ConveyorView.cs
│   │       │   │   ├── StackerCraneView.cs
│   │       │   │   ├── EntityView.cs
│   │       │   │   ├── ShelfView.cs
│   │       │   │   └── HeatmapOverlay.cs
│   │       │   ├── Input/
│   │       │   │   ├── BuildModeController.cs
│   │       │   │   ├── SelectionController.cs
│   │       │   │   └── CommandDispatcher.cs
│   │       │   ├── Camera/
│   │       │   │   ├── IsometricCamera.cs
│   │       │   │   └── CameraShake.cs
│   │       │   ├── UI/
│   │       │   │   ├── HUD.cs
│   │       │   │   ├── ComponentPalette.cs
│   │       │   │   ├── InspectorPanel.cs
│   │       │   │   ├── OrderQueue.cs
│   │       │   │   ├── MetricsDashboard.cs
│   │       │   │   └── SpeedControls.cs
│   │       │   ├── Audio/
│   │       │   │   ├── AudioManager.cs
│   │       │   │   └── ConveyorAudio.cs
│   │       │   ├── Juice/
│   │       │   │   ├── ParticleEffects.cs
│   │       │   │   ├── FloatingNumbers.cs
│   │       │   │   └── ConveyorWaveShader.shader
│   │       │   ├── Launcher/
│   │       │   │   └── ServerLauncher.cs
│   │       │   └── Content/
│   │       │       ├── Scenarios/
│   │       │       ├── Prefabs/
│   │       │       ├── Materials/
│   │       │       └── Audio/
│   │       └── Packages/
│   │
│   └── web/                             ← Futuro
│       └── stockflow-dashboard/
│
└── docs/
    ├── GDD.md
    └── ARCHITECTURE.md
```

### 3.1 — Dipendenze tra progetti

```
StockFlow.Protocol ◄──────── StockFlow.Server
        ▲                          │
        │                          ├──► StockFlow.Simulation
        │                          │
        │                          └──► StockFlow.Persistence
        │
        └──────────────────── Unity Client (come DLL)
                              Web Client (come npm package)


StockFlow.Simulation ──── C# puro, zero dipendenze
StockFlow.Persistence ─── EF Core, non referenzia Simulation
StockFlow.Protocol ─────── MessagePack, tipi condivisi
StockFlow.Server ────────── referenzia tutti e tre
Client Unity ────────────── referenzia solo Protocol (DLL)
```

---

## 4. Persistenza Dati

### 4.1 — Strategia per categoria di dato

| Dato | Storage | Formato | Motivo |
|------|---------|---------|--------|
| Stato simulazione runtime | RAM | Oggetti C# | Cambia 10-100x/s, persistere sarebbe un bottleneck |
| Scenari e livelli | File | JSON | Dati statici, letti una volta all'avvio |
| Salvataggi partita | File | MessagePack | Snapshot completo, serializza/deserializza e basta |
| Metriche storiche | Database | EF Core + SQLite | Time-series con query aggregate |
| Sessioni e run storiche | Database | EF Core + SQLite | Storico partite, confronto performance |
| Ordini completati (log) | Database | EF Core + SQLite | Analisi post-sessione |
| Config utente | File | JSON | Piccolo, statico, preferenze personali |
| Audit log (B2B) | Database | EF Core + SQLite/PostgreSQL | Tracciabilità operazioni WMS |

### 4.2 — Principio: mai il database nel hot path

Il tick loop della simulazione non tocca mai il database. Mai una write sincrona, mai una query durante un tick. Il flusso è:

```
Tick loop (10-100 Hz)
    │
    ├── Aggiorna stato in RAM
    ├── Calcola metriche in RAM
    └── Scrive metriche nel MetricsBuffer (in-memory ring buffer)
                │
                │  ogni N secondi (configurabile, default 5s)
                ▼
MetricsFlushService (background)
    │
    └── Legge batch dal buffer → SaveChangesAsync() → DB
```

Il `MetricsFlushService` è un `BackgroundService` ASP.NET separato dal tick loop. Usa un `Channel<T>` per ricevere i batch dal `MetricsCollector` senza lock.

### 4.3 — EF Core e DbContext

```csharp
public class StockFlowDbContext : DbContext
{
    public DbSet<MetricRecord> Metrics { get; set; }
    public DbSet<SessionRecord> Sessions { get; set; }
    public DbSet<OrderRecord> CompletedOrders { get; set; }
    public DbSet<AuditLogEntry> AuditLog { get; set; }  // Fase 2

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(StockFlowDbContext).Assembly);
    }
}
```

### 4.4 — Entità database

```csharp
// Metrica singola (time-series)
public class MetricRecord
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public float SimulationTime { get; set; }
    public DateTime WallClockTime { get; set; }
    public string MetricName { get; set; }    // "throughput", "avg_fulfillment", ...
    public double Value { get; set; }
    
    // Navigation
    public SessionRecord Session { get; set; }
}

// Sessione di simulazione (una "partita" o "run")
public class SessionRecord
{
    public Guid Id { get; set; }
    public string ScenarioName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public float TotalSimulationTime { get; set; }
    public string FinalMetricsJson { get; set; }   // snapshot finale
    public SessionStatus Status { get; set; }       // Running, Completed, Abandoned
    
    // Navigation
    public ICollection<MetricRecord> Metrics { get; set; }
    public ICollection<OrderRecord> CompletedOrders { get; set; }
}

public enum SessionStatus
{
    Running,
    Completed,
    Abandoned
}

// Ordine completato (log per analisi)
public class OrderRecord
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public int OrderId { get; set; }           // ID interno simulazione
    public float CreatedAtSimTime { get; set; }
    public float CompletedAtSimTime { get; set; }
    public float FulfillmentTime { get; set; }  // calcolato
    public int LineCount { get; set; }
    public bool WasLate { get; set; }
    
    // Navigation
    public SessionRecord Session { get; set; }
}

// Audit log (Fase 2, B2B)
public class AuditLogEntry
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Source { get; set; }          // "WMS", "User", "System"
    public string Action { get; set; }          // "OrderReceived", "MissionCompleted"
    public string Details { get; set; }         // JSON payload
}
```

### 4.5 — Configurazione EF Core (Fluent API)

```csharp
public class MetricRecordConfig : IEntityTypeConfiguration<MetricRecord>
{
    public void Configure(EntityTypeBuilder<MetricRecord> builder)
    {
        builder.HasKey(m => m.Id);
        
        builder.HasIndex(m => new { m.SessionId, m.MetricName, m.SimulationTime })
               .HasDatabaseName("IX_Metrics_Session_Name_Time");
        
        builder.Property(m => m.MetricName)
               .HasMaxLength(64)
               .IsRequired();
    }
}

public class SessionRecordConfig : IEntityTypeConfiguration<SessionRecord>
{
    public void Configure(EntityTypeBuilder<SessionRecord> builder)
    {
        builder.HasKey(s => s.Id);
        
        builder.HasMany(s => s.Metrics)
               .WithOne(m => m.Session)
               .HasForeignKey(m => m.SessionId)
               .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(s => s.CompletedOrders)
               .WithOne(o => o.Session)
               .HasForeignKey(o => o.SessionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 4.6 — Swap provider SQLite → PostgreSQL

Il motivo principale per usare EF Core fin dall'inizio: il cambio di database per B2B è una modifica di configurazione, non di codice.

```csharp
// Program.cs — registrazione DbContext
if (config.DatabaseProvider == "sqlite")
{
    services.AddDbContext<StockFlowDbContext>(options =>
        options.UseSqlite($"Data Source={config.DataPath}/stockflow.db"));
}
else if (config.DatabaseProvider == "postgresql")
{
    services.AddDbContext<StockFlowDbContext>(options =>
        options.UseNpgsql(config.ConnectionString));
}
```

```json
// appsettings.json — gioco Steam (default)
{
    "Database": {
        "Provider": "sqlite",
        "DataPath": "./data"
    }
}

// appsettings.enterprise.json — B2B
{
    "Database": {
        "Provider": "postgresql",
        "ConnectionString": "Host=db.azienda.local;Database=stockflow;..."
    }
}
```

Le migration EF Core funzionano con entrambi i provider. Si generano una volta e si applicano automaticamente all'avvio.

### 4.7 — MetricsRepository

```csharp
public interface IMetricsRepository
{
    Task SaveBatchAsync(IEnumerable<MetricRecord> records);
    Task<IReadOnlyList<MetricRecord>> GetTimeSeriesAsync(
        Guid sessionId, string metricName, float fromTime, float toTime);
    Task<MetricsSummary> GetSummaryAsync(Guid sessionId);
    Task<Stream> ExportCsvAsync(Guid sessionId);
}

public class MetricsRepository : IMetricsRepository
{
    private readonly StockFlowDbContext db;
    
    public MetricsRepository(StockFlowDbContext db)
    {
        this.db = db;
    }
    
    public async Task SaveBatchAsync(IEnumerable<MetricRecord> records)
    {
        db.Metrics.AddRange(records);
        await db.SaveChangesAsync();
    }
    
    public async Task<IReadOnlyList<MetricRecord>> GetTimeSeriesAsync(
        Guid sessionId, string metricName, float fromTime, float toTime)
    {
        return await db.Metrics
            .Where(m => m.SessionId == sessionId
                     && m.MetricName == metricName
                     && m.SimulationTime >= fromTime
                     && m.SimulationTime <= toTime)
            .OrderBy(m => m.SimulationTime)
            .ToListAsync();
    }
    
    public async Task<MetricsSummary> GetSummaryAsync(Guid sessionId)
    {
        var metrics = db.Metrics.Where(m => m.SessionId == sessionId);
        
        return new MetricsSummary
        {
            AvgThroughput = await metrics
                .Where(m => m.MetricName == "throughput")
                .AverageAsync(m => m.Value),
            AvgFulfillmentTime = await metrics
                .Where(m => m.MetricName == "avg_fulfillment_time")
                .AverageAsync(m => m.Value),
            PeakThroughput = await metrics
                .Where(m => m.MetricName == "throughput")
                .MaxAsync(m => m.Value),
            TotalOrdersCompleted = await db.CompletedOrders
                .CountAsync(o => o.SessionId == sessionId),
            LateOrderPercentage = await db.CompletedOrders
                .Where(o => o.SessionId == sessionId)
                .AverageAsync(o => o.WasLate ? 1.0 : 0.0) * 100
        };
    }
}
```

### 4.8 — MetricsFlushService

```csharp
public class MetricsFlushService : BackgroundService
{
    private readonly Channel<MetricRecord[]> channel;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<MetricsFlushService> logger;
    
    // Il MetricsCollector scrive qui dentro
    public ChannelWriter<MetricRecord[]> Writer => channel.Writer;
    
    public MetricsFlushService(
        IServiceScopeFactory scopeFactory,
        ILogger<MetricsFlushService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        
        channel = Channel.CreateBounded<MetricRecord[]>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            });
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var batch in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repo = scope.ServiceProvider
                    .GetRequiredService<IMetricsRepository>();
                
                await repo.SaveBatchAsync(batch);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, 
                    "Errore flush metriche, batch di {Count} record perso", 
                    batch.Length);
                // Non crashare il servizio, logga e continua
            }
        }
    }
}
```

### 4.9 — MetricsCollector (nel Simulation Engine)

Il collector vive nel Simulation Engine ma non conosce EF Core. Usa un'interfaccia astratta che il Server implementa con il channel.

```csharp
// In StockFlow.Simulation — interfaccia astratta
public interface IMetricsSink
{
    void Emit(string metricName, double value, float simulationTime);
}

// In StockFlow.Simulation — collector
public class MetricsCollector
{
    private readonly IMetricsSink sink;
    private readonly List<(string name, double value, float time)> buffer = new();
    private readonly int flushIntervalTicks;
    private int ticksSinceFlush;
    
    public MetricsCollector(IMetricsSink sink, int flushIntervalTicks = 50)
    {
        this.sink = sink;
        this.flushIntervalTicks = flushIntervalTicks;
    }
    
    // Chiamato ogni tick dal SimulationEngine
    public void RecordTick(SimulationState state)
    {
        buffer.Add(("throughput", state.CurrentThroughput, state.SimulationTime));
        buffer.Add(("avg_fulfillment_time", state.AvgFulfillmentTime, state.SimulationTime));
        buffer.Add(("warehouse_saturation", state.WarehouseSaturation, state.SimulationTime));
        // ... altri KPI
        
        ticksSinceFlush++;
        if (ticksSinceFlush >= flushIntervalTicks)
        {
            Flush();
            ticksSinceFlush = 0;
        }
    }
    
    private void Flush()
    {
        foreach (var (name, value, time) in buffer)
        {
            sink.Emit(name, value, time);
        }
        buffer.Clear();
    }
}

// In StockFlow.Server — implementazione concreta
public class ChannelMetricsSink : IMetricsSink
{
    private readonly ChannelWriter<MetricRecord[]> writer;
    private readonly Guid sessionId;
    private readonly List<MetricRecord> batch = new();
    
    public void Emit(string metricName, double value, float simulationTime)
    {
        batch.Add(new MetricRecord
        {
            SessionId = sessionId,
            MetricName = metricName,
            Value = value,
            SimulationTime = simulationTime,
            WallClockTime = DateTime.UtcNow
        });
        
        // Manda il batch al flush service quando è abbastanza grande
        if (batch.Count >= 100)
        {
            writer.TryWrite(batch.ToArray());
            batch.Clear();
        }
    }
}
```

---

## 5. Salvataggi (File-based)

### 5.1 — Formato

I salvataggi sono snapshot completi serializzati in MessagePack. Nessun database coinvolto.

```csharp
[MessagePackObject]
public class SaveFile
{
    [Key(0)] public int Version { get; set; }           // versione formato
    [Key(1)] public DateTime SavedAt { get; set; }
    [Key(2)] public string ScenarioId { get; set; }
    
    // Stato completo della simulazione
    [Key(3)] public GridData Grid { get; set; }          // griglia + componenti
    [Key(4)] public EntityData[] Entities { get; set; }  // tutti i pacchi in scena
    [Key(5)] public OrderData[] ActiveOrders { get; set; }
    [Key(6)] public MissionData[] ActiveMissions { get; set; }
    
    // Progressione giocatore
    [Key(7)] public float SimulationTime { get; set; }
    [Key(8)] public int Budget { get; set; }
    [Key(9)] public int[] UnlockedComponents { get; set; }
    [Key(10)] public int Stars { get; set; }
    
    // Metriche snapshot (per mostrare summary al caricamento)
    [Key(11)] public MetricsSnapshot MetricsAtSave { get; set; }
}
```

### 5.2 — Salvataggio e caricamento

```csharp
public class SaveManager
{
    private readonly string savePath;
    
    public async Task SaveAsync(SimulationEngine engine, string fileName)
    {
        var saveFile = new SaveFile
        {
            Version = 1,
            SavedAt = DateTime.UtcNow,
            Grid = engine.ExportGrid(),
            Entities = engine.ExportEntities(),
            // ...
        };
        
        var bytes = MessagePackSerializer.Serialize(saveFile);
        var path = Path.Combine(savePath, $"{fileName}.stockflow");
        await File.WriteAllBytesAsync(path, bytes);
    }
    
    public async Task<SaveFile> LoadAsync(string fileName)
    {
        var path = Path.Combine(savePath, $"{fileName}.stockflow");
        var bytes = await File.ReadAllBytesAsync(path);
        return MessagePackSerializer.Deserialize<SaveFile>(bytes);
    }
}
```

### 5.3 — Localizzazione file

```
[Steam]
    %APPDATA%/StockFlow/
    ├── saves/
    │   ├── autosave.stockflow
    │   ├── my-warehouse.stockflow
    │   └── black-friday-test.stockflow
    ├── data/
    │   └── stockflow.db              ← SQLite metriche storiche
    ├── config/
    │   └── preferences.json
    └── logs/
        └── server.log

[B2B Docker]
    /data/
    ├── scenarios/
    ├── saves/
    ├── stockflow.db (o PostgreSQL esterno)
    └── plugins/
```

---

## 6. Scenari (File-based)

### 6.1 — Formato scenario

```json
{
    "id": "campaign-04-black-friday",
    "name": "Black Friday",
    "description": "Picco di ordini 10x. Prepara il magazzino.",
    "gridSize": { "width": 40, "height": 30 },
    "budget": 25000,
    "availableComponents": [
        "conveyor_straight", "conveyor_curve", "merge", 
        "diverter", "stacker_crane", "shelf_single"
    ],
    "preplacedComponents": [
        { "type": "bay_inbound", "position": [0, 15], "direction": "east" },
        { "type": "bay_outbound", "position": [39, 15], "direction": "east" }
    ],
    "skuCatalog": {
        "count": 50,
        "classes": {
            "A": { "percentage": 20, "accessFrequency": "high" },
            "B": { "percentage": 30, "accessFrequency": "medium" },
            "C": { "percentage": 50, "accessFrequency": "low" }
        }
    },
    "orderProfile": {
        "baseRate": 10,
        "peakRate": 100,
        "peakStartTime": 300,
        "peakDuration": 600,
        "linesPerOrder": { "min": 1, "max": 3 },
        "deadline": 120
    },
    "objectives": {
        "1star": { "type": "complete_all_orders" },
        "2star": { "type": "max_late_percentage", "value": 10 },
        "3star": { "type": "min_throughput", "value": 80 }
    },
    "duration": 1800
}
```

I file scenario vivono nella cartella `Scenarios/` e sono caricati all'avvio della sessione. Non passano per il database.

---

## 7. Protocollo di Comunicazione

### 7.1 — Due canali

**WebSocket (porta 9600):** comunicazione bidirezionale real-time. Il server manda delta dello stato a ogni tick. I client mandano comandi. Bassa latenza, alta frequenza.

**REST API (porta 9601):** operazioni non real-time. CRUD scenari, export metriche, configurazione, gestione sessioni, endpoint WMS. Request/response classico.

### 7.2 — Formato messaggi: MessagePack

I messaggi WebSocket sono serializzati con MessagePack (binario) anziché JSON.

Vantaggi rispetto a JSON:
- 2-5x più compatto
- 5-10x più veloce in serializzazione/deserializzazione
- Supporto nativo tipi binari e array
- Librerie mature per .NET (MessagePack-CSharp) e Unity (stesso pacchetto)

Un delta di stato con 100 entità aggiornate pesa circa 2-5 KB in MessagePack, contro 10-25 KB in JSON.

Per il canale REST si usa JSON standard (leggibilità, debug, compatibilità tool).

### 7.3 — Messaggi Server → Client (WebSocket)

```csharp
[MessagePackObject]
public abstract class ServerMessage
{
    [Key(0)] public byte MessageType { get; set; }
    [Key(1)] public float ServerTime { get; set; }
}

// Delta stato: solo ciò che è cambiato dall'ultimo tick
[MessagePackObject]
public class StateDeltaMessage : ServerMessage
{
    [Key(2)] public float SimulationTime { get; set; }
    [Key(3)] public float TimeScale { get; set; }
    
    [Key(4)] public EntityState[] UpdatedEntities { get; set; }
    [Key(5)] public EntityState[] CreatedEntities { get; set; }
    [Key(6)] public int[] RemovedEntityIds { get; set; }
    
    [Key(7)] public ComponentState[] UpdatedComponents { get; set; }
    [Key(8)] public ComponentState[] CreatedComponents { get; set; }
    [Key(9)] public int[] RemovedComponentIds { get; set; }
    
    [Key(10)] public SimEvent[] Events { get; set; }
    [Key(11)] public MetricsSnapshot Metrics { get; set; }
    [Key(12)] public OrderState[] UpdatedOrders { get; set; }
    
    [Key(13)] public uint? StateChecksum { get; set; }   // ogni N tick
}

// Stato completo: alla connessione iniziale o su richiesta
[MessagePackObject]
public class FullStateMessage : ServerMessage
{
    [Key(2)] public SimulationState FullState { get; set; }
}

// Risposta a un comando
[MessagePackObject]
public class CommandResultMessage : ServerMessage
{
    [Key(2)] public int CommandId { get; set; }
    [Key(3)] public bool Success { get; set; }
    [Key(4)] public string ErrorMessage { get; set; }
}
```

### 7.4 — Messaggi Client → Server (WebSocket)

```csharp
[MessagePackObject]
public abstract class ClientMessage
{
    [Key(0)] public byte MessageType { get; set; }
    [Key(1)] public int CommandId { get; set; }
}

[MessagePackObject]
public class PlaceComponentMessage : ClientMessage
{
    [Key(2)] public int ComponentType { get; set; }
    [Key(3)] public int GridX { get; set; }
    [Key(4)] public int GridY { get; set; }
    [Key(5)] public int Direction { get; set; }
}

[MessagePackObject]
public class RemoveComponentMessage : ClientMessage
{
    [Key(2)] public int ComponentId { get; set; }
}

[MessagePackObject]
public class ConfigureComponentMessage : ClientMessage
{
    [Key(2)] public int ComponentId { get; set; }
    [Key(3)] public Dictionary<string, string> Properties { get; set; }
}

[MessagePackObject]
public class ChangeSpeedMessage : ClientMessage
{
    [Key(2)] public float NewTimeScale { get; set; }
}

[MessagePackObject]
public class SaveGameMessage : ClientMessage
{
    [Key(2)] public string FileName { get; set; }
}

[MessagePackObject]
public class LoadGameMessage : ClientMessage
{
    [Key(2)] public string FileName { get; set; }
}

[MessagePackObject]
public class RequestFullStateMessage : ClientMessage { }
```

### 7.5 — REST API Endpoints

```
GET    /api/scenarios              Lista scenari disponibili
GET    /api/scenarios/{id}         Dettaglio scenario
POST   /api/scenarios              Crea nuovo scenario
PUT    /api/scenarios/{id}         Aggiorna scenario
DELETE /api/scenarios/{id}         Elimina scenario

POST   /api/sessions               Avvia nuova sessione di simulazione
GET    /api/sessions/{id}          Stato sessione
DELETE /api/sessions/{id}          Termina sessione

GET    /api/sessions/{id}/metrics              Metriche correnti
GET    /api/sessions/{id}/metrics/history      Metriche storiche
GET    /api/sessions/{id}/metrics/summary      Summary aggregato
GET    /api/sessions/{id}/metrics/export       Export CSV

GET    /api/sessions/{id}/orders               Ordini completati
GET    /api/sessions/{id}/orders/stats         Statistiche ordini

GET    /api/sessions/{id}/state                Stato completo (debug)

GET    /api/saves                  Lista salvataggi
POST   /api/saves                  Crea salvataggio
DELETE /api/saves/{name}           Elimina salvataggio

--- Fase 2: WMS ---
POST   /api/wms/orders             Ricevi ordini da WMS
GET    /api/wms/confirmations      Conferme evasione per WMS
WebSocket /ws/wms                  Stream bidirezionale WMS
```

---

## 8. Ciclo di Comunicazione

### 8.1 — Connessione iniziale

```
Client                              Server
  │                                    │
  │──── WebSocket Connect ────────────►│
  │                                    │
  │◄─── FullStateMessage ─────────────│  (stato completo iniziale)
  │                                    │
  │◄─── StateDeltaMessage ────────────│  ╮
  │◄─── StateDeltaMessage ────────────│  │ stream continuo
  │◄─── StateDeltaMessage ────────────│  │ 1 delta per tick
  │◄─── StateDeltaMessage ────────────│  ╯
```

### 8.2 — Invio comando

```
Client                              Server
  │                                    │
  │──── PlaceComponentMessage ────────►│  (CommandId=42)
  │         {conveyor, pos 5,3, east}  │
  │                                    │  Server valida ed esegue al prossimo tick
  │                                    │
  │◄─── CommandResultMessage ─────────│  (CommandId=42, success=true)
  │                                    │
  │◄─── StateDeltaMessage ────────────│  (contiene il nuovo componente
  │                                    │   in CreatedComponents)
```

### 8.3 — Flusso completo di un tick

```
Server tick N:
    1. Legge tutti i comandi ricevuti dai client dalla coda
    2. Valida ed esegue ogni comando
    3. Manda CommandResultMessage per ogni comando (success/fail)
    4. Avanza la simulazione di un tick:
       - Muove entità sui conveyor
       - Esegue cicli traslo
       - Processa routing ai diverter
       - Genera/chiude ordini
       - Aggiorna metriche
    5. Passa le metriche al MetricsCollector (buffer in RAM)
    6. Calcola StateDelta rispetto al tick N-1
    7. Serializza con MessagePack
    8. Manda StateDeltaMessage a tutti i client connessi
    
    [Indipendentemente, in background:]
    MetricsFlushService legge dal Channel e scrive su DB
```

---

## 9. Client Unity — Ricezione e Rendering

### 9.1 — ServerConnection

```csharp
public class ServerConnection : MonoBehaviour
{
    private ClientWebSocket socket;
    private CancellationTokenSource cts;
    
    private ConcurrentQueue<ServerMessage> incomingMessages = new();
    private ConcurrentQueue<ClientMessage> outgoingCommands = new();
    
    public async Task ConnectAsync(string url)
    {
        socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(url), cts.Token);
        
        _ = ReceiveLoopAsync();
        _ = SendLoopAsync();
    }
    
    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[1024 * 64];
        
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, cts.Token);
            var message = MessagePackSerializer.Deserialize<ServerMessage>(
                buffer.AsMemory(0, result.Count));
            
            incomingMessages.Enqueue(message);
        }
    }
    
    public bool TryDequeue(out ServerMessage message)
    {
        return incomingMessages.TryDequeue(out message);
    }
    
    public void SendCommand(ClientMessage command)
    {
        outgoingCommands.Enqueue(command);
    }
}
```

### 9.2 — StateBuffer

```csharp
public class StateBuffer
{
    private SimulationState currentState;
    private SimulationState previousState;
    
    private float lastTickTime;
    private float tickInterval;
    
    public void ApplyFullState(FullStateMessage msg)
    {
        currentState = msg.FullState;
        previousState = msg.FullState;
    }
    
    public void ApplyDelta(StateDeltaMessage delta)
    {
        previousState = currentState.Clone();
        
        foreach (var entity in delta.UpdatedEntities)
            currentState.UpdateEntity(entity);
        foreach (var entity in delta.CreatedEntities)
            currentState.AddEntity(entity);
        foreach (var id in delta.RemovedEntityIds)
            currentState.RemoveEntity(id);
            
        foreach (var comp in delta.UpdatedComponents)
            currentState.UpdateComponent(comp);
        foreach (var comp in delta.CreatedComponents)
            currentState.AddComponent(comp);
        foreach (var id in delta.RemovedComponentIds)
            currentState.RemoveComponent(id);
        
        currentState.Metrics = delta.Metrics;
        foreach (var order in delta.UpdatedOrders)
            currentState.UpdateOrder(order);
        
        lastTickTime = Time.time;
        tickInterval = delta.SimulationTime - previousState.SimulationTime;
    }
    
    public float InterpolationFactor
    {
        get
        {
            if (tickInterval <= 0) return 1f;
            return Mathf.Clamp01((Time.time - lastTickTime) / tickInterval);
        }
    }
    
    public SimulationState Current => currentState;
    public SimulationState Previous => previousState;
}
```

### 9.3 — SimulationBridge

```csharp
public class SimulationBridge : MonoBehaviour
{
    [SerializeField] private string serverUrl = "ws://localhost:9600";
    
    private ServerConnection connection;
    private StateBuffer stateBuffer;
    private EntityPool entityPool;
    private ComponentViewRegistry componentRegistry;
    private EventHandler eventHandler;
    
    async void Start()
    {
        connection = GetComponent<ServerConnection>();
        stateBuffer = new StateBuffer();
        await connection.ConnectAsync(serverUrl);
    }
    
    void Update()
    {
        while (connection.TryDequeue(out var message))
        {
            switch (message)
            {
                case FullStateMessage full:
                    stateBuffer.ApplyFullState(full);
                    RebuildAllVisuals();
                    break;
                    
                case StateDeltaMessage delta:
                    stateBuffer.ApplyDelta(delta);
                    ProcessEvents(delta.Events);
                    break;
                    
                case CommandResultMessage result:
                    HandleCommandResult(result);
                    break;
            }
        }
    }
    
    void LateUpdate()
    {
        var current = stateBuffer.Current;
        var previous = stateBuffer.Previous;
        float t = stateBuffer.InterpolationFactor;
        
        if (current == null) return;
        
        SyncEntityPool(current);
        
        foreach (var entity in current.Entities)
        {
            var view = entityPool.Get(entity.Id);
            if (view == null) continue;
            
            var prevEntity = previous?.GetEntity(entity.Id);
            if (prevEntity.HasValue)
            {
                view.transform.position = Vector3.Lerp(
                    ToUnity(prevEntity.Value.Position),
                    ToUnity(entity.Position),
                    t
                );
            }
            else
            {
                view.transform.position = ToUnity(entity.Position);
            }
        }
        
        foreach (var comp in current.Components)
        {
            var view = componentRegistry.GetView(comp.Id);
            view?.UpdateVisuals(comp, t);
        }
        
        hud.UpdateMetrics(current.Metrics);
        orderQueue.UpdateOrders(current.Orders);
    }
    
    private UnityEngine.Vector3 ToUnity(Protocol.Vector3 v)
    {
        return new UnityEngine.Vector3(v.X, v.Y, v.Z);
    }
}
```

---

## 10. Server — Tick Loop

### 10.1 — SimulationHostedService

```csharp
public class SimulationHostedService : BackgroundService
{
    private readonly SimulationEngine engine;
    private readonly WebSocketHandler wsHandler;
    private readonly MetricsCollector metricsCollector;
    private readonly ILogger logger;
    
    private readonly ConcurrentQueue<(ClientSession, ClientMessage)> commandQueue = new();
    
    public void EnqueueCommand(ClientSession session, ClientMessage message)
    {
        commandQueue.Enqueue((session, message));
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tickRate = engine.TickRate;
        var tickInterval = TimeSpan.FromSeconds(1.0 / tickRate);
        
        using var timer = new PeriodicTimer(tickInterval);
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // 1. Processa comandi
            while (commandQueue.TryDequeue(out var item))
            {
                var (session, message) = item;
                var result = engine.ProcessCommand(message);
                await session.SendAsync(result);
            }
            
            // 2. Tick simulazione
            engine.Tick(1f / tickRate * engine.TimeScale);
            
            // 3. Metriche (buffer in RAM, flush asincrono)
            metricsCollector.RecordTick(engine.CurrentState);
            
            // 4. Delta e broadcast
            var delta = engine.GetStateDelta();
            var bytes = MessagePackSerializer.Serialize(delta);
            await wsHandler.BroadcastAsync(bytes);
        }
    }
}
```

### 10.2 — Velocità variabile

| Velocità | Tick rate | Delta time per tick | Note |
|----------|-----------|-------------------|------|
| 1x | 10/s | 0.1s sim | Tempo reale |
| 2x | 10/s | 0.2s sim | Stessa frequenza, tempo sim doppio |
| 5x | 10/s | 0.5s sim | Stessa frequenza, tempo sim 5x |
| 10x | 20/s | 0.5s sim | Frequenza doppia + tempo sim 5x |
| LIVE | 10/s | 0.1s sim | Forzato 1x con WMS connesso |

---

## 11. Modalità di Deployment

### 11.1 — Gioco Steam (locale)

Il giocatore non sa che c'è un server separato. Unity avvia il processo automaticamente.

```csharp
public class ServerLauncher : MonoBehaviour
{
    private Process serverProcess;
    
    void Awake()
    {
        var serverPath = Path.Combine(
            Application.dataPath, "..", "Server", "StockFlow.Server.exe");
        
        serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = serverPath,
                Arguments = "--port 9600 --mode local",
                CreateNoWindow = true,
                UseShellExecute = false
            }
        };
        serverProcess.Start();
    }
    
    void OnApplicationQuit()
    {
        serverProcess?.Kill();
        serverProcess?.WaitForExit(3000);
    }
}
```

Distribuzione Steam:
```
StockFlow/
├── StockFlow.exe
├── Server/
│   ├── StockFlow.Server.exe
│   └── appsettings.json
├── StockFlow_Data/
├── Scenarios/
└── data/
    └── stockflow.db            ← SQLite, creato automaticamente
```

### 11.2 — B2B On-Premise (Docker)

```yaml
services:
  stockflow:
    image: stockflow/server:latest
    ports:
      - "9600:9600"
      - "9601:9601"
    volumes:
      - ./data:/data
      - ./scenarios:/scenarios
      - ./plugins:/plugins
    environment:
      - STOCKFLOW_MODE=enterprise
      - STOCKFLOW_DB_PROVIDER=postgresql
      - STOCKFLOW_DB_CONNECTION=Host=db;Database=stockflow;...
      - STOCKFLOW_WMS_ENABLED=true
      
  db:
    image: postgres:16
    volumes:
      - pgdata:/var/lib/postgresql/data
    environment:
      - POSTGRES_DB=stockflow
```

### 11.3 — Headless / CI

```bash
stockflow-server --headless --scenario black-friday.json \
                 --duration 3600 --output metrics.csv \
                 --db sqlite:///tmp/test.db
```

---

## 12. Latenza e Performance

### 12.1 — Budget di latenza

| Scenario | Latenza rete | Serializzazione | Totale | Percepibile? |
|----------|-------------|-----------------|--------|--------------|
| Locale (stesso PC) | <1ms | ~0.5ms | ~1.5ms | No |
| Rete locale | 1-5ms | ~0.5ms | ~5ms | No |
| Internet (cloud) | 20-50ms | ~0.5ms | ~50ms | Lieve, compensato da interpolazione |

### 12.2 — Budget per il database

| Operazione | Target | Note |
|-----------|--------|------|
| Flush batch metriche (100 righe) | <5ms | Asincrono, non blocca tick |
| Query time-series (1 metrica, 1h) | <50ms | Dashboard, non real-time |
| Query summary aggregato | <100ms | On-demand |
| Export CSV | <1s | Background |

SQLite è ampiamente entro questi budget. PostgreSQL anche di più.

### 12.3 — Ottimizzazione del delta

**Delta semantico:** solo ciò che è cambiato. Se 900 entità su 1000 sono ferme, il delta contiene solo 100 `UpdatedEntities`.

**Compressione posizioni:** coordinate griglia come interi + progress come byte (0-255).

**Batching eventi:** eventi dello stesso tipo nella stessa posizione aggregati.

**Budget target:** <10 KB per delta a regime (1000 entità, 10 tick/s = ~100 KB/s).

---

## 13. Validazione Anti-Divergenza

### 13.1 — Checksum periodico

Ogni N tick (es. 100) il server calcola un hash dello stato completo e lo include nel delta. Il client calcola lo stesso hash sul suo stato ricostruito. Se divergono, il client richiede un `FullStateMessage` per risincronizzarsi.

### 13.2 — Heartbeat

Il server manda un heartbeat ogni 5 secondi. Se il client non lo riceve per 15 secondi, mostra "Connessione persa" e tenta la riconnessione automatica.

### 13.3 — Riconnessione

Quando un client si riconnette, il server manda un `FullStateMessage` con lo stato completo corrente, il client ricostruisce tutti i visuals, e lo stream di delta riprende.

---

## 14. Threading Model

### 14.1 — Lato Server

```
Main Thread (ASP.NET / Kestrel)
    └── Gestisce REST API e WebSocket handshake

Simulation Thread (SimulationHostedService)
    └── Tick loop:
        ├── Legge comandi dalla ConcurrentQueue
        ├── Esegue tick simulazione
        ├── Passa metriche al MetricsCollector
        ├── Calcola delta
        └── Broadcast a tutti i client

Metrics Flush Thread (MetricsFlushService)
    └── Legge da Channel<T>
    └── Scrive su DB via EF Core (async)

WebSocket I/O (gestiti da Kestrel)
    └── Ricezione messaggi → ConcurrentQueue
    └── Invio messaggi → buffer per client
```

### 14.2 — Lato Client Unity

```
Main Thread
    ├── Update(): legge messaggi dalla ConcurrentQueue, applica delta
    └── LateUpdate(): interpola e renderizza

Background Task (async/await)
    ├── ReceiveLoopAsync(): WebSocket → ConcurrentQueue
    └── SendLoopAsync(): ConcurrentQueue → WebSocket
```

---

## 15. Sicurezza e Validazione

### 15.1 — Il server non si fida del client

Ogni comando ricevuto dal client è validato dal server. Se il comando è invalido, il server manda un `CommandResultMessage` con `Success=false`.

### 15.2 — Autenticazione

- Modalità locale: nessuna (localhost only)
- Modalità enterprise: JWT + HTTPS/WSS
- Modalità cloud: OAuth2 / API key

---

## 16. Convenzioni di Codice

### 16.1 — Namespace

- `StockFlow.Simulation.*` — Simulation Engine (C# puro)
- `StockFlow.Server.*` — Server hosting, API, WebSocket
- `StockFlow.Persistence.*` — EF Core, DbContext, repository
- `StockFlow.Protocol.*` — Messaggi e tipi condivisi
- `StockFlow.Game.*` — Client Unity

### 16.2 — Regole ferree

- `StockFlow.Simulation` non referenzia MAI Server, Persistence, Protocol, o UnityEngine
- `StockFlow.Simulation` comunica con la persistenza SOLO tramite interfacce astratte (`IMetricsSink`)
- `StockFlow.Persistence` non referenzia MAI Simulation — riceve solo DTO
- Il client Unity referenzia SOLO Protocol (come DLL importata)
- Il client non modifica MAI il suo stato locale se non in risposta a un messaggio del server
- Il database non viene MAI toccato nel hot path (tick loop)
- Le write su DB sono SEMPRE asincrone via MetricsFlushService

---

*Documento collegato: GDD Stock Flow v0.2 per game design, componenti, metriche e modello di business.*
