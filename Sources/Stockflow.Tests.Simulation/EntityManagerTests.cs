using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Tests.Simulation.Helpers;

namespace Stockflow.Tests.Simulation;

public class EntityManagerTests
{
    private static (EntityManager mgr, StubComponent comp) Setup()
        => (new EntityManager(), new StubComponent(1));

    [Fact]
    public void Spawn_ReturnsEntityWithCorrectProperties()
    {
        var (mgr, comp) = Setup();

        var entity = mgr.Spawn("SKU-X", 2.5f, 0.5f, 10f, comp, new PortId(0));

        Assert.Equal("SKU-X", entity.Sku);
        Assert.Equal(2.5f, entity.Weight);
        Assert.Equal(0.5f, entity.Size);
        Assert.Equal(10f, entity.EntryTime);
        Assert.Same(comp, entity.CurrentComponent);
    }

    [Fact]
    public void Spawn_AssignsIncrementingIds()
    {
        var (mgr, comp) = Setup();

        var e1 = mgr.Spawn("A", 1f, 1f, 0f, comp, new PortId(0));
        var e2 = mgr.Spawn("B", 1f, 1f, 0f, comp, new PortId(0));

        Assert.NotEqual(e1.Id, e2.Id);
        Assert.Equal(e1.Id + 1, e2.Id);
    }

    [Fact]
    public void Spawn_AddedToActiveCollection()
    {
        var (mgr, comp) = Setup();
        var entity = mgr.Spawn("C", 1f, 1f, 0f, comp, new PortId(0));

        Assert.True(mgr.Active.ContainsKey(entity.Id));
    }

    [Fact]
    public void Despawn_ExistingEntity_RemovesFromActiveAndReturnsTrue()
    {
        var (mgr, comp) = Setup();
        var entity = mgr.Spawn("D", 1f, 1f, 0f, comp, new PortId(0));

        Assert.True(mgr.Despawn(entity.Id));
        Assert.False(mgr.Active.ContainsKey(entity.Id));
    }

    [Fact]
    public void Despawn_UnknownId_ReturnsFalse()
    {
        var (mgr, _) = Setup();
        Assert.False(mgr.Despawn(999));
    }

    [Fact]
    public void Spawn_AfterDespawn_ReusesPooledEntityInstance()
    {
        var (mgr, comp) = Setup();
        var e1 = mgr.Spawn("E", 1f, 1f, 0f, comp, new PortId(0));
        mgr.Despawn(e1.Id);

        var e2 = mgr.Spawn("F", 1f, 1f, 0f, comp, new PortId(0));

        Assert.Same(e1, e2);
        Assert.Equal("F", e2.Sku);
    }

    [Fact]
    public void GetByComponent_ReturnsOnlyEntitiesOnThatComponent()
    {
        var mgr   = new EntityManager();
        var compA = new StubComponent(1);
        var compB = new StubComponent(2);

        var e1 = mgr.Spawn("A1", 1f, 1f, 0f, compA, new PortId(0));
        var e2 = mgr.Spawn("A2", 1f, 1f, 0f, compA, new PortId(0));
        mgr.Spawn("B1", 1f, 1f, 0f, compB, new PortId(0));

        var onA = mgr.GetByComponent(compA.Id).ToList();

        Assert.Equal(2, onA.Count);
        Assert.Contains(e1, onA);
        Assert.Contains(e2, onA);
    }

    [Fact]
    public void GetAll_ReturnsAllActiveEntities()
    {
        var (mgr, comp) = Setup();
        mgr.Spawn("G", 1f, 1f, 0f, comp, new PortId(0));
        mgr.Spawn("H", 1f, 1f, 0f, comp, new PortId(0));

        Assert.Equal(2, mgr.GetAll().Count);
    }
}
