namespace Stockflow.Simulation.Grid;

public readonly record struct GridCoord(int X, int Y, int Floor = 0)
{
    public static GridCoord operator +(GridCoord a, GridCoord b)
        => new(a.X + b.X, a.Y + b.Y, a.Floor + b.Floor);

    public static readonly GridCoord[] CardinalOffsets =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
    };
}