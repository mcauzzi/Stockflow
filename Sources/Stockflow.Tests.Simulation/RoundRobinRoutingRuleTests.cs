using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;
using Stockflow.Tests.Simulation.Helpers;

namespace Stockflow.Tests.Simulation;

public class RoundRobinRoutingRuleTests
{
    private static SimEntity MakeEntity()
    {
        var stub = new StubComponent();
        return new EntityManager().Spawn("A", 1f, 1f, 0f, stub, new PortId(0));
    }

    private static readonly PortId Port1 = new(1);
    private static readonly PortId Port2 = new(2);
    private static readonly IReadOnlyList<PortId> TwoPorts = [Port1, Port2];

    [Fact]
    public void SelectOutput_Initially_ReturnsFirstPort()
    {
        var rule   = new RoundRobinRoutingRule();
        var entity = MakeEntity();

        Assert.Equal(Port1, rule.SelectOutput(entity, TwoPorts));
    }

    [Fact]
    public void SelectOutput_BeforeTransferSucceeded_KeepsReturningFirstPort()
    {
        var rule   = new RoundRobinRoutingRule();
        var entity = MakeEntity();

        rule.SelectOutput(entity, TwoPorts);
        rule.SelectOutput(entity, TwoPorts);

        Assert.Equal(Port1, rule.SelectOutput(entity, TwoPorts));
    }

    [Fact]
    public void SelectOutput_AfterOneTransfer_ReturnsSecondPort()
    {
        var rule   = new RoundRobinRoutingRule();
        var entity = MakeEntity();

        rule.SelectOutput(entity, TwoPorts);
        rule.OnTransferSucceeded(Port1);

        Assert.Equal(Port2, rule.SelectOutput(entity, TwoPorts));
    }

    [Fact]
    public void SelectOutput_AfterTwoTransfers_WrapsToFirstPort()
    {
        var rule   = new RoundRobinRoutingRule();
        var entity = MakeEntity();

        rule.OnTransferSucceeded(Port1);
        rule.OnTransferSucceeded(Port2);

        Assert.Equal(Port1, rule.SelectOutput(entity, TwoPorts));
    }

    [Fact]
    public void SelectOutput_EntityIsIgnored_AlwaysRoundRobins()
    {
        var rule = new RoundRobinRoutingRule();
        var mgr  = new EntityManager();
        var stub = new StubComponent();
        var e1   = mgr.Spawn("SKU-A", 1f, 1f, 0f, stub, new PortId(0));
        var e2   = mgr.Spawn("SKU-B", 2f, 2f, 0f, stub, new PortId(0));

        Assert.Equal(Port1, rule.SelectOutput(e1, TwoPorts));
        rule.OnTransferSucceeded(Port1);
        Assert.Equal(Port2, rule.SelectOutput(e2, TwoPorts));
    }
}
