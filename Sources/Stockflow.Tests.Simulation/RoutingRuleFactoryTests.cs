using Stockflow.Simulation.Routing;

namespace Stockflow.Tests.Simulation;

public class RoutingRuleFactoryTests
{
    [Fact]
    public void Create_RoundRobin_ReturnsRoundRobinRule()
    {
        var rule = RoutingRuleFactory.Create(RoutingRuleFactory.RoundRobin);

        Assert.IsType<RoundRobinRoutingRule>(rule);
    }

    [Fact]
    public void Create_IsCaseInsensitive()
    {
        var rule = RoutingRuleFactory.Create("ROUND_ROBIN");

        Assert.IsType<RoundRobinRoutingRule>(rule);
    }

    [Fact]
    public void Create_UnknownKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => RoutingRuleFactory.Create("by_destination"));
    }

    [Fact]
    public void KeyOf_RoundRobinRule_ReturnsRoundRobin()
    {
        Assert.Equal(RoutingRuleFactory.RoundRobin,
                     RoutingRuleFactory.KeyOf(new RoundRobinRoutingRule()));
    }

    [Fact]
    public void AvailableRules_ContainsAtLeastRoundRobin()
    {
        Assert.Contains(RoutingRuleFactory.RoundRobin, RoutingRuleFactory.AvailableRules);
    }

    [Fact]
    public void AvailableRules_AllRoundTripThroughCreateAndKeyOf()
    {
        foreach (var key in RoutingRuleFactory.AvailableRules)
        {
            var rule = RoutingRuleFactory.Create(key);
            Assert.Equal(key, RoutingRuleFactory.KeyOf(rule));
        }
    }
}
