# Stockflow.Simulation — Documentazione Engine v0.1

Stato attuale dell'implementazione del motore di simulazione (`Sources/Stockflow.Simulation`).

---

## Struttura cartelle

```
Stockflow.Simulation/
├── Core/
│   ├── SimulationEngine.cs
│   ├── SimulationClock.cs
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
│   ├── SimEntity.cs      (in Entity.cs)
│   ├── EntityStatus.cs
│   ├── EntityState.cs
│   └── EntityManager.cs
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
| `Clock` | `SimulationClock` | Gestione del tempo simulato |
| `TimeScale` | `float` | Proxy per `Clock.TimeScale` — moltiplicatore velocità |
| `SimulationTime` | `float` | Proxy per `Clock.SimulatedTime` — tempo accumulato in secondi |
| `Grid` | `GridManager` | Infrastruttura spaziale — piazzamento e lookup componenti |
| `State` | `SimulationState` | Snapshot osservabile corrente |
| `Tick(deltaTime)` | `void` | Avanza la simulazione di un passo |
| `ProcessCommand(cmd)` | `CommandResult` | Esegue un comando; i tipi concreti si aggiungono in #33 |
| `GetStateDelta()` | `StateDelta` | Differenze rispetto all'ultima chiamata |

**Separazione delle responsabilità:**
- `Clock` gestisce il tempo simulato; l'engine espone `TimeScale` e `SimulationTime` come proxy per retrocompatibilità col webserver
- `Grid` è macchinario interno dell'engine (infrastruttura spaziale, non snapshot)
- `State` è lo snapshot osservabile da serializzare e trasmettere ai client

**Calcolo deltaTime — responsabilità del caller:**

```csharp
// In SimulationHostedService (Webserver)
engine.Tick(1f / tickRate * engine.TimeScale);
```

L'engine non conosce il tick rate reale; lo riceve già calcolato. Questo permette al server di variare frequenza e velocità indipendentemente.

**GetStateDelta — semantica:**

Traccia internamente gli insiemi di ID componenti e ID entità noti. Ad ogni chiamata restituisce aggiunte e rimozioni rispetto alla chiamata precedente. Verrà espanso con delta completo (issue #8).

### SimulationClock

**File:** `Core/SimulationClock.cs` — namespace `Stockflow.Simulation.Core`

Gestisce il tempo simulato in modo indipendente dall'engine.

| Membro | Tipo | Descrizione |
|---|---|---|
| `SimulatedTime` | `float` | Tempo simulato accumulato in secondi |
| `TimeScale` | `float` | Moltiplicatore velocità: 1x, 2x, 5x, 10x |
| `IsLiveMode` | `bool` | `true` quando `TimeScale == 1f`; Phase 2: bloccato con connessioni WMS attive |
| `Advance(realDelta)` | `void` | `SimulatedTime += realDelta * TimeScale` |

### SimulationState

**File:** `Core/SimulationState.cs` — namespace `Stockflow.Simulation.Core`

Snapshot osservabile dello stato corrente. Contiene solo ciò che è rilevante per i client.

| Membro | Tipo | Descrizione |
|---|---|---|
| `Components` | `List<ISimComponent>` | Tutti i componenti attivi |
| `Entities` | `EntityManager` | Gestione CRUD e query delle entità attive |

### StateDelta

**File:** `Core/StateDelta.cs` — namespace `Stockflow.Simulation.Core`

Differenze tra due tick consecutivi. Struttura in espansione progressiva.

| Membro | Tipo | Descrizione |
|---|---|---|
| `SimulationTime` | `float` | Tempo simulato al momento del delta |
| `AddedComponentIds` | `IReadOnlyList<int>` | ID componenti aggiunti dall'ultimo delta |
| `RemovedComponentIds` | `IReadOnlyList<int>` | ID componenti rimossi dall'ultimo delta |
| `AddedEntityStates` | `IReadOnlyList<EntityState>` | Snapshot delle entità entrate nel sistema dall'ultimo delta |
| `RemovedEntityIds` | `IReadOnlyList<int>` | ID delle entità uscite dal sistema dall'ultimo delta |

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
| `Occupant` | `SimEntity?` | Entità attualmente sul componente |
| `Ports` | `IReadOnlyList<Port>` | Porte fisiche di ingresso/uscita |
| `Tick(deltaTime)` | `void` | Logica interna per tick |
| `TryAccept(entity, fromPort)` | `bool` | Tenta di accettare una `SimEntity` |

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

### SimEntity

**File:** `Entity/Entity.cs` — namespace `Stockflow.Simulation.Entity`

Unità di carico che si muove attraverso i componenti. Classe concreta senza interfaccia — le entità sono puri data carrier, nessuna variazione comportamentale.

| Membro | Tipo | Descrizione |
|---|---|---|
| `Id` | `int` | Identificatore univoco assegnato da `EntityManager` |
| `Sku` | `string` | Codice articolo |
| `Weight` | `float` | Peso in kg |
| `Size` | `float` | Dimensione (unità generiche) |
| `EntryTime` | `float` | Tempo simulato di ingresso nel sistema |
| `CurrentComponent` | `ISimComponent` | Componente corrente |
| `CurrentPort` | `PortId` | Porta di arrivo nel componente corrente |
| `Progress` | `float` | Avanzamento: `0.0` ingresso → `1.0` uscita |
| `DestinationComponent` | `ISimComponent?` | Destinazione finale |
| `Status` | `EntityStatus` | Stato macchina corrente |

`Reset()` è `internal` — usato da `EntityManager` per restituire l'istanza al pool.

### EntityStatus

**File:** `Entity/EntityStatus.cs`

```csharp
public enum EntityStatus { Idle, Moving, Queued }
```

### EntityState

**File:** `Entity/EntityState.cs`

Snapshot serializzabile per sincronizzazione di rete. Contiene solo tipi primitivi, nessun riferimento a oggetti C#.

| Membro | Tipo | Descrizione |
|---|---|---|
| `Id` | `int` | Identificatore univoco |
| `Sku` | `string` | Codice articolo |
| `CurrentComponentId` | `int` | ID del componente corrente |
| `CurrentPort` | `PortId` | Porta nel componente corrente |
| `Progress` | `float` | Avanzamento sul componente |
| `Status` | `EntityStatus` | Stato macchina |

Factory method: `EntityState.From(SimEntity e)`.

### EntityManager

**File:** `Entity/EntityManager.cs`

Gestisce il ciclo di vita delle entità. Implementa un object pool con `Queue<SimEntity>` per evitare allocazioni nel hot path.

| Metodo | Descrizione |
|---|---|
| `Spawn(sku, weight, size, entryTime, startComponent, startPort)` | Prende dal pool (o crea) e inizializza una nuova entità attiva |
| `Despawn(id)` | Rimuove dall'attivo, esegue `Reset()` e restituisce al pool |
| `GetAll()` | Restituisce tutte le entità attive (`IReadOnlyCollection<SimEntity>`) |
| `GetByComponent(componentId)` | Filtra le entità attive per componente corrente |

`EntityManager` vive su `SimulationState.Entities`.

---

## Moduli

### IComponentModule

**File:** `Modules/IComponentModule.cs`

| Metodo | Quando viene chiamato |
|---|---|
| `OnEntityEnter(SimEntity entity)` | `TryAccept` con successo |
| `OnEntityExit(SimEntity entity)` | Trasferimento al componente successivo |
| `OnTick(deltaTime)` | Ogni tick (*non ancora invocato — da implementare*) |

---

## Stato attuale e gap noti

| Componente | Stato |
|---|---|
| `SimulationEngine` + tick loop | ✅ Implementato |
| `SimulationClock` (`SimulatedTime`, `TimeScale`, `IsLiveMode`, `Advance`) | ✅ Implementato (#5) |
| `GridManager` + `Cell` + `GridCoord` | ✅ Implementato |
| `ISimComponent` + `OneWayConveyor` | ✅ Implementato |
| `RoutingGraph` + `Connection` | ✅ Implementato |
| `ICommand` + `CommandResult` | ✅ Implementati |
| `StateDelta` (componenti + entità) | ✅ Implementata (#6) |
| `IComponentModule` (interfaccia) | ✅ Implementata; `OnTick` non ancora invocato |
| `SimEntity` + `EntityStatus` | ✅ Implementati (#6) |
| `EntityState` (snapshot rete) | ✅ Implementato (#6) |
| `EntityManager` (CRUD + object pool) | ✅ Implementato (#6) |
| Entità in `SimulationState` | ✅ Implementato (#6) |
| Delta completo (posizioni aggiornate tick-by-tick) | Parziale — issue #8 |
| Comandi concreti | Mancanti — issue #33 |
| Logica routing con `DestinationComponent` | Mancante — issue #28 |
