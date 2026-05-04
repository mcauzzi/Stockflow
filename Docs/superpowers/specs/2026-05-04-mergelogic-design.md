# MergeLogic — Design Spec
**Issue:** #23  
**Milestone:** F1A — Core Gameplay  
**Data:** 2026-05-04

---

## 1. Requisiti (da issue #23)

- Componente 1×1 con 2 ingressi e 1 uscita
- Logica configurabile: **alternata** o **prioritaria**
- Gestione conflitti quando entrambi gli ingressi hanno un pacco disponibile
- Buffer minimo interno: **slot singolo** (stesso modello di `OneWayConveyor`)

---

## 2. File da creare/modificare

| File | Operazione |
|---|---|
| `Sources/Stockflow.Simulation/Component/MergeLogic.cs` | Nuovo |
| `Sources/Stockflow.Simulation/Component/MergeMode.cs` | Nuovo |
| `Sources/Stockflow.Simulation/Component/ComponentType.cs` | Aggiunta voce `MergeLogic` |
| `Sources/Stockflow.Simulation/Commands/PlaceComponentCommand.cs` | Aggiunta `PlaceMergeLogicCommand` |
| `Sources/Stockflow.Simulation/Core/SimulationEngine.cs` | Factory + branch `ConfigureComponent` |
| `Sources/Stockflow.Protocol/Messages/SharedTypes.cs` | Aggiunta `ComponentKinds.MergeLogic` |
| `Sources/Stockflow.Webserver/Controllers/SimulationController.cs` | Case `merge` in `PlaceComponent` + `Mode` in request |
| `Sources/Stockflow.Webserver/Serialization/ComponentSerializer.cs` | `KindString` + `BuildProperties` per MergeLogic |
| `Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs` | Nuovo |

---

## 3. Struttura dati

### 3.1 MergeMode

```csharp
public enum MergeMode { Alternating, Priority }
```

### 3.2 Porte (default Facing = North)

| PortId | PortDirection | Posizione relativa | Ruolo |
|---|---|---|---|
| 0 | In | `Position + Facing.Opposite().ToOffset()` | Ingresso primario |
| 1 | In | `Position + Facing.RotateCCW().ToOffset()` | Ingresso secondario |
| 2 | Out | `Position + Facing.ToOffset()` | Uscita |

L'ingresso primario (PortId 0) è il lato opposto al Facing, coerente con gli altri conveyor.  
L'ingresso secondario (PortId 1) arriva dal lato sinistro rispetto al Facing.

### 3.3 Campi interni

```
SimEntity?  Occupant        — entità in transito (progress 0→1)
float       Speed           — velocità di transito
MergeMode   Mode            — Alternating | Priority
PortId      _activePort     — porta di ingresso attualmente aperta
int         _stallTicks     — tick consecutivi con slot vuoto
const int   StallThreshold  — soglia anti-starvation (costante privata = 30)
```

`_activePort` inizializzato a `PortId(0)` (ingresso primario).

---

## 4. Comportamento

### 4.1 TryAccept(entity, fromPort)

1. Se `Occupant != null` → restituisce `false` (slot occupato)
2. Se `fromPort != _activePort` → restituisce `false` (porta chiusa dalla logica merge)
3. Accetta l'entità: `Occupant = entity`, `Progress = 0`, `_stallTicks = 0`
4. Aggiorna `_activePort`:
   - **Alternating**: passa all'altra porta (`In0 ↔ In1`)
   - **Priority**: se `_activePort` era `In1` (aperto per starvation), torna a `In0`; altrimenti rimane su `In0`

### 4.2 Tick(deltaTime)

```
if Occupant != null:
    if Progress < 1.0:
        Progress += Speed * deltaTime
    else:
        next = Graph.GetNext(this, OutPort.Id)
        if next != null && next.TryAccept(Occupant, next.ToPort):
            fire OnEntityExit modules
            Occupant = null
else:
    _stallTicks++
    if _stallTicks >= StallThreshold:
        switch _activePort (In0 ↔ In1)
        _stallTicks = 0
```

### 4.3 Conflict resolution

Se due upstream conveyors chiamano `TryAccept` nello stesso tick, il primo vincerà (lo slot si riempie), il secondo riceverà `false` e rimarrà bloccato sul proprio conveyor. Gestione implicita dal modello a slot singolo, nessuna logica extra necessaria.

---

## 5. Test plan (simulazione)

| Test | Comportamento verificato |
|---|---|
| `TryAccept_EmptySlot_In0_Accepts` | Porta attiva di default è In0 |
| `TryAccept_EmptySlot_In1_Rejected_Initially` | In1 rifiutata quando `_activePort = In0` |
| `TryAccept_OccupiedSlot_ReturnsFalse` | Slot pieno → sempre false indipendentemente dalla porta |
| `Alternating_AfterIn0_ActiveSwitchesToIn1` | Dopo accettare da In0, In1 diventa attiva |
| `Alternating_AfterIn1_ActiveSwitchesToIn0` | Round-robin completo |
| `Priority_AfterIn0_ActiveStaysIn0` | In0 rimane sempre prioritaria dopo accettazione |
| `Priority_StallThreshold_SwitchesToIn1` | Dopo `StallThreshold` tick vuoti, In1 si apre |
| `Priority_AfterIn1Accepts_BackToIn0` | Dopo accettare da In1, `_activePort` torna a In0 |
| `Tick_ProgressAdvances` | `Progress += Speed * deltaTime` |
| `Tick_EntityComplete_TransfersDownstream` | Trasferimento corretto al componente a valle |
| `Tick_NoNext_EntityStays` | Nessun downstream → entità rimane (jam) |
| `StallTicks_ResetOnAccept` | `_stallTicks` azzerato ad ogni accettazione |

| `SetFacing_RebuildsPortsAndReconnects` | `SetFacing` aggiorna posizioni porte e riconnette |

---

## 6. Layer command (Simulation)

### 6.1 PlaceMergeLogicCommand

```csharp
public sealed record PlaceMergeLogicCommand(
    GridCoord  Position,
    Direction  Facing,
    MergeMode  Mode  = MergeMode.Alternating,
    float      Speed = 1f
) : ICommand;
```

Registrata nella factory di `SimulationEngine` esattamente come gli altri `PlaceXxxCommand`.

### 6.2 ConfigureComponent — branch MergeLogic

Aggiunto a `SimulationEngine.ConfigureComponent`:

```
if component is MergeLogic merge:
    "mode"   → parse "alternating"|"priority" → merge.Mode
    "speed"  → parse float > 0 → merge.Speed
    "facing" → parse Direction →
                  Graph.DisconnectAll(merge)
                  merge.SetFacing(newDir)
                  AutoConnect(merge)
    return Ok()
```

### 6.3 MergeLogic.SetFacing(Direction)

Metodo pubblico su `MergeLogic` che ricostruisce le tre porte (`_inPort0`, `_inPort1`, `_outPort`) e aggiorna la proprietà `Facing`. Necessario perché le posizioni delle porte derivano da `Facing` e sono calcolate nel costruttore; il cambio a runtime richiede una ricostruzione esplicita.

Dopo la chiamata, `SimulationEngine` invoca `AutoConnect(merge)` per ristabilire le connessioni con i nuovi vicini.

---

## 7. Layer controller (Webserver)

### 7.1 ComponentKinds

```csharp
// SharedTypes.cs
public const string MergeLogic = "merge";
```

Il valore `"merge"` è già usato dal frontend Angular (`sim-mock.ts`) — wire-stable.

### 7.2 PlaceComponentRequest

Aggiunto parametro opzionale:

```csharp
string? Mode = null   // "alternating" | "priority"
```

### 7.3 SimulationController — case merge

```csharp
ComponentKinds.MergeLogic => new PlaceMergeLogicCommand(
    pos,
    dir,
    req.Mode == "priority" ? MergeMode.Priority : MergeMode.Alternating,
    req.Speed ?? 1f),
```

### 7.4 ComponentSerializer

**KindString:**
```csharp
SimComponentType.MergeLogic => ComponentKinds.MergeLogic,
```

**BuildProperties:**
```csharp
MergeLogic m => new()
{
    ["mode"]  = m.Mode == MergeMode.Priority ? "priority" : "alternating",
    ["speed"] = m.Speed.ToString("F3"),
},
```

`Facing` non va in `Properties` — è già esposto come campo top-level di `ComponentState`.

### 7.5 Flusso configurazione runtime (frontend → simulazione)

```
PUT /api/sim/components/{id}
    body: { "mode": "priority" }          → cambia logica merge
    body: { "speed": "2.0" }              → cambia velocità
    body: { "facing": "East" }            → ruota uscita, riconnette porte
```

Tutti e tre i casi passano per l'esistente `ConfigureComponentCommand` — nessun nuovo endpoint REST necessario.

---

## 9. Vincoli architetturali

- `Stockflow.Simulation` deve rimanere **zero dipendenze NuGet** — nessuna dipendenza esterna introdotta
- `MergeLogic` segue esattamente lo stesso pattern di `OneWayConveyor` e `ConveyorTurn`
- Il campo `Occupant` esposto da `ISimComponent` rappresenta l'entità in transito (progress 0→1), non un buffer
- `SetFacing` è chiamato **solo** dal `SimulationEngine` (dentro il tick lock, via `ConfigureComponent`), mai direttamente da codice client
