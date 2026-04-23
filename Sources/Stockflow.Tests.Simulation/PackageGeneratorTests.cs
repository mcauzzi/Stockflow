using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;

namespace Stockflow.Tests.Simulation;

public class PackageGeneratorTests
{
    private static PackageGenerator Make(float spawnRate = 1f, bool enabled = true,
                                         int id = 1, EntityManager? mgr = null,
                                         RoutingGraph? graph = null)
        => new(id, new GridCoord(0, 0), Direction.North,
               spawnRate, "PKG", 1f, 1f,
               graph ?? new RoutingGraph(),
               mgr   ?? new EntityManager())
        {
            IsEnabled = enabled,
        };

    [Fact]
    public void TryAccept_AlwaysReturnsFalse()
    {
        var mgr    = new EntityManager();
        var gen    = Make(mgr: mgr);
        var entity = mgr.Spawn("X", 1f, 1f, 0f, gen, new PortId(0));

        Assert.False(gen.TryAccept(entity, new PortId(0)));
    }

    [Fact]
    public void Tick_AfterInterval_SpawnsEntityAsOccupant()
    {
        var gen = Make(spawnRate: 2f); // interval = 0.5 s

        gen.Tick(0.5f);

        Assert.NotNull(gen.Occupant);
    }

    [Fact]
    public void Tick_BeforeInterval_NoOccupant()
    {
        var gen = Make(spawnRate: 1f); // interval = 1 s

        gen.Tick(0.4f);

        Assert.Null(gen.Occupant);
    }

    [Fact]
    public void Tick_Disabled_DoesNotSpawn()
    {
        var gen = Make(spawnRate: 1f, enabled: false);

        gen.Tick(5f);

        Assert.Null(gen.Occupant);
    }

    [Fact]
    public void Tick_SpawnRateZero_DoesNotSpawn()
    {
        var gen = Make(spawnRate: 0f);

        gen.Tick(10f);

        Assert.Null(gen.Occupant);
    }

    [Fact]
    public void Tick_SpawnedEntity_HasCorrectSku()
    {
        var mgr   = new EntityManager();
        var graph = new RoutingGraph();
        var gen   = new PackageGenerator(1, new GridCoord(0, 0), Direction.North,
                                         1f, "BOX-42", 3f, 0.5f, graph, mgr);

        gen.Tick(1f);

        Assert.Equal("BOX-42", gen.Occupant!.Sku);
        Assert.Equal(3f,       gen.Occupant!.Weight);
        Assert.Equal(0.5f,     gen.Occupant!.Size);
    }

    [Fact]
    public void Tick_WithConnectedExit_PushesOccupantDownstream()
    {
        var mgr   = new EntityManager();
        var graph = new RoutingGraph();
        var gen   = new PackageGenerator(1, new GridCoord(0, 0), Direction.North, 1f, "PKG", 1f, 1f, graph, mgr);
        var exit  = new PackageExit(2, new GridCoord(0, 1), Direction.North, mgr);
        graph.Connect(gen, gen.Ports[0].Id, exit, exit.Ports[0].Id);

        gen.Tick(1f); // spawn → Occupant set
        gen.Tick(0f); // push attempt → exit empty → succeeds

        Assert.Null(gen.Occupant);
        Assert.NotNull(exit.Occupant);
    }

    [Fact]
    public void Tick_Backpressure_HoldsOccupantWhenNextIsFull()
    {
        var mgr   = new EntityManager();
        var graph = new RoutingGraph();
        var gen   = new PackageGenerator(1, new GridCoord(0, 0), Direction.North, 1f, "PKG", 1f, 1f, graph, mgr);
        var exit  = new PackageExit(2, new GridCoord(0, 1), Direction.North, mgr);
        graph.Connect(gen, gen.Ports[0].Id, exit, exit.Ports[0].Id);

        // Pre-fill the exit so it refuses further entities
        var blocker = mgr.Spawn("BLOCK", 1f, 1f, 0f, exit, exit.Ports[0].Id);
        exit.TryAccept(blocker, exit.Ports[0].Id);

        gen.Tick(1f); // spawn
        gen.Tick(0f); // push → exit full → stays in gen

        Assert.NotNull(gen.Occupant);
    }

    [Fact]
    public void Tick_AfterPushSucceeds_NextIntervalSpawnsAgain()
    {
        var mgr   = new EntityManager();
        var graph = new RoutingGraph();
        var gen   = new PackageGenerator(1, new GridCoord(0, 0), Direction.North, 1f, "PKG", 1f, 1f, graph, mgr);
        var exit  = new PackageExit(2, new GridCoord(0, 1), Direction.North, mgr);
        graph.Connect(gen, gen.Ports[0].Id, exit, exit.Ports[0].Id);

        gen.Tick(1f); // spawn
        gen.Tick(0f); // push to exit → Occupant = null
        exit.Tick(0f); // exit processes entity → exit.Occupant = null
        gen.Tick(1f); // new interval → spawn again

        Assert.NotNull(gen.Occupant);
    }
}
