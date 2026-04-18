# Stockflow.Simulation — Documentazione Engine v0.1

Stato attuale dell'implementazione del motore di simulazione (`Sources/Stockflow.Simulation`).

---

## Panoramica

Il motore è una **class library zero-dependency** (nessun NuGet, nessun framework ASP.NET). Tutta la logica di simulazione vive qui; i layer superiori (Webserver, Persistence) sono read-only rispetto allo stato.

```
SimulationEngine
  └── SimulationState
        ├── GridManager          ← spazio fisico 3D
        └── List<ISimComponent>  ← componenti attivi
```

Il loop di tick è esterno all'engine: il chiamante invoca `Tick()` alla frequenza desiderata (tipicamente 20 Hz).

---

## SimulationEngine

**File:** `SimulationEngine.cs`

```csharp
public class SimulationEngine(int width, int length, int height)
```

| Membro | Tipo | Descrizione |
|---|---|---|
| `Timescale` | `float` | Moltiplicatore velocità (1 = normale, 2 = doppio) |
| `State` | `SimulationState` | Stato corrente della simulazione |
| `Tick()` | `void` | Avanza la simulazione di un passo |

### Calcolo deltaTime

```
deltaTime = BaseTickDuration × Timescale
BaseTickDuration = 1/20 s  (costante, 20 Hz nominali)
```

Con `Timescale = 2` il tempo simulato avanza il doppio per ogni tick reale.

---

## SimulationState

**File:** `SimulationState.cs`

Contenitore immutabile dello stato. Creato da `SimulationEngine` e non sostituito durante la vita dell'engine.

| Membro | Tipo | Descrizione |
|---|---|---|
| `Grid` | `GridManager` | Griglia 3D del magazzino |
| `Components` | `List<ISimComponent>` | Tutti i componenti attivi |

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
| `Component` | `ISimComponent?` | Componente presente sulla cella, `null` se vuota |
| `IsOccupied` | `bool` | `true` se `Component != null` |

### GridManager

**File:** `Grid/Cell.cs`

Griglia tridimensionale `Width × Length × Height` di celle. Asse semantico: X = colonne, Y = righe, Floor = piani.

| Membro | Descrizione |
|---|---|
| `IsInBounds(coord)` | Verifica che la coordinata sia dentro i limiti |
| `TryGetCell(coord, out cell)` | Restituisce la cella se la coordinata è valida |
| `TryPlace(component)` | Piazza il componente sulla cella corrispondente a `component.Position`; fallisce se fuori bounds o occupata |
| `TryRemove(coord)` | Rimuove il componente dalla cella; fallisce se la cella è vuota |

Tutti i metodi sono **non-throwing**: usano il pattern `Try*` per comunicare il fallimento.

---

## Componenti

### ISimComponent

**File:** `Component/ISimComponent.cs`

Interfaccia che ogni componente fisico del magazzino deve implementare.

| Membro | Tipo | Descrizione |
|---|---|---|
| `Id` | `int` | Identificatore univoco |
| `Position` | `GridCoord` | Cella occupata nella griglia |
| `Facing` | `Direction` | Direzione verso cui è orientato il componente |
| `Type` | `ComponentType` | Tipo (costante sulla classe concreta) |
| `Modules` | `IReadOnlyList<IComponentModule>` | Moduli comportamentali aggiuntivi |
| `Occupant` | `ISimEntity?` | Entità attualmente sul componente |
| `Ports` | `IReadOnlyList<Port>` | Porte fisiche di ingresso/uscita |
| `Tick(deltaTime)` | `void` | Logica interna, chiamata ogni tick |
| `TryAccept(entity, fromPort)` | `bool` | Tenta di accettare un'entità; `false` se il componente è occupato |

### Direction

**File:** `Component/Direction.cs` e `Component/DirectionExtensions.cs`

```csharp
public enum Direction { North, East, South, West }
```

Metodi di estensione disponibili:

| Metodo | Risultato |
|---|---|
| `ToOffset()` | `GridCoord` con offset unitario nella direzione |
| `Opposite()` | Direzione opposta |
| `RotateCW()` | Rotazione 90° orario |
| `RotateCCW()` | Rotazione 90° antiorario |

### Port

**File:** `Component/Port.cs`

```csharp
public readonly record struct Port(PortId Id, GridCoord Position, PortDirection Direction)
```

Porta fisica di un componente. `Position` è la cella su cui si affaccia (adiacente al componente). `Direction` può essere `In`, `Out` o `Bidirectional`.

### PortId

**File:** `Component/PortId.cs`

```csharp
public readonly record struct PortId(int Index)
```

Wrapper tipizzato attorno a un indice intero. Evita di confondere ID di porte con altri `int`.

### ComponentType

**File:** `Component/ComponentType.cs`

```csharp
public enum ComponentType { OneWayConveyor }
```

Enum dei tipi di componente disponibili. Ogni classe concreta restituisce il proprio valore come costante (non configurabile a runtime).

### OneWayConveyor

**File:** `Component/OneWayConveyor.cs`

Componente nastro trasportatore unidirezionale. Trasporta un'entità dall'ingresso all'uscita in base alla propria velocità e orientamento.

```csharp
public OneWayConveyor(int id, GridCoord position, Direction facing, float speed,
                      RoutingGraph graph, IReadOnlyList<IComponentModule>? modules = null)
```

**Porte generate automaticamente:**
- `InPort` (Id=0): cella opposta alla direzione di `Facing`
- `OutPort` (Id=1): cella nella direzione di `Facing`

**Logica di tick:**
1. Se non c'è occupante: nessuna operazione.
2. Se `Progress < 1.0`: incrementa `Progress += Speed × deltaTime`.
3. Se `Progress >= 1.0`: tenta di trasferire l'entità al componente successivo via `RoutingGraph`. Se il trasferimento ha successo, notifica i moduli con `OnEntityExit` e libera il componente.

`Speed` è espresso in "unità di progresso per secondo simulato" (1.0 = attraversamento completo in 1 secondo con `Timescale = 1`).

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

Descrive un collegamento diretto tra la porta di uscita di un componente e la porta di ingresso di un altro.

### RoutingGraph

**File:** `Routing/RoutingGraph.cs`

Grafo diretto che mappa `(componente, porta di uscita) → Connection`. Usato dai componenti durante il tick per sapere a chi passare l'entità.

| Metodo | Descrizione |
|---|---|
| `Connect(from, fromPort, to, toPort)` | Aggiunge o sovrascrive un collegamento |
| `Disconnect(component, port)` | Rimuove il collegamento in uscita da quella porta |
| `GetNext(component, outPort)` | Restituisce la `Connection?` collegata a quella porta di uscita |

Un componente può avere al massimo **una connessione per porta di uscita** (il grafo non supporta fanout).

---

## Entità

### ISimEntity

**File:** `Entity/ISimEntity.cs`

Interfaccia per qualsiasi oggetto che si muove attraverso i componenti.

| Membro | Tipo | Descrizione |
|---|---|---|
| `Id` | `int` | Identificatore univoco (init-only) |
| `CurrentComponent` | `ISimComponent` | Componente su cui si trova attualmente |
| `CurrentPort` | `PortId` | Porta dal quale è arrivata nel componente corrente |
| `Progress` | `float` | Avanzamento sul componente corrente: `0.0` = ingresso, `1.0` = uscita |
| `DestinationComponent` | `ISimComponent?` | Destinazione finale (usata per routing futuro) |
| `EntryTime` | `float` | Tempo simulato di ingresso nel sistema (init-only) |

Nessuna implementazione concreta esiste ancora — è il prossimo pezzo da costruire.

---

## Moduli

### IComponentModule

**File:** `Modules/IComponentModule.cs`

Sistema plugin per aggiungere comportamenti a un componente senza sottoclassarlo.

| Metodo | Quando viene chiamato |
|---|---|
| `OnEntityEnter(entity)` | Quando un'entità accetta l'ingresso nel componente (`TryAccept` con successo) |
| `OnEntityExit(entity)` | Quando un'entità viene trasferita al componente successivo |
| `OnTick(deltaTime)` | Ogni tick (*non ancora chiamato — da implementare*) |

---

## Stato attuale e gap noti

| Componente | Stato |
|---|---|
| `SimulationEngine` + tick loop | Implementato |
| `GridManager` + `Cell` + `GridCoord` | Implementato |
| `ISimComponent` + `OneWayConveyor` | Implementato |
| `RoutingGraph` + `Connection` | Implementato |
| `IComponentModule` (interfaccia) | Implementata; `OnTick` non ancora invocato |
| `ISimEntity` (interfaccia) | Definita; nessuna classe concreta |
| Entità in `SimulationState` | Mancante |
| Spawn / despawn entità | Mancante |
| Clock di simulazione (`ElapsedTime`) | Mancante |
| Logica routing con `DestinationComponent` | Mancante |
| `RoutingGraph` centralizzato in `SimulationState` | Mancante |
