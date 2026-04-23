namespace Stockflow.Simulation.Commands;

public sealed record ConfigureComponentCommand(
    int                                ComponentId,
    IReadOnlyDictionary<string, string> Properties
) : ICommand;
