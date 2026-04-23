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
