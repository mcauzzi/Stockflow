using Stockflow.Simulation.Grid;
using Stockflow.Tests.Simulation.Helpers;

namespace Stockflow.Tests.Simulation;

public class GridManagerTests
{
    private static GridManager MakeGrid(int w = 5, int l = 5, int h = 1) => new(w, l, h);

    [Fact]
    public void TryPlace_ValidEmptyCell_ReturnsTrue()
    {
        var grid = MakeGrid();
        var comp = new StubComponent(1);
        Assert.True(grid.TryPlace(comp));
    }

    [Fact]
    public void TryPlace_OccupiedCell_ReturnsFalse()
    {
        var grid  = MakeGrid();
        var comp1 = new StubComponent(1, new GridCoord(1, 1));
        var comp2 = new StubComponent(2, new GridCoord(1, 1));

        grid.TryPlace(comp1);
        Assert.False(grid.TryPlace(comp2));
    }

    [Fact]
    public void TryPlace_OutOfBounds_ReturnsFalse()
    {
        var grid = MakeGrid(3, 3, 1);
        var comp = new StubComponent(99, new GridCoord(100, 100));
        Assert.False(grid.TryPlace(comp));
    }

    [Fact]
    public void TryRemove_OccupiedCell_ClearsComponentAndReturnsTrue()
    {
        var grid = MakeGrid();
        var comp = new StubComponent(1, new GridCoord(2, 2));
        grid.TryPlace(comp);

        Assert.True(grid.TryRemove(comp.Position));
        Assert.True(grid.TryGetCell(comp.Position, out var cell));
        Assert.False(cell.IsOccupied);
    }

    [Fact]
    public void TryRemove_EmptyCell_ReturnsFalse()
    {
        var grid = MakeGrid();
        Assert.False(grid.TryRemove(new GridCoord(2, 2)));
    }

    [Fact]
    public void TryGetCell_ValidCoord_ReturnsCellWithCorrectCoord()
    {
        var grid  = MakeGrid();
        var coord = new GridCoord(1, 2);

        Assert.True(grid.TryGetCell(coord, out var cell));
        Assert.Equal(coord, cell.Coord);
    }

    [Fact]
    public void TryGetCell_OutOfBounds_ReturnsFalse()
    {
        var grid = MakeGrid(3, 3, 1);
        Assert.False(grid.TryGetCell(new GridCoord(10, 10), out _));
    }

    [Fact]
    public void IsInBounds_EdgeCells_ReturnsTrue()
    {
        var grid = MakeGrid(4, 4, 2);
        Assert.True(grid.IsInBounds(new GridCoord(0, 0, 0)));
        Assert.True(grid.IsInBounds(new GridCoord(3, 3, 1)));
    }

    [Fact]
    public void IsInBounds_NegativeOrExceedingCoord_ReturnsFalse()
    {
        var grid = MakeGrid();
        Assert.False(grid.IsInBounds(new GridCoord(-1, 0)));
        Assert.False(grid.IsInBounds(new GridCoord(0, -1)));
        Assert.False(grid.IsInBounds(new GridCoord(5, 0)));
    }

    [Fact]
    public void AdjacentCells_ViaCardinalOffsets_AreAllReachable()
    {
        var grid   = MakeGrid(5, 5, 1);
        var center = new GridCoord(2, 2);

        var allReachable = GridCoord.CardinalOffsets
            .Select(o => center + o)
            .All(c => grid.TryGetCell(c, out _));

        Assert.True(allReachable);
    }
}
