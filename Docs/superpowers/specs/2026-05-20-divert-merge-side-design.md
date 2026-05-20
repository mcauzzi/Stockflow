# DiverterLogic + MergeLogic — Lateral Side Config

**Data:** 2026-05-20  
**Branch:** `claude/divert-merge-side`

---

## 1. Obiettivo

Permettere di scegliere al momento del piazzamento se la porta laterale di `DiverterLogic` e `MergeLogic` si affaccia a sinistra o a destra rispetto alla direzione `Facing`. Attualmente entrambe le porte laterali sono hardcoded (diverter → destra, merge → sinistra).

---

## 2. Scelte di design

- **Tipo:** si riusa `TurnSide { Left, Right }` già esistente in `Component/TurnSide.cs`. Il concetto è identico per tutti e tre i componenti che hanno una porta laterale (`ConveyorTurn`, `DiverterLogic`, `MergeLogic`).
- **Solo piazzamento:** `Side` non è modificabile a runtime. È esposta come proprietà read-only nel `ConfigSchema` (solo per serializzazione/ispezione), non tramite `ApplyConfig`.
- **Default DiverterLogic:** `TurnSide.Right` — invariante rispetto al comportamento attuale.
- **Default MergeLogic:** `TurnSide.Left` — invariante rispetto al comportamento attuale.

---

## 3. Modifiche per componente

### 3.1 DiverterLogic

**Costruttore:** aggiunto parametro `TurnSide side = TurnSide.Right`.

**Porta laterale:**
```csharp
_outPort1 = new(_portOut1,
    Position + (side == TurnSide.Right ? facing.RotateCW() : facing.RotateCCW()).ToOffset(),
    PortDirection.Out);
```

**Proprietà pubblica:** `public TurnSide Side { get; }` — inizializzata dal costruttore.

**ConfigSchema:** aggiunto entry read-only:
```csharp
new("side", "Lateral Side", PropertyType.Enum,
    DefaultValue: "right", EnumValues: ["left", "right"], IsReadOnly: true),
```

**ExportProperties:** aggiunto `["side"] = Side == TurnSide.Right ? "right" : "left"`.

### 3.2 MergeLogic

**Costruttore:** aggiunto parametro `TurnSide side = TurnSide.Left`.

**Porta ingresso secondaria (PortId 1):**
```csharp
_inPort1 = new(_portIn1,
    Position + (side == TurnSide.Left ? facing.RotateCCW() : facing.RotateCW()).ToOffset(),
    PortDirection.In);
```

**Proprietà pubblica:** `public TurnSide Side { get; }` — inizializzata dal costruttore.

**ConfigSchema:** stesso entry read-only con `DefaultValue: "left"`.

**ExportProperties:** aggiunto `["side"] = Side == TurnSide.Left ? "left" : "right"`.

**SetFacing(Direction):** il metodo esistente ricostruisce le porte — deve usare `this.Side` invece di `facing.RotateCCW()` hardcoded per la porta ingresso secondario.

---

## 4. Layer Command (Simulation)

### PlaceDiverterLogicCommand
```csharp
public sealed record PlaceDiverterLogicCommand(
    GridCoord Position,
    Direction Facing,
    TurnSide  Side  = TurnSide.Right,
    float     Speed = 1f
) : ICommand;
```

### PlaceMergeLogicCommand
```csharp
public sealed record PlaceMergeLogicCommand(
    GridCoord Position,
    Direction Facing,
    MergeMode Mode  = MergeMode.Alternating,
    TurnSide  Side  = TurnSide.Left,
    float     Speed = 1f
) : ICommand;
```

`SimulationEngine` factory legge `Side` e lo passa al costruttore del componente.

---

## 5. Layer Webserver

### PlaceComponentRequest

Aggiunto campo opzionale già presente per `Turn` su `ConveyorTurn` — stesso pattern:
```csharp
string? Side = null   // "left" | "right"
```

### SimulationController — helper di parsing
```csharp
static TurnSide ParseSide(string? s, TurnSide defaultVal) =>
    s?.Equals("left", StringComparison.OrdinalIgnoreCase) == true
        ? TurnSide.Left : defaultVal;
```

- Case `diverter`: `Side = ParseSide(req.Side, TurnSide.Right)`
- Case `merge`: `Side = ParseSide(req.Side, TurnSide.Left)`

### ComponentSerializer — BuildProperties

**DiverterLogic:**
```csharp
["side"]  = d.Side == TurnSide.Right ? "right" : "left",
```

**MergeLogic:**
```csharp
["side"]  = m.Side == TurnSide.Left ? "left" : "right",
```

---

## 6. Test

| Test | Componente | Descrizione |
|---|---|---|
| `Ports_FacingNorth_SideLeft_CorrectPositions` | `DiverterLogicTests` | `_portOut1` → West `(-1,0)` con side=Left |
| `Ports_FacingNorth_SideRight_CorrectPositions` | `DiverterLogicTests` | Rinomina test esistente (era implicito Right) |
| `Ports_FacingNorth_SideRight_CorrectPositions` | `MergeLogicTests` | Port 1 (InPort secondario) → East `(1,0)` con side=Right |
| `Ports_FacingNorth_SideLeft_CorrectPositions` | `MergeLogicTests` | Rinomina/verifica test esistente (era implicito Left) |

---

## 7. File da modificare

| File | Operazione |
|---|---|
| `Sources/Stockflow.Simulation/Component/DiverterLogic.cs` | Aggiunge `Side`, `TurnSide` param, porta laterale dinamica, schema/export |
| `Sources/Stockflow.Simulation/Component/MergeLogic.cs` | Aggiunge `Side`, `TurnSide` param, porta laterale dinamica, schema/export |
| `Sources/Stockflow.Simulation/Commands/PlaceComponentCommand.cs` | Aggiunge `Side` a entrambi i command record |
| `Sources/Stockflow.Simulation/Core/SimulationEngine.cs` | Factory: passa `Side` al costruttore |
| `Sources/Stockflow.Webserver/Controllers/SimulationController.cs` | Parsing `req.Side` per diverter e merge |
| `Sources/Stockflow.Webserver/Serialization/ComponentSerializer.cs` | Espone `"side"` in `BuildProperties` |
| `Sources/Stockflow.Tests.Simulation/DiverterLogicTests.cs` | Aggiunge/rinomina test porte |
| `Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs` | Aggiunge/rinomina test porte |
