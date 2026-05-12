namespace Stockflow.Simulation.Routing;

/// <summary>
/// Maps the wire/string form of a routing strategy (used in <c>ComponentState.Properties</c>
/// and in <c>PropertySchema.EnumValues</c>) to its concrete <see cref="IRoutingRule"/>
/// implementation. New strategies must register both directions here.
/// </summary>
public static class RoutingRuleFactory
{
    public const string RoundRobin = "round_robin";

    public static readonly string[] AvailableRules = [RoundRobin];

    public static IRoutingRule Create(string key) => key.ToLowerInvariant() switch
    {
        RoundRobin => new RoundRobinRoutingRule(),
        _          => throw new ArgumentException($"Unknown routing rule '{key}'", nameof(key)),
    };

    public static string KeyOf(IRoutingRule rule) => rule switch
    {
        RoundRobinRoutingRule => RoundRobin,
        _                     => throw new ArgumentException(
                                     $"Routing rule type '{rule.GetType().Name}' is not registered in the factory"),
    };
}
