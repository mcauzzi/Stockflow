# Stockflow.Simulation — Documentazione Engine v0.1

Stato attuale dell'implementazione del motore di simulazione (`Sources/Stockflow.Simulation`).

---

## Struttura cartelle

```
Stockflow.Simulation/
├── Core/
│   ├── SimulationEngine.cs
│   ├── SimulationState.cs
│   └── StateDelta.cs
├── Commands/
│   ├── ICommand.cs
│   └── CommandResult.cs
├── Component/
│   ├── ISimComponent.cs
│   ├── OneWayConveyor.cs
│   ├── ComponentType.cs
│   ├── Direction.cs
│   ├── DirectionExtensions.cs
│   ├── Port.cs
│   ├── PortId.cs
│   └── PortDirection.cs
├── Entity/
│   └── ISimEntity.cs
├── Grid/
│   ├── Cell.cs          (contiene anche GridManager)
│   └── GridCoord.cs
├── Modules/
│   └── IComponentModule.cs
└── Routing/
    ├── RoutingGraph.cs
    └── Connection.cs
```

---

## Core

### SimulationEngine

**File:** `Core/SimulationEngine.cs` — namespace `Stockflow.Simulation.Core`

Cuore del sistema. C# puro, zero dipendenze da framework.

```csharp
public class SimulationEngine(int width, int length, int height)
```

| Membro | Tipo | Descrizione |
|---|---|---|
| `TimeScale` | `float` | Moltiplicatore velocità (1 = normale, 2 = doppio) |
| `SimulationTime` | `float` | Tempo simulato accumulato in secondi |
| `Grid` | `GridManager` | Infrastruttura spaziale — piazzamento e lookup componenti |
| `State` | `SimulationState` | Snapshot osservabile corrente |
| `Tick(deltaTime)` | `void` | Avanza la simulazione di un passo |
| `ProcessCommand(cmd)` | `CommandResult` | Esegue un comando; i tipi concreti si aggiungono in #33 |
| `GetStateDelta()` | `StateDelta` | Differenze rispetto all'ultima chiamata |

**Separazione delle responsabilità:**
- `Grid` è macchinario interno dell'engine (infrastruttura spaziale, non snapshot)
- `State` è lo snapshot osservabile da serializzare e trasmettere ai client

**Calcolo deltaTime — responsabilità del caller:**

```csharp
// In SimulationHostedService (Webserver)
engine.Tick(1f / tickRate * engine.TimeScale);
```

L'engine non conosce il tick rate reale; lo riceve già calcolato. Questo permette al server di variare frequenza e velocità indipendentemente.

**GetStateDelta — semantica:**

Traccia internamente l'insieme di ID componenti noti. Ad ogni chiamata restituisce gli ID aggiunti e rimossi rispetto alla chiamata precedente. Verrà espanso con entità (issue #6) e delta completo (issue #8).

### SimulationState

**File:** `Core/SimulationState.cs` — namespace `Stockflow.Simulation.Core`

Snapshot osservabile dello stato corrente. Contiene solo ciò che è rilevante per i client.

| Membro | Tipo | Descrizione |
|---|---|---|
| `Components` | `List<ISimComponent>` | Tutti i componenti attivi |

### StateDelta

**File:** `Core/StateDelta.cs` — namespace `Stockflow.Simulation.Core`

Differenze tra due tick consecutivi. Struttura in espansione progressiva.

| Membro | Tipo | Descrizione |
|---|---|---|
| `SimulationTime` | `float` | Tempo simulato al momento del delta |
| `AddedComponentIds` | `IReadOnlyList<int>` | ID componenti aggiunti dall'ultimo delta |
| `RemovedComponentIds` | `IReadOnlyList<int>` | ID componenti rimossi dall'ultimo delta |

---

## Commands

### ICommand

**File:** `Commands/ICommand.cs`

Interfaccia marker per tutti i comandi interni alla simulazione. Il server traduce i `ClientMessage` (Protocol) in `ICommand` prima di passarli all'engine — la simulazione non dipende mai dal layer di rete.

### CommandResult

**File:** `Commands/CommandResult.cs`

```csharp
public readonly record struct CommandResult(bool Success, string? ErrorMessage = null)
```

Factory methods: `CommandResult.Ok()` e `CommandResult.Fail(string why)`.

---

## Grid

### GridCoord

**File:** `Grid/GridCoord.cs`

```csharp
public readonly record struct GridCoord(int X, int Y, int Floor = 0)
```

Coordinata discreta 3D. `Floor` rappresenta il piano (default 0). Supporta addizione tramite `operator +`.

`CardinalOffsets` espone i quattro offset cardinali (N/S/E/W) come array statico.

### Cell

**File:** `Grid/Cell.cs`

Una singola cella della griglia. Può ospitare al massimo un `ISimComponent`.

| Membro | Tipo | Descrizione |
|---|---|---|
| `Coord` | `GridCoord` | Posizione nella griglia (immutabile) |
| `Component` | `ISimComponent?` | Componente presente, `null` se vuota |
| `IsOccupied` | `bool` | `true` se `Component != null` |

### GridManager

**File:** `Grid/Cell.cs` — esposto da `SimulationEngine.Grid`

Griglia tridimensionale `Width × Length × Height`. Appartiene all'engine, non allo state.

| Metodo | Descrizione |
|---|---|
| `IsInBounds(coord)` | Verifica che la coordinata sia dentro i limiti |
| `TryGetCell(coord, out cell)` | Restituisce la cella se la coordinata è valida |
| `TryPlace(component)` | Piazza il componente sulla cella corrispondente a `component.Position`; fallisce se occupata o fuori bounds |
| `TryRemove(coord)` | Rimuove il componente dalla cella; fallisce se vuota |

Tutti i metodi usano il pattern `Try*` — non throwing.

---

## Componenti

### ISimComponent

**File:** `Component/ISimComponent.cs`

| Membro | Tipo | Descrizione |
|---|---|---|
| `Id` | `int` | Identificatore univoco |
| `Position` | `GridCoord` | Cella occupata nella griglia |
| `Facing` | `Direction` | Orientamento del componente |
| `Type` | `ComponentType` | Costante sulla classe concreta |
| `Modules` | `IReadOnlyList<IComponentModule>` | Moduli comportamentali aggiuntivi |
| `Occupant` | `ISimEntity?` | Entità attualmente sul componente |
| `Ports` | `IReadOnlyList<Port>` | Porte fisiche di ingresso/uscita |
| `Tick(deltaTime)` | `void` | Logica interna per tick |
| `TryAccept(entity, fromPort)` | `bool` | Tenta di accettare un'entità |

### Direction

**File:** `Component/Direction.cs` e `Component/DirectionExtensions.cs`

```csharp
public enum Direction { North, East, South, West }
```

| Metodo | Risultato |
|---|---|
| `ToOffset()` | `GridCoord` offset unitario nella direzione |
| `Opposite()` | Direzione opposta |
| `RotateCW()` | Rotazione 90° orario |
| `RotateCCW()` | Rotazione 90° antiorario |

### Port

**File:** `Component/Port.cs`

```csharp
public readonly record struct Port(PortId Id, GridCoord Position, PortDirection Direction)
```

`Position` è la cella su cui si affaccia la porta (adiacente al componente). `Direction`: `In`, `Out`, `Bidirectional`.

### ComponentType

**File:** `Component/ComponentType.cs`

```csharp
public enum ComponentType { OneWayConveyor }
```

Ogni classe concreta restituisce il proprio tipo come proprietà costante (`Type => ComponentType.OneWayConveyor`), non configurabile via costruttore.

### OneWayConveyor

**File:** `Component/OneWayConveyor.cs`

Nastro trasportatore unidirezionale. Trasporta un'entità dall'ingresso all'uscita.

```csharp
public OneWayConveyor(int id, GridCoord position, Direction facing, float speed,
                      RoutingGraph graph, IReadOnlyList<IComponentModule>? modules = null)
```

**Porte generate automaticamente:**
- `InPort` (Id=0): cella opposta a `Facing`
- `OutPort` (Id=1): cella nella direzione di `Facing`

**Logica di tick:**
1. Nessun occupante → nessuna operazione.
2. `Progress < 1.0` → `Progress += Speed × deltaTime`.
3. `Progress >= 1.0` → tenta trasferimento via `RoutingGraph`. Se ha successo: notifica `OnEntityExit` ai moduli, libera il componente.

`Speed` è in "unità di progresso per secondo simulato".

---

## Routing

### Connection

**File:** `Routing/Connection.cs`

```csharp
public readonly record struct Connection(
    ISimComponent From, PortId FromPort,
    ISimComponent To,   PortId ToPort
)
```

### RoutingGraph

**File:** `Routing/RoutingGraph.cs`

Mappa `(ISimComponent, PortId) → Connection`. Usato dai componenti nel tick per sapere a chi passare l'entità.

| Metodo | Descrizione |
|---|---|
| `Connect(from, fromPort, to, toPort)` | Aggiunge o sovrascrive un collegamento |
| `Disconnect(component, port)` | Rimuove il collegamento in uscita |
| `GetNext(component, outPort)` | Restituisce `Connection?` collegata a quella porta |

Max una connessione per porta di uscita (no fanout).

---

## Entità

### ISimEntity

**File:** `Entity/ISimEntity.cs`

| Membro | Tipo | Descrizione |
|---|---|---|
| `Id` | `int` | Identificatore univoco (init-only) |
| `CurrentComponent` | `ISimComponent` | Componente corrente |
| `CurrentPort` | `PortId` | Porta di arrivo nel componente corrente |
| `Progress` | `float` | Avanzamento: `0.0` ingresso → `1.0` uscita |
| `DestinationComponent` | `ISimComponent?` | Destinazione finale |
| `EntryTime` | `float` | Tempo simulato di ingresso nel sistema (init-only) |

Nessuna implementazione concreta — issue #6.

---

## Moduli

### IComponentModule

**File:** `Modules/IComponentModule.cs`

| Metodo | Quando viene chiamato |
|---|---|
| `OnEntityEnter(entity)` | `TryAccept` con successo |
| `OnEntityExit(entity)` | Trasferimento al componente successivo |
| `OnTick(deltaTime)` | Ogni tick (*non ancora invocato — da implementare*) |

---

## Stato attuale e gap noti

| Componente | Stato |
|---|---|
| `SimulationEngine` + tick loop | Implementato |
| `GridManager` + `Cell` + `GridCoord` | Implementato |
| `ISimComponent` + `OneWayConveyor` | Implementato |
| `RoutingGraph` + `Connection` | Implementato |
| `ICommand` + `CommandResult` | Implementati |
| `StateDelta` (struttura base) | Implementata |
| `IComponentModule` (interfaccia) | Implementata; `OnTick` non ancora invocato |
| `ISimEntity` (interfaccia) | Definita; nessuna classe concreta — issue #6 |
| `EntityManager` | Mancante — issue #6 |
| Entità in `SimulationState` | Mancante — issue #6 |
| `SimulationClock` | Mancante — issue #5 |
| Delta completo (entità + componenti) | Parziale — issue #8 |
| Comandi concreti | Mancanti — issue #33 |
| Logica routing con `DestinationComponent` | Mancante — issue #28 |
