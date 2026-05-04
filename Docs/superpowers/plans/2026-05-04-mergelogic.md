# MergeLogic Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare il componente `MergeLogic` (2 ingressi, 1 uscita) con logica alternata/prioritaria e configurazione runtime delle porte via REST.

**Architecture:** `MergeLogic` segue il pattern slot-singolo di `OneWayConveyor`/`ConveyorTurn`. La logica merge è gestita da `_activePort` (porta attualmente aperta) con anti-starvation tramite `_stallTicks`. `SetFacing` ricostruisce le porte e viene invocato dal `SimulationEngine` durante `ConfigureComponent` con successivo `AutoConnect`.

**Tech Stack:** .NET 10, C# 13, xUnit, ASP.NET Core Minimal API.

---

## File map

| File | Operazione |
|---|---|
| `Sources/Stockflow.Simulation/Component/MergeMode.cs` | Crea — enum `Alternating`/`Priority` |
| `Sources/Stockflow.Simulation/Component/ComponentType.cs` | Modifica — aggiunge `MergeLogic` |
| `Sources/Stockflow.Simulation/Component/MergeLogic.cs` | Crea — componente principale |
| `Sources/Stockflow.Simulation/Commands/PlaceComponentCommand.cs` | Modifica — aggiunge `PlaceMergeLogicCommand` |
| `Sources/Stockflow.Simulation/Core/SimulationEngine.cs` | Modifica — factory + `ConfigureComponent` branch |
| `Sources/Stockflow.Protocol/Messages/SharedTypes.cs` | Modifica — aggiunge `ComponentKinds.MergeLogic` |
| `Sources/Stockflow.Webserver/Controllers/SimulationController.cs` | Modifica — case `merge` + param `Mode` |
| `Sources/Stockflow.Webserver/Serialization/ComponentSerializer.cs` | Modifica — `KindString` + `BuildProperties` |
| `Sources/Stockflow.Console/src/app/core/mock/sim-mock.ts` | Modifica — `live: true` per `merge` |
| `Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs` | Crea — unit tests |

---

## Task 1: Tipi fondamentali — MergeMode + ComponentType

**Files:**
- Create: `Sources/Stockflow.Simulation/Component/MergeMode.cs`
- Modify: `Sources/Stockflow.Simulation/Component/ComponentType.cs`

- [ ] **Step 1: Crea MergeMode.cs**

```csharp
// Sources/Stockflow.Simulation/Component/MergeMode.cs
namespace Stockflow.Simulation.Component;

public enum MergeMode { Alternating, Priority }
```

- [ ] **Step 2: Aggiungi MergeLogic a ComponentType**

Apri `Sources/Stockflow.Simulation/Component/ComponentType.cs`. Aggiungi `MergeLogic` alla fine dell'enum:

```csharp
namespace Stockflow.Simulation.Component;

public enum ComponentType
{
    OneWayConveyor,
    ConveyorTurn,
    PackageGenerator,
    PackageExit,
    MergeLogic,
}
```

- [ ] **Step 3: Verifica build**

```
dotnet build Sources/Stockflow.Simulation/
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```
git add Sources/Stockflow.Simulation/Component/MergeMode.cs
git add Sources/Stockflow.Simulation/Component/ComponentType.cs
git commit -m "feat(simulation): aggiunge MergeMode enum e ComponentType.MergeLogic"
```

---

## Task 2: MergeLogic — TryAccept e Tick

**Files:**
- Create: `Sources/Stockflow.Simulation/Component/MergeLogic.cs`
- Create: `Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs`

### Ciclo TDD 1: gate di base (slot + porta)

- [ ] **Step 1: Scrivi i primi 3 test (file nuovo)**

```csharp
// Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;

namespace Stockflow.Tests.Simulation;

public class MergeLogicTests
{
    private static MergeLogic MakeMerge(
        MergeMode    mode  = MergeMode.Alternating,
        RoutingGraph? graph = null)
        => new(1, new GridCoord(0, 0), Direction.North, mode, 1f,
               graph ?? new RoutingGraph());

    [Fact]
    public void TryAccept_EmptySlot_In0_Accepts()
    {
        var merge  = MakeMerge();
        var entity = new EntityManager().Spawn("A", 1f, 1f, 0f, merge, new PortId(0));

        Assert.True(merge.TryAccept(entity, new PortId(0)));
        Assert.Same(merge, entity.CurrentComponent);
        Assert.Equal(0f, entity.Progress);
    }

    [Fact]
    public void TryAccept_EmptySlot_In1_Rejected_Initially()
    {
        var merge  = MakeMerge();
        var entity = new EntityManager().Spawn("A", 1f, 1f, 0f, merge, new PortId(1));

        Assert.False(merge.TryAccept(entity, new PortId(1)));
        Assert.Null(merge.Occupant);
    }

    [Fact]
    public void TryAccept_OccupiedSlot_ReturnsFalse()
    {
        var merge = MakeMerge();
        var mgr   = new EntityManager();
        var e1    = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
        var e2    = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(0));
        merge.TryAccept(e1, new PortId(0));

        Assert.False(merge.TryAccept(e2, new PortId(0)));
    }
}
```

- [ ] **Step 2: Verifica che i test non compilino (classe MergeLogic non esiste)**

```
dotnet test Sources/Stockflow.Tests.Simulation/ --no-build 2>&1 | head -5
```
Expected: build error "The type or namespace name 'MergeLogic' could not be found".

- [ ] **Step 3: Crea MergeLogic.cs — scheletro completo**

```csharp
// Sources/Stockflow.Simulation/Component/MergeLogic.cs
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Modules;
using Stockflow.Simulation.Routing;

namespace Stockflow.Simulation.Component;

public class MergeLogic : ISimComponent
{
    public  int                             Id       { get; }
    public  GridCoord                       Position { get; }
    public  Direction                       Facing   { get; private set; }
    public  ComponentType                   Type     => ComponentType.MergeLogic;
    public  IReadOnlyList<IComponentModule> Modules  { get; }
    public  SimEntity?                      Occupant { get; private set; }
    public  IReadOnlyList<Port>             Ports    => _ports;
    public  float                           Speed    { get; set; }
    public  MergeMode                       Mode     { get; set; }
    public  RoutingGraph                    Graph    { get; }

    private Port   _inPort0;
    private Port   _inPort1;
    private Port   _outPort;
    private Port[] _ports = null!;
    private PortId _activePort;
    private int    _stallTicks;
    private const int StallThreshold = 30;

    public MergeLogic(int id, GridCoord position, Direction facing, MergeMode mode, float speed,
                      RoutingGraph graph, IReadOnlyList<IComponentModule>? modules = null)
    {
        Id          = id;
        Position    = position;
        Mode        = mode;
        Speed       = speed;
        Graph       = graph;
        Modules     = modules ?? [];
        _activePort = new PortId(0);
        SetFacing(facing);
    }

    public void SetFacing(Direction facing)
    {
        Facing   = facing;
        _inPort0 = new(new PortId(0), Position + facing.Opposite().ToOffset(),  PortDirection.In);
        _inPort1 = new(new PortId(1), Position + facing.RotateCCW().ToOffset(), PortDirection.In);
        _outPort = new(new PortId(2), Position + facing.ToOffset(),             PortDirection.Out);
        _ports   = [_inPort0, _inPort1, _outPort];
    }

    public void Tick(float deltaTime)
    {
        if (Occupant != null)
        {
            if (Occupant.Progress < 1.0f)
            {
                Occupant.Progress += Speed * deltaTime;
            }
            else
            {
                var next = Graph.GetNext(this, _outPort.Id);
                if (next != null && next.Value.To.TryAccept(Occupant, next.Value.ToPort))
                {
                    foreach (var module in Modules)
                        module.OnEntityExit(Occupant);
                    Occupant = null;
                }
            }
        }
        else
        {
            _stallTicks++;
            if (_stallTicks >= StallThreshold)
            {
                _activePort = _activePort == new PortId(0) ? new PortId(1) : new PortId(0);
                _stallTicks = 0;
            }
        }
    }

    public bool TryAccept(SimEntity entity, PortId fromPort)
    {
        if (Occupant != null) return false;
        if (fromPort != _activePort) return false;

        Occupant                = entity;
        entity.CurrentComponent = this;
        entity.CurrentPort      = fromPort;
        entity.Progress         = 0.0f;
        _stallTicks             = 0;

        if (Mode == MergeMode.Alternating)
            _activePort = _activePort == new PortId(0) ? new PortId(1) : new PortId(0);
        else
            _activePort = new PortId(0);

        foreach (var module in Modules)
            module.OnEntityEnter(entity);

        return true;
    }
}
```

- [ ] **Step 4: Esegui i primi 3 test**

```
dotnet test Sources/Stockflow.Tests.Simulation/ --filter "FullyQualifiedName~MergeLogicTests.TryAccept"
```
Expected: 3 passed.

### Ciclo TDD 2: logica alternata

- [ ] **Step 5: Aggiungi test Alternating a MergeLogicTests.cs**

Aggiungi questi due test alla classe `MergeLogicTests`:

```csharp
[Fact]
public void Alternating_AfterIn0_ActiveSwitchesToIn1()
{
    var graph = new RoutingGraph();
    var merge = MakeMerge(MergeMode.Alternating, graph);
    var mgr   = new EntityManager();
    var exit  = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
    graph.Connect(merge, new PortId(2), exit, new PortId(0));

    var e1 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
    merge.TryAccept(e1, new PortId(0));
    merge.Tick(1f); merge.Tick(0f); exit.Tick(0f); // drain

    var e2 = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(0));
    Assert.False(merge.TryAccept(e2, new PortId(0))); // In0 chiusa
    var e3 = mgr.Spawn("C", 1f, 1f, 0f, merge, new PortId(1));
    Assert.True(merge.TryAccept(e3, new PortId(1)));  // In1 aperta
}

[Fact]
public void Alternating_AfterIn1_ActiveSwitchesToIn0()
{
    var graph = new RoutingGraph();
    var merge = MakeMerge(MergeMode.Alternating, graph);
    var mgr   = new EntityManager();
    var exit  = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
    graph.Connect(merge, new PortId(2), exit, new PortId(0));

    // 1° round: In0 → attiva diventa In1
    var e1 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
    merge.TryAccept(e1, new PortId(0));
    merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

    // 2° round: In1 → attiva diventa In0
    var e2 = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(1));
    merge.TryAccept(e2, new PortId(1));
    merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

    // Ora In0 deve essere attiva di nuovo
    var e3 = mgr.Spawn("C", 1f, 1f, 0f, merge, new PortId(1));
    Assert.False(merge.TryAccept(e3, new PortId(1))); // In1 chiusa
    var e4 = mgr.Spawn("D", 1f, 1f, 0f, merge, new PortId(0));
    Assert.True(merge.TryAccept(e4, new PortId(0)));  // In0 aperta
}
```

- [ ] **Step 6: Esegui i test Alternating**

```
dotnet test Sources/Stockflow.Tests.Simulation/ --filter "FullyQualifiedName~MergeLogicTests.Alternating"
```
Expected: 2 passed. (La logica è già nella classe; se fallisce, ricontrolla la logica `_activePort` in `TryAccept`.)

### Ciclo TDD 3: logica prioritaria

- [ ] **Step 7: Aggiungi test Priority a MergeLogicTests.cs**

```csharp
[Fact]
public void Priority_AfterIn0_ActiveStaysIn0()
{
    var graph = new RoutingGraph();
    var merge = MakeMerge(MergeMode.Priority, graph);
    var mgr   = new EntityManager();
    var exit  = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
    graph.Connect(merge, new PortId(2), exit, new PortId(0));

    var e1 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
    merge.TryAccept(e1, new PortId(0));
    merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

    var e2 = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(1));
    Assert.False(merge.TryAccept(e2, new PortId(1))); // In1 sempre chiusa
    var e3 = mgr.Spawn("C", 1f, 1f, 0f, merge, new PortId(0));
    Assert.True(merge.TryAccept(e3, new PortId(0)));  // In0 rimane attiva
}

[Fact]
public void Priority_StallThreshold_SwitchesToIn1()
{
    var merge = MakeMerge(MergeMode.Priority);
    var mgr   = new EntityManager();

    for (int i = 0; i < 30; i++) merge.Tick(0f);

    var entity = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(1));
    Assert.True(merge.TryAccept(entity, new PortId(1)));
}

[Fact]
public void Priority_AfterIn1Accepts_BackToIn0()
{
    var graph = new RoutingGraph();
    var merge = MakeMerge(MergeMode.Priority, graph);
    var mgr   = new EntityManager();
    var exit  = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
    graph.Connect(merge, new PortId(2), exit, new PortId(0));

    for (int i = 0; i < 30; i++) merge.Tick(0f); // starvation → In1 aperta

    var e1 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(1));
    merge.TryAccept(e1, new PortId(1));
    merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

    // Torna a In0
    var e2 = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(1));
    Assert.False(merge.TryAccept(e2, new PortId(1)));
    var e3 = mgr.Spawn("C", 1f, 1f, 0f, merge, new PortId(0));
    Assert.True(merge.TryAccept(e3, new PortId(0)));
}
```

- [ ] **Step 8: Esegui i test Priority**

```
dotnet test Sources/Stockflow.Tests.Simulation/ --filter "FullyQualifiedName~MergeLogicTests.Priority"
```
Expected: 3 passed.

### Ciclo TDD 4: Tick

- [ ] **Step 9: Aggiungi test Tick a MergeLogicTests.cs**

```csharp
[Fact]
public void Tick_ProgressAdvances()
{
    var merge  = MakeMerge();
    var entity = new EntityManager().Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
    merge.TryAccept(entity, new PortId(0));

    merge.Tick(0.5f);

    Assert.Equal(0.5f, entity.Progress);
}

[Fact]
public void Tick_EntityComplete_TransfersDownstream()
{
    var graph      = new RoutingGraph();
    var merge      = MakeMerge(graph: graph);
    var downstream = new OneWayConveyor(2, new GridCoord(0, -1), Direction.North, 1f, graph);
    graph.Connect(merge, new PortId(2), downstream, new PortId(0));

    var entity = new EntityManager().Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
    merge.TryAccept(entity, new PortId(0));
    merge.Tick(1f);
    merge.Tick(0f);

    Assert.Null(merge.Occupant);
    Assert.Same(downstream, entity.CurrentComponent);
}

[Fact]
public void Tick_NoNext_EntityStays()
{
    var merge  = MakeMerge();
    var entity = new EntityManager().Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
    merge.TryAccept(entity, new PortId(0));
    merge.Tick(1f);
    merge.Tick(0f);

    Assert.Same(entity, merge.Occupant);
}

[Fact]
public void StallTicks_ResetOnAccept()
{
    var graph = new RoutingGraph();
    var merge = MakeMerge(MergeMode.Priority, graph);
    var mgr   = new EntityManager();
    var exit  = new PackageExit(2, new GridCoord(0, -1), Direction.North, mgr);
    graph.Connect(merge, new PortId(2), exit, new PortId(0));

    for (int i = 0; i < 15; i++) merge.Tick(0f); // _stallTicks = 15

    var e1 = mgr.Spawn("A", 1f, 1f, 0f, merge, new PortId(0));
    merge.TryAccept(e1, new PortId(0)); // reset _stallTicks a 0
    merge.Tick(1f); merge.Tick(0f); exit.Tick(0f);

    // 29 tick < soglia 30 — non deve ancora switchare
    for (int i = 0; i < 29; i++) merge.Tick(0f);

    var e2 = mgr.Spawn("B", 1f, 1f, 0f, merge, new PortId(1));
    Assert.False(merge.TryAccept(e2, new PortId(1))); // In0 ancora attiva
}
```

- [ ] **Step 10: Esegui tutti i test Tick**

```
dotnet test Sources/Stockflow.Tests.Simulation/ --filter "FullyQualifiedName~MergeLogicTests.Tick|FullyQualifiedName~MergeLogicTests.StallTicks"
```
Expected: 4 passed.

- [ ] **Step 11: Esegui tutti i test della suite**

```
dotnet test Sources/Stockflow.Tests.Simulation/
```
Expected: tutti i test passano (inclusi i pre-esistenti).

- [ ] **Step 12: Commit**

```
git add Sources/Stockflow.Simulation/Component/MergeLogic.cs
git add Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs
git commit -m "feat(simulation): implementa MergeLogic con logica alternata e prioritaria"
```

---

## Task 3: MergeLogic — test SetFacing

**Files:**
- Modify: `Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs`

- [ ] **Step 1: Aggiungi test SetFacing**

Aggiungi a `MergeLogicTests`:

```csharp
[Fact]
public void SetFacing_RebuildsPortPositions()
{
    // Position (0,0), default Facing = North
    // Dopo SetFacing(East):
    //   In0 (PortId 0): Position + West.ToOffset()  = (-1, 0)
    //   In1 (PortId 1): Position + North.ToOffset() = ( 0,-1)  [East.RotateCCW() = North]
    //   Out (PortId 2): Position + East.ToOffset()  = ( 1, 0)
    var merge = MakeMerge();

    merge.SetFacing(Direction.East);

    Assert.Equal(Direction.East, merge.Facing);
    Assert.Equal(3, merge.Ports.Count);

    var in0 = merge.Ports.First(p => p.Id == new PortId(0));
    var in1 = merge.Ports.First(p => p.Id == new PortId(1));
    var @out = merge.Ports.First(p => p.Id == new PortId(2));

    Assert.Equal(new GridCoord(-1,  0), in0.Position);
    Assert.Equal(new GridCoord( 0, -1), in1.Position);
    Assert.Equal(new GridCoord( 1,  0), @out.Position);
    Assert.Equal(PortDirection.In,  in0.Direction);
    Assert.Equal(PortDirection.In,  in1.Direction);
    Assert.Equal(PortDirection.Out, @out.Direction);
}
```

- [ ] **Step 2: Esegui il test**

```
dotnet test Sources/Stockflow.Tests.Simulation/ --filter "FullyQualifiedName~MergeLogicTests.SetFacing"
```
Expected: 1 passed. (`SetFacing` è già implementata nello scheletro del Task 2.)

- [ ] **Step 3: Commit**

```
git add Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs
git commit -m "test(simulation): aggiunge SetFacing_RebuildsPortPositions per MergeLogic"
```

---

## Task 4: PlaceMergeLogicCommand + SimulationEngine

**Files:**
- Modify: `Sources/Stockflow.Simulation/Commands/PlaceComponentCommand.cs`
- Modify: `Sources/Stockflow.Simulation/Core/SimulationEngine.cs`

- [ ] **Step 1: Aggiungi PlaceMergeLogicCommand a PlaceComponentCommand.cs**

Apri `Sources/Stockflow.Simulation/Commands/PlaceComponentCommand.cs`. Aggiungi alla fine del file (prima della riga `RemoveComponentCommand`):

```csharp
public sealed record PlaceMergeLogicCommand(
    GridCoord Position,
    Direction Facing,
    MergeMode Mode  = MergeMode.Alternating,
    float     Speed = 1f
) : ICommand;
```

Il file finale sarà:

```csharp
using Stockflow.Simulation.Component;
using Stockflow.Simulation.Grid;

namespace Stockflow.Simulation.Commands;

public sealed record PlacePackageGeneratorCommand(
    GridCoord Position,
    Direction Facing,
    float     SpawnRate = 1f,
    string    Sku       = "PKG",
    float     Weight    = 1f,
    float     Size      = 1f
) : ICommand;

public sealed record PlacePackageExitCommand(
    GridCoord Position,
    Direction Facing
) : ICommand;

public sealed record PlaceOneWayConveyorCommand(
    GridCoord Position,
    Direction Facing,
    float     Speed = 1f
) : ICommand;

public sealed record PlaceConveyorTurnCommand(
    GridCoord Position,
    Direction Facing,
    TurnSide  Turn  = TurnSide.Right,
    float     Speed = 1f
) : ICommand;

public sealed record PlaceMergeLogicCommand(
    GridCoord Position,
    Direction Facing,
    MergeMode Mode  = MergeMode.Alternating,
    float     Speed = 1f
) : ICommand;

public sealed record RemoveComponentCommand(int ComponentId) : ICommand;
```

- [ ] **Step 2: Registra factory in SimulationEngine**

Apri `Sources/Stockflow.Simulation/Core/SimulationEngine.cs`. Nel costruttore, aggiungi alla fine del dizionario `_placementFactories` (prima della `}`):

```csharp
[typeof(PlaceMergeLogicCommand)] = (c, id) =>
{
    var x = (PlaceMergeLogicCommand)c;
    return new MergeLogic(id, x.Position, x.Facing, x.Mode, x.Speed, Graph);
},
```

- [ ] **Step 3: Aggiungi branch ConfigureComponent per MergeLogic**

Nel metodo `ConfigureComponent` di `SimulationEngine.cs`, aggiungi prima del return finale:

```csharp
if (component is MergeLogic merge)
{
    if (cmd.Properties.TryGetValue("mode", out var m))
        merge.Mode = m.Equals("priority", StringComparison.OrdinalIgnoreCase)
                         ? MergeMode.Priority
                         : MergeMode.Alternating;
    if (cmd.Properties.TryGetValue("speed", out var sp) && float.TryParse(sp, out var speed) && speed > 0)
        merge.Speed = speed;
    if (cmd.Properties.TryGetValue("facing", out var f) &&
        Enum.TryParse<Direction>(f, ignoreCase: true, out var newFacing))
    {
        Graph.DisconnectAll(merge);
        merge.SetFacing(newFacing);
        AutoConnect(merge);
    }
    return CommandResult.Ok();
}
```

- [ ] **Step 4: Scrivi i test engine**

Aggiungi alla fine di `Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs`:

```csharp
// ── SimulationEngine integration ──────────────────────────────────────────

[Fact]
public void Engine_PlaceMergeLogic_AddsComponent()
{
    var engine = new Stockflow.Simulation.Core.SimulationEngine(10, 10, 1);
    engine.ProcessCommand(new Stockflow.Simulation.Commands.PlaceMergeLogicCommand(
        new GridCoord(5, 5), Direction.North));

    Assert.Single(engine.State.Components);
    Assert.IsType<MergeLogic>(engine.State.Components[0]);
    Assert.Equal(ComponentType.MergeLogic, engine.State.Components[0].Type);
}

[Fact]
public void Engine_ConfigureMerge_Mode_UpdatesMode()
{
    var engine = new Stockflow.Simulation.Core.SimulationEngine(10, 10, 1);
    engine.ProcessCommand(new Stockflow.Simulation.Commands.PlaceMergeLogicCommand(
        new GridCoord(5, 5), Direction.North, MergeMode.Alternating));
    var merge = (MergeLogic)engine.State.Components[0];

    engine.ProcessCommand(new Stockflow.Simulation.Commands.ConfigureComponentCommand(
        merge.Id, new Dictionary<string, string> { ["mode"] = "priority" }));

    Assert.Equal(MergeMode.Priority, merge.Mode);
}

[Fact]
public void Engine_ConfigureMerge_Facing_UpdatesFacing()
{
    var engine = new Stockflow.Simulation.Core.SimulationEngine(10, 10, 1);
    engine.ProcessCommand(new Stockflow.Simulation.Commands.PlaceMergeLogicCommand(
        new GridCoord(5, 5), Direction.North));
    var merge = (MergeLogic)engine.State.Components[0];

    engine.ProcessCommand(new Stockflow.Simulation.Commands.ConfigureComponentCommand(
        merge.Id, new Dictionary<string, string> { ["facing"] = "East" }));

    Assert.Equal(Direction.East, merge.Facing);
}
```

- [ ] **Step 5: Esegui i test engine**

```
dotnet test Sources/Stockflow.Tests.Simulation/ --filter "FullyQualifiedName~MergeLogicTests.Engine"
```
Expected: 3 passed.

- [ ] **Step 6: Esegui l'intera suite**

```
dotnet test
```
Expected: tutti i test passano.

- [ ] **Step 7: Commit**

```
git add Sources/Stockflow.Simulation/Commands/PlaceComponentCommand.cs
git add Sources/Stockflow.Simulation/Core/SimulationEngine.cs
git add Sources/Stockflow.Tests.Simulation/MergeLogicTests.cs
git commit -m "feat(simulation): PlaceMergeLogicCommand + engine factory e ConfigureComponent"
```

---

## Task 5: Webserver layer

**Files:**
- Modify: `Sources/Stockflow.Protocol/Messages/SharedTypes.cs`
- Modify: `Sources/Stockflow.Webserver/Controllers/SimulationController.cs`
- Modify: `Sources/Stockflow.Webserver/Serialization/ComponentSerializer.cs`
- Modify: `Sources/Stockflow.Console/src/app/core/mock/sim-mock.ts`

- [ ] **Step 1: Aggiungi ComponentKinds.MergeLogic in SharedTypes.cs**

Apri `Sources/Stockflow.Protocol/Messages/SharedTypes.cs`. Aggiungi alla classe `ComponentKinds`:

```csharp
public static class ComponentKinds
{
    public const string OneWayConveyor   = "conveyor_oneway";
    public const string ConveyorTurn     = "conveyor_turn";
    public const string PackageGenerator = "package_generator";
    public const string PackageExit      = "package_exit";
    public const string MergeLogic       = "merge";
}
```

- [ ] **Step 2: Aggiungi Mode a PlaceComponentRequest e case merge in SimulationController.cs**

Apri `Sources/Stockflow.Webserver/Controllers/SimulationController.cs`.

**2a.** Modifica il record `PlaceComponentRequest` aggiungendo `string? Mode = null`:

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
    string? Mode      = null);
```

**2b.** Nel metodo `PlaceComponent`, aggiungi il case `merge` nello switch (dopo `ComponentKinds.ConveyorTurn`):

```csharp
ComponentKinds.MergeLogic => new PlaceMergeLogicCommand(
    pos,
    dir,
    req.Mode?.Equals("priority", StringComparison.OrdinalIgnoreCase) == true
        ? MergeMode.Priority
        : MergeMode.Alternating,
    req.Speed ?? 1f),
```

Aggiungi anche il using necessario in cima al file se non presente:
```csharp
using Stockflow.Simulation.Component;
```
(È già presente perché usa `TurnSide`.)

- [ ] **Step 3: Aggiorna ComponentSerializer**

Apri `Sources/Stockflow.Webserver/Serialization/ComponentSerializer.cs`.

**3a.** In `KindString`, aggiungi prima del catch-all `_`:

```csharp
SimComponentType.MergeLogic => ComponentKinds.MergeLogic,
```

**3b.** In `BuildProperties`, aggiungi prima del catch-all `_ => null`:

```csharp
MergeLogic m => new()
{
    ["mode"]  = m.Mode == MergeMode.Priority ? "priority" : "alternating",
    ["speed"] = m.Speed.ToString("F3"),
},
```

Aggiungi il using in cima se non presente:
```csharp
using Stockflow.Simulation.Commands;
```
(Non serve — MergeLogic e MergeMode sono in `Stockflow.Simulation.Component`, già importato.)

- [ ] **Step 4: Attiva merge nella libreria componenti Angular**

Apri `Sources/Stockflow.Console/src/app/core/mock/sim-mock.ts`. Cambia `live: false` in `live: true` per la voce `merge`:

```typescript
{ id: 'merge',  name: 'Merge',       sym: '┳', cost: 300,  hotkey: '3', kind: 'merge',           live: true  },
```

- [ ] **Step 5: Build completo**

```
dotnet build Stockflow.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Esegui l'intera test suite**

```
dotnet test
```
Expected: tutti i test passano.

- [ ] **Step 7: Commit**

```
git add Sources/Stockflow.Protocol/Messages/SharedTypes.cs
git add Sources/Stockflow.Webserver/Controllers/SimulationController.cs
git add Sources/Stockflow.Webserver/Serialization/ComponentSerializer.cs
git add Sources/Stockflow.Console/src/app/core/mock/sim-mock.ts
git commit -m "feat(webserver): espone MergeLogic via REST + abilita nel frontend"
```

---

## Self-review checklist (già eseguita)

| Requisito spec | Task che lo copre |
|---|---|
| 2 ingressi, 1 uscita (1×1) | Task 2 — porte In0/In1/Out |
| Logica alternata | Task 2 — ciclo TDD 2 |
| Logica prioritaria | Task 2 — ciclo TDD 3 |
| Gestione conflitti (slot singolo) | Task 2 — `TryAccept_OccupiedSlot_ReturnsFalse` |
| Buffer minimo (slot singolo) | Task 2 — `Occupant: SimEntity?` |
| Anti-starvation | Task 2 — ciclo TDD 3 (`Priority_StallThreshold_SwitchesToIn1`) |
| SetFacing ricostruisce porte | Task 3 — `SetFacing_RebuildsPortPositions` |
| PlaceMergeLogicCommand | Task 4 — Step 1 |
| SimulationEngine factory | Task 4 — Step 2 |
| ConfigureComponent (mode/speed/facing) | Task 4 — Step 3 + test Engine_Configure* |
| ComponentKinds.MergeLogic | Task 5 — Step 1 |
| REST PlaceComponent case merge | Task 5 — Step 2 |
| ComponentSerializer | Task 5 — Step 3 |
| Frontend live: true | Task 5 — Step 4 |
