namespace Stockflow.Simulation.Commands;

public sealed record LoadScenarioCommand(
    int                     Width,
    int                     Length,
    int                     Floors,
    IReadOnlyList<ICommand> Preplaced
) : ICommand;
