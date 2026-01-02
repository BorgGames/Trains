using Trains.Geometry;

namespace Trains.Core.Tests;

public sealed class GeometryTests {
    [Fact]
    public void Direction_Opposite_CoversAll() {
        Assert.Equal(Direction.South, Direction.North.Opposite());
        Assert.Equal(Direction.West, Direction.East.Opposite());
        Assert.Equal(Direction.North, Direction.South.Opposite());
        Assert.Equal(Direction.East, Direction.West.Opposite());
    }

    [Fact]
    public void Direction_ToOffset_And_FromOffset_RoundTrips() {
        foreach (var d in new[] { Direction.North, Direction.East, Direction.South, Direction.West }) {
            var (dx, dy) = d.ToOffset();
            Assert.Equal(d, DirectionExtensions.FromOffset(dx, dy));
        }
    }

    [Fact]
    public void Direction_FromOffset_Invalid_Throws() {
        Assert.Throws<ArgumentOutOfRangeException>(() => DirectionExtensions.FromOffset(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DirectionExtensions.FromOffset(1, 1));
    }

    [Fact]
    public void GridPoint_Offset_Works() {
        var p = new GridPoint(5, 7);
        Assert.Equal(new GridPoint(5, 8), p.Offset(Direction.North));
        Assert.Equal(new GridPoint(6, 7), p.Offset(Direction.East));
        Assert.Equal(new GridPoint(5, 6), p.Offset(Direction.South));
        Assert.Equal(new GridPoint(4, 7), p.Offset(Direction.West));
    }
}
