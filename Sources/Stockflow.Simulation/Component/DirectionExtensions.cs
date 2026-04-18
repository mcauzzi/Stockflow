using Stockflow.Simulation.Grid;

namespace Stockflow.Simulation.Component;

public static class DirectionExtensions
{
    public static GridCoord ToOffset(this Direction dir) => dir switch
                                                            {
                                                                Direction.North => new(0, 1),
                                                                Direction.East  => new(1, 0),
                                                                Direction.South => new(0, -1),
                                                                Direction.West  => new(-1, 0),
                                                                _ => throw new ArgumentOutOfRangeException(nameof(dir))
                                                            };

    public static Direction Opposite(this Direction dir) => dir switch
                                                            {
                                                                Direction.North => Direction.South,
                                                                Direction.East  => Direction.West,
                                                                Direction.South => Direction.North,
                                                                Direction.West  => Direction.East,
                                                                _ => throw new ArgumentOutOfRangeException(nameof(dir))
                                                            };

    public static Direction RotateCW(this Direction dir) => dir switch
                                                            {
                                                                Direction.North => Direction.East,
                                                                Direction.East  => Direction.South,
                                                                Direction.South => Direction.West,
                                                                Direction.West  => Direction.North,
                                                                _ => throw new ArgumentOutOfRangeException(nameof(dir))
                                                            };

    public static Direction RotateCCW(this Direction dir) => dir.RotateCW().Opposite();
}