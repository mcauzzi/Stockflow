# DiverterLogic + MergeLogic Lateral Side Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere il parametro `TurnSide side` a `DiverterLogic` e `MergeLogic` così che la porta laterale possa essere configurata sinistra/destra al momento del piazzamento, con propagazione completa fino al frontend Angular.

**Architecture:** Si riusa `TurnSide { Left, Right }` già in `Component/TurnSide.cs`. `Side` è placement-only (non modificabile a runtime): esposto come proprietà read-only nel ConfigSchema e in `ExportProperties`. Il webserver legge `req.Side` da `PlaceComponentRequest` e lo passa al command. Il frontend estende il controllo "SIDE" (già esistente per `conveyor_turn`) a `diverter` e `merge`.

**Tech Stack:** .NET 10, C# 13, xUnit, Angular 21 (Signals).

---

## File map

| File | Operazione |
|---|---|
| `Sources/Stockflow.Simulation/Component/DiverterLogic.cs` | Modifica — aggiunge `Side`, aggiorna costruttore, Schema, ExportProperties |
| `Sources/Stockflow.Simulation/Component/MergeLogic.cs` | Modifica — aggiunge `Side`, aggiorna costruttore + SetFacing, Schema, ExportProperties |
| `Sources/Stockflow.Simulation/Commands/PlaceComponentCommand.cs` | Modifica — aggiunge `TurnSide Side` a entrambi i record |
| `Sources/Stockflow.Simulation/Core/SimulationEngine.cs` | Modifica — factory per diverter e merge passa `x.Side` |
| `Sources/Stockflow.Webserver/Controllers/SimulationController.cs` | Modifica — aggiunge `string? Side` a `PlaceComponentRequest`, aggiorna cases |
| `Sources/Stockflow.Tests.Simulation/DiverterLogicTests.cs` | Modifica — rinomina test esistente, aggiunge test Side=Left, aggiorna `MakeDiverter` |
| `Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs` | Modifica — aggiunge test Side=Right, aggiorna `MakeMerge` |
| `Sources/Stockflow.Console/src/app/features/palette/palette.component.ts` | Modifica — aggiunge `isDiverter`, `hasLateralSide`, aggiorna template |
| `Sources/Stockflow.Console/src/app/app.ts` | Modifica — passa `side` param per diverter e merge |

---

## Task 1: DiverterLogic — Side support

**Files:**
- Modify: `Sources/Stockflow.Simulation/Component/DiverterLogic.cs`
- Modify: `Sources/Stockflow.Tests.Simulation/DiverterLogicTests.cs`

- [ ] **Step 1: Scrivi il test che fallisce**

In `DiverterLogicTests.cs`, aggiorna `MakeDiverter` per accettare `side` e aggiungi il nuovo test. Trova il metodo helper `MakeDiverter` (riga ~11) e la sezione test porte (`Ports_FacingNorth_CorrectPositions`):

```csharp
private static DiverterLogic MakeDiverter(
    RoutingGraph? graph = null,
    TurnSide      side  = TurnSide.Right)
    => new(1, new GridCoord(0, 0), Direction.North, side, 1f, graph ?? new RoutingGraph());
```

Rinomina il test esistente `Ports_FacingNorth_CorrectPositions` → `Ports_FacingNorth_SideRight_CorrectPositions` (nessuna logica cambia, il default è Right).

Aggiungi il nuovo test dopo di esso:

```csharp
[Fact]
public void Ports_FacingNorth_SideLeft_CorrectPositions()
{
    var diverter = MakeDiverter(side: TurnSide.Left);

    Assert.Equal(new GridCoord(0,  1), diverter.Ports[0].Position); // InPort  → South
    Assert.Equal(PortDirection.In,     diverter.Ports[0].Direction);

    Assert.Equal(new GridCoord(0, -1), diverter.Ports[1].Position); // OutPort0 → North (dritto)
    Assert.Equal(PortDirection.Out,    diverter.Ports[1].Direction);

    Assert.Equal(new GridCoord(-1, 0), diverter.Ports[2].Position); // OutPort1 → West (sinistra)
    Assert.Equal(PortDirection.Out,    diverter.Ports[2].Direction);
}
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```
dotnet test Sources/Stockflow.Tests.Simulation/ --filter "FullyQualifiedName~DiverterLogicTests.Ports_FacingNorth_SideLeft"
```

Expected: errore di compilazione — `TurnSide side` non esiste nel costruttore.

- [ ] **Step 3: Implementa la modifica a DiverterLogic.cs**

Sostituisci il costruttore e i campi privati in `Sources/Stockflow.Simulation/Component/DiverterLogic.cs`. Il file completo aggiornato:

```csharp
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Modules;
using Stockflow.Simulation.Routing;

namespace Stockflow.Simulation.Component;

public class DiverterLogic : ISimComponent
{
    public  int                             Id       { get; }
    public  GridCoord                       Position { get; }
    public  Direction                       Facing   { get; }
    public  TurnSide                        Side     { get; }
    public  ComponentType                   Type     => ComponentType.DiverterLogic;
    public  IReadOnlyList<IComponentModule> Modules  { get; }
    public  SimEntity?                      Occupant { get; private set; }
    public  IReadOnlyList<Port>             Ports    { get; }
    public  float                           Speed    { get; set; }
    public  RoutingGraph                    Graph    { get; }
    public  IRoutingRule                    Rule     { get; private set; }

    private readonly Port     _inPort;
    private readonly Port     _outPort0;
    private readonly Port     _outPort1;
    private readonly PortId[] _outputPorts;

    private static readonly PortId _portIn   = new(0);
    private static readonly PortId _portOut0 = new(1);
    private static readonly PortId _portOut1 = new(2);

    public DiverterLogic(int id, GridCoord position, Direction facing, TurnSide side, float speed,
                         RoutingGraph graph, IRoutingRule? rule = null,
                         IReadOnlyList<IComponentModule>? modules = null)
    {
        Id       = id;
        Position = position;
        Facing   = facing;
        Side     = side;
        Speed    = speed;
        Graph    = graph;
        Rule     = rule ?? new RoundRobinRoutingRule();
        Modules  = modules ?? [];

        var lateralDir = side == TurnSide.Right ? facing.RotateCW() : facing.RotateCCW();
        _inPort      = new(_portIn,   Position + facing.Opposite().ToOffset(), PortDirection.In);
        _outPort0    = new(_portOut0, Position + facing.ToOffset(),            PortDirection.Out);
        _outPort1    = new(_portOut1, Position + lateralDir.ToOffset(),        PortDirection.Out);
        _outputPorts = [_portOut0, _portOut1];
        Ports        = [_inPort, _outPort0, _outPort1];
    }

    // --- ConfigSchema ---

    public static readonly PropertySchema[] Schema =
    [
        new("speed",   "Speed (m/s)",  PropertyType.Float, DefaultValue: "1",       Min: "0.01", Max: "10"),
        new("routing", "Routing Rule", PropertyType.Enum,  DefaultValue: RoutingRuleFactory.RoundRobin,
            EnumValues: RoutingRuleFactory.AvailableRules),
        new("side",    "Lateral Side", PropertyType.Enum,  DefaultValue: "right",
            EnumValues: ["left", "right"], IsReadOnly: true),
    ];

    public IReadOnlyList<PropertySchema> ConfigSchema => Schema;

    public string? ApplyConfig(IReadOnlyDictionary<string, string> properties)
    {
        foreach (var (key, value) in properties)
        {
            var schema = Schema.FirstOrDefault(s => s.Key == key);
            if (schema is null || schema.IsReadOnly) continue;

            var error = schema.Validate(value);
            if (error is not null) return error;

            if (key == "speed")
                Speed = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

            if (key == "routing")
                Rule = RoutingRuleFactory.Create(value);
        }
        return null;
    }

    public Dictionary<string, string> ExportProperties() => new()
    {
        ["speed"]   = Speed.ToString("F3"),
        ["routing"] = RoutingRuleFactory.NameOf(Rule),
        ["side"]    = Side == TurnSide.Right ? "right" : "left",
    };

    public void Tick(float deltaTime)
    {
        if (Occupant == null) return;

        if (Occupant.Progress < 1.0f)
        {
            Occupant.Progress += Speed * deltaTime;
            return;
        }

        var targetPort = Rule.SelectOutput(Occupant, _outputPorts);
        var next       = Graph.GetNext(this, targetPort);
        if (next == null) return;

        if (next.Value.To.TryAccept(Occupant, next.Value.ToPort))
        {
            foreach (var module in Modules)
                module.OnEntityExit(Occupant);
            Rule.OnTransferSucceeded(targetPort);
            Occupant = null;
        }
    }

    public bool TryAccept(SimEntity entity, PortId fromPort)
    {
        if (Occupant != null) return false;

        Occupant                = entity;
        entity.CurrentComponent = this;
        entity.CurrentPort      = fromPort;
        entity.Progress         = 0.0f;

        foreach (var module in Modules)
            module.OnEntityEnter(entity);

        return true;
    }
}
```

> **Nota:** Verifica il contenuto attuale di `ApplyConfig` e `ExportProperties` nel file — se `RoutingRuleFactory.NameOf` o `RoutingRuleFactory.Create` non esistono ancora, usa i metodi effettivi presenti nel file. Il punto critico è aggiungere `Side` al costruttore, la logica `lateralDir`, la proprietà pubblica `Side`, e `["side"]` in `ExportProperties`.

- [ ] **Step 4: Esegui tutti i test DiverterLogic**

```
dotnet test Sources/Stockflow.Tests.Simulation/ --filter "FullyQualifiedName~DiverterLogicTests"
```

Expected: tutti i test passano, incluso il nuovo `Ports_FacingNorth_SideLeft_CorrectPositions`.

- [ ] **Step 5: Build completo per verificare nessuna regressione**

```
dotnet build Stockflow.slnx
```

Expected: 0 errori. Se ci sono errori su `new DiverterLogic(...)` con argomenti posizionali, aggiorna i call site (es. in `SimulationEngine.cs` factory) aggiungendo `TurnSide.Right` come quarto argomento — ma non cambiare la factory di `SimulationEngine` in modo permanente ancora (lo fa Task 3).

- [ ] **Step 6: Commit**

```
git add Sources/Stockflow.Simulation/Component/DiverterLogic.cs
git add Sources/Stockflow.Tests.Simulation/DiverterLogicTests.cs
git commit -m "feat(simulation): DiverterLogic aggiunge TurnSide Side al costruttore"
```

---

## Task 2: MergeLogic — Side support

**Files:**
- Modify: `Sources/Stockflow.Simulation/Component/MergeLogic.cs`
- Modify: `Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs`

- [ ] **Step 1: Scrivi il test che fallisce**

In `MergeLogicTests.cs`, aggiorna `MakeMerge` e aggiungi il nuovo test. Trova il metodo helper `MakeMerge` (riga ~10) e modificalo:

```csharp
private static MergeLogic MakeMerge(
    MergeMode     mode  = MergeMode.Alternating,
    RoutingGraph? graph = null,
    TurnSide      side  = TurnSide.Left)
    => new(1, new GridCoord(0, 0), Direction.North, mode, side, 1f,
           graph ?? new RoutingGraph());
```

Aggiungi il nuovo test dopo `SetFacing_RebuildsPortPositions`:

```csharp
[Fact]
public void Ports_FacingNorth_SideRight_CorrectPositions()
{
    var merge = MakeMerge(side: TurnSide.Right);

    // Facing=North, Side=Right:
    // InPort0 (0): South = (0, 1)   — ingresso primario, invariato
    // InPort1 (1): East  = (1, 0)   — ingresso secondario a destra
    // OutPort (2): North = (0,-1)   — uscita, invariata
    Assert.Equal(new GridCoord(0,  1), merge.Ports[0].Position);
    Assert.Equal(PortDirection.In,     merge.Ports[0].Direction);

    Assert.Equal(new GridCoord(1,  0), merge.Ports[1].Position);
    Assert.Equal(PortDirection.In,     merge.Ports[1].Direction);

    Assert.Equal(new GridCoord(0, -1), merge.Ports[2].Position);
    Assert.Equal(PortDirection.Out,    merge.Ports[2].Direction);
}

[Fact]
public void SetFacing_WithSideRight_UsesRotateCW()
{
    var merge = MakeMerge(side: TurnSide.Right);
    merge.SetFacing(Direction.East);

    // Facing=East, Side=Right:
    // InPort0 (0): West  = (-1, 0)
    // InPort1 (1): South = (0,  1)   — RotateCW di East
    // OutPort (2): East  = ( 1, 0)
    Assert.Equal(new GridCoord(-1, 0), merge.Ports[0].Position);
    Assert.Equal(new GridCoord(0,  1), merge.Ports[1].Position);
    Assert.Equal(new GridCoord(1,  0), merge.Ports[2].Position);
}
```

- [ ] **Step 2: Esegui i test per verificare che falliscono**

```
dotnet test Sources/Stockflow.Tests.Simulation/ --filter "FullyQualifiedName~MergeLogicTests.Ports_FacingNorth_SideRight"
```

Expected: errore di compilazione — `TurnSide side` non esiste nel costruttore.

- [ ] **Step 3: Implementa la modifica a MergeLogic.cs**

In `Sources/Stockflow.Simulation/Component/MergeLogic.cs`:

**3a.** Aggiungi la proprietà pubblica dopo `Mode`:
```csharp
public  MergeMode                       Mode     { get; set; }
public  TurnSide                        Side     { get; }
```

**3b.** Aggiorna il costruttore (riga ~33) — aggiungi `TurnSide side` dopo `mode`:
```csharp
public MergeLogic(int id, GridCoord position, Direction facing, MergeMode mode, TurnSide side,
                  float speed, RoutingGraph graph,
                  IReadOnlyList<IComponentModule>? modules = null)
{
    Id          = id;
    Position    = position;
    Mode        = mode;
    Side        = side;
    Speed       = speed;
    Graph       = graph;
    Modules     = modules ?? [];
    _activePort = _port0;
    SetFacing(facing);
}
```

**3c.** Aggiorna `SetFacing` (riga ~46) per usare `Side`:
```csharp
public void SetFacing(Direction facing)
{
    var lateralDir = Side == TurnSide.Left ? facing.RotateCCW() : facing.RotateCW();
    Facing   = facing;
    _inPort0 = new(_port0, Position + facing.Opposite().ToOffset(), PortDirection.In);
    _inPort1 = new(_port1, Position + lateralDir.ToOffset(),        PortDirection.In);
    _outPort = new(_port2, Position + facing.ToOffset(),            PortDirection.Out);
    _ports   = [_inPort0, _inPort1, _outPort];
}
```

**3d.** Aggiorna `Schema` (riga ~57) — aggiungi entry `"side"`:
```csharp
public static readonly PropertySchema[] Schema =
[
    new("mode",  "Merge Mode",  PropertyType.Enum,  DefaultValue: "alternating", EnumValues: ["alternating", "priority"]),
    new("speed", "Speed (m/s)", PropertyType.Float, DefaultValue: "1",           Min: "0.01", Max: "10"),
    new("side",  "Lateral Side", PropertyType.Enum, DefaultValue: "left",
        EnumValues: ["left", "right"], IsReadOnly: true),
];
```

**3e.** Aggiorna `ExportProperties` (riga ~89):
```csharp
public Dictionary<string, string> ExportProperties() => new()
{
    ["mode"]  = Mode == MergeMode.Priority ? "priority" : "alternating",
    ["speed"] = Speed.ToString("F3"),
    ["side"]  = Side == TurnSide.Left ? "left" : "right",
};
```

- [ ] **Step 4: Esegui tutti i test MergeLogic**

```
dotnet test Sources/Stockflow.Tests.Simulation/ --filter "FullyQualifiedName~MergeLogicTests"
```

Expected: tutti i test passano. `SetFacing_RebuildsPortPositions` continua a passare perché il default è `Side=Left` = `RotateCCW` = stesso comportamento precedente.

- [ ] **Step 5: Esegui la suite completa**

```
dotnet test
```

Expected: tutti i test passano, 0 errori.

- [ ] **Step 6: Commit**

```
git add Sources/Stockflow.Simulation/Component/MergeLogic.cs
git add Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs
git commit -m "feat(simulation): MergeLogic aggiunge TurnSide Side al costruttore e SetFacing"
```

---

## Task 3: Commands + Engine factory

**Files:**
- Modify: `Sources/Stockflow.Simulation/Commands/PlaceComponentCommand.cs`
- Modify: `Sources/Stockflow.Simulation/Core/SimulationEngine.cs`

- [ ] **Step 1: Aggiorna i command record**

In `Sources/Stockflow.Simulation/Commands/PlaceComponentCommand.cs`, modifica i due record:

```csharp
public sealed record PlaceDiverterLogicCommand(
    GridCoord Position,
    Direction Facing,
    TurnSide  Side  = TurnSide.Right,
    float     Speed = 1f
) : ICommand;

public sealed record PlaceMergeLogicCommand(
    GridCoord Position,
    Direction Facing,
    MergeMode Mode  = MergeMode.Alternating,
    TurnSide  Side  = TurnSide.Left,
    float     Speed = 1f
) : ICommand;
```

- [ ] **Step 2: Aggiorna le factory in SimulationEngine.cs**

In `Sources/Stockflow.Simulation/Core/SimulationEngine.cs`, trova le factory per `PlaceDiverterLogicCommand` e `PlaceMergeLogicCommand` nel dizionario `_placementFactories` (riga ~50) e aggiornale:

```csharp
[typeof(PlaceMergeLogicCommand)] = (c, id) =>
{
    var x = (PlaceMergeLogicCommand)c;
    return new MergeLogic(id, x.Position, x.Facing, x.Mode, x.Side, x.Speed, Graph);
},
[typeof(PlaceDiverterLogicCommand)] = (c, id) =>
{
    var x = (PlaceDiverterLogicCommand)c;
    return new DiverterLogic(id, x.Position, x.Facing, x.Side, x.Speed, Graph);
},
```

- [ ] **Step 3: Build + test completo**

```
dotnet build Stockflow.slnx
dotnet test
```

Expected: build OK, tutti i test passano. I test esistenti `PlaceDiverterLogic_*` e `PlaceMergeLogic_*` continuano a passare perché i default sono invariati.

- [ ] **Step 4: Commit**

```
git add Sources/Stockflow.Simulation/Commands/PlaceComponentCommand.cs
git add Sources/Stockflow.Simulation/Core/SimulationEngine.cs
git commit -m "feat(simulation): PlaceDiverterLogicCommand e PlaceMergeLogicCommand aggiungono Side"
```

---

## Task 4: Webserver — propagazione Side

**Files:**
- Modify: `Sources/Stockflow.Webserver/Controllers/SimulationController.cs`

- [ ] **Step 1: Aggiungi `Side` a `PlaceComponentRequest`**

In fondo al file `Sources/Stockflow.Webserver/Controllers/SimulationController.cs` (riga ~215), modifica il record:

```csharp
public sealed record PlaceComponentRequest(
    string  Kind,
    int     GridX,
    int     GridY,
    string? Facing    = "North",
    float?  SpawnRate = null,
    string? Sku       = null,
    float?  Weight    = null,
    float?  Size      = null,
    string? Turn      = null,
    float?  Speed     = null,
    string? Mode      = null,
    string? Side      = null);
```

- [ ] **Step 2: Aggiorna i case `diverter` e `merge` in `PlaceComponent`**

In `PlaceComponent` (riga ~118), aggiorna i due case:

```csharp
ComponentKinds.MergeLogic => new PlaceMergeLogicCommand(
    pos,
    dir,
    req.Mode == "priority" ? MergeMode.Priority : MergeMode.Alternating,
    req.Side == "Left"     ? TurnSide.Left       : TurnSide.Right,
    req.Speed ?? 1f),
ComponentKinds.DiverterLogic => new PlaceDiverterLogicCommand(
    pos,
    dir,
    req.Side == "Left" ? TurnSide.Left : TurnSide.Right,
    req.Speed ?? 1f),
```

> **Nota:** Il confronto `req.Side == "Left"` usa exact case (come il pattern esistente `req.Turn == "Left"` per ConveyorTurn). Il frontend Angular invia `"Left"` o `"Right"` con iniziale maiuscola.

- [ ] **Step 3: Build + test**

```
dotnet build Stockflow.slnx
dotnet test
```

Expected: 0 errori, tutti i test passano.

- [ ] **Step 4: Commit**

```
git add Sources/Stockflow.Webserver/Controllers/SimulationController.cs
git commit -m "feat(webserver): PlaceComponentRequest aggiunge Side per diverter e merge"
```

---

## Task 5: Frontend Angular — controllo Side in palette

**Files:**
- Modify: `Sources/Stockflow.Console/src/app/features/palette/palette.component.ts`
- Modify: `Sources/Stockflow.Console/src/app/app.ts`

- [ ] **Step 1: Aggiungi getter in palette.component.ts**

In `Sources/Stockflow.Console/src/app/features/palette/palette.component.ts`, trova i getter `isConveyorTurn` e `isMerge` (riga ~217) e aggiungi `isDiverter` e `hasLateralSide` subito dopo:

```typescript
get isConveyorTurn():  boolean { return this.selectedItem?.kind === 'conveyor_turn'; }
get isMerge():         boolean { return this.selectedItem?.kind === 'merge'; }
get isDiverter():      boolean { return this.selectedItem?.kind === 'diverter'; }
get hasLateralSide():  boolean { return this.isConveyorTurn || this.isDiverter || this.isMerge; }
```

- [ ] **Step 2: Aggiorna il template in palette.component.ts**

Nel template HTML inline (riga ~60), trova il blocco "Turn side selector":

```html
<!-- Turn side selector (shown only for conveyor_turn) -->
<div class="facing" *ngIf="isConveyorTurn">
  <div class="facing-lbl">TURN SIDE</div>
```

Sostituiscilo con:

```html
<!-- Side selector (conveyor_turn, diverter, merge) -->
<div class="facing" *ngIf="hasLateralSide">
  <div class="facing-lbl">SIDE</div>
```

- [ ] **Step 3: Aggiorna app.ts — passa `side` per diverter e merge**

In `Sources/Stockflow.Console/src/app/app.ts`, trova il blocco di costruzione `params` prima di `this.sim.placeComponent(...)`. Aggiungi le due righe per diverter e merge:

```typescript
if (kind === 'conveyor_turn') params['turn'] = this.placeTurnSide();
if (kind === 'diverter')      params['side'] = this.placeTurnSide();
if (kind === 'merge') {
  params['speed'] = this.placeSpeed();
  params['mode']  = this.placeMergeMode();
  params['side']  = this.placeTurnSide();
}
```

- [ ] **Step 4: Build Angular**

```
node Sources/Stockflow.Console/node_modules/@angular/cli/bin/ng.js build --configuration development --project Stockflow.Console
```

Expected: 0 errori TypeScript.

- [ ] **Step 5: Commit**

```
git add Sources/Stockflow.Console/src/app/features/palette/palette.component.ts
git add Sources/Stockflow.Console/src/app/app.ts
git commit -m "feat(console): palette mostra controllo SIDE per diverter e merge"
```

---

## Task 6: Build + test finale

- [ ] **Step 1: Build completo**

```
dotnet build Stockflow.slnx
```

Expected: Build succeeded, 0 errori.

- [ ] **Step 2: Test suite completa**

```
dotnet test
```

Expected: tutti i test passano (baseline era 69+ test su develop).

- [ ] **Step 3: Build Angular finale**

```
node Sources/Stockflow.Console/node_modules/@angular/cli/bin/ng.js build --configuration development --project Stockflow.Console
```

Expected: 0 errori.

---

## Self-review

**Spec coverage:**
- ✅ DiverterLogic: `TurnSide side` al costruttore, `_portOut1` dinamico → Task 1
- ✅ MergeLogic: `TurnSide side` al costruttore + `SetFacing` aggiornato → Task 2
- ✅ ConfigSchema read-only `"side"` su entrambi → Tasks 1, 2
- ✅ `ExportProperties` espone `"side"` → Tasks 1, 2 (usato da `ComponentSerializer.BuildProperties` che delega a `ExportProperties`)
- ✅ `PlaceDiverterLogicCommand` e `PlaceMergeLogicCommand` aggiornati → Task 3
- ✅ `SimulationEngine` factory passa `x.Side` → Task 3
- ✅ `PlaceComponentRequest` ha `string? Side` → Task 4
- ✅ Controller usa `req.Side` per diverter e merge → Task 4
- ✅ Frontend: `hasLateralSide` getter, template aggiornato, `params['side']` → Task 5
- ✅ Default DiverterLogic=Right, MergeLogic=Left: comportamento precedente invariato

**Placeholder scan:** nessun TBD/TODO. Nota al Task 1 Step 3 avverte di verificare i metodi `RoutingRuleFactory` effettivi.

**Type consistency:** `TurnSide` usato ovunque, nessun mismatch. `req.Side == "Left"` → stesso pattern di `req.Turn == "Left"` nel controller esistente.
