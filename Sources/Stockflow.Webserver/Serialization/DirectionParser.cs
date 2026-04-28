using SimDirection = Stockflow.Simulation.Component.Direction;

namespace Stockflow.Webserver.Serialization;

/// <summary>
/// Parsing case-insensitive di direzioni cardinali, condiviso tra
/// <c>SimulationController</c> (ingressi PascalCase dal client) e
/// <c>SessionController</c> (ingressi lowercase dai file scenario §6.1).
/// </summary>
public static class DirectionParser
{
    public static SimDirection Parse(string? s) => (s ?? string.Empty).ToLowerInvariant() switch
    {
        "east"  => SimDirection.East,
        "south" => SimDirection.South,
        "west"  => SimDirection.West,
        _       => SimDirection.North,
    };
}
