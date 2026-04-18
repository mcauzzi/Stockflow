namespace Stockflow.Simulation.Commands;

public readonly record struct CommandResult(bool Success, string? ErrorMessage = null)
{
    public static CommandResult Ok()             => new(true);
    public static CommandResult Fail(string why) => new(false, why);
}
