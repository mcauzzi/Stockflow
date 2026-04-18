using Stockflow.Simulation.Component;

namespace Stockflow.Simulation.Grid;

public class Cell
{
    public GridCoord      Coord      { get; }
    public ISimComponent? Component  { get; set; }
    public bool           IsOccupied => Component != null;

    public Cell(GridCoord coord) => Coord = coord;
}

public class GridManager
{
    private readonly Cell[][][] _grid;

    public int Width  { get; }
    public int Length { get; }
    public int Height { get; }

    public GridManager(int width, int length, int height)
    {
        Width  = width;
        Length = length;
        Height = height;

        _grid = new Cell[width][][];
        for (int x = 0; x < width; x++)
        {
            _grid[x] = new Cell[length][];
            for (int y = 0; y < length; y++)
            {
                _grid[x][y] = new Cell[height];
                for (int z = 0; z < height; z++)
                    _grid[x][y][z] = new Cell(new GridCoord(x, y, z));
            }
        }
    }

    public bool IsInBounds(GridCoord coord) =>
        coord.X >= 0 && coord.X < Width  &&
        coord.Y >= 0 && coord.Y < Length &&
        coord.Floor >= 0 && coord.Floor < Height;

    public bool TryGetCell(GridCoord coord, out Cell cell)
    {
        if (!IsInBounds(coord))
        {
            cell = null!;
            return false;
        }
        cell = _grid[coord.X][coord.Y][coord.Floor];
        return true;
    }

    public bool TryPlace(ISimComponent component)
    {
        if (!TryGetCell(component.Position, out var cell) || cell.IsOccupied)
            return false;
        cell.Component = component;
        return true;
    }

    public bool TryRemove(GridCoord coord)
    {
        if (!TryGetCell(coord, out var cell) || !cell.IsOccupied)
            return false;
        cell.Component = null;
        return true;
    }
}