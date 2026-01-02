using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class VehiclePlacementTests {
    [Fact]
    public void Constructor_InvalidVehicleId_Throws() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VehiclePlacement(-1, new[] { seg.GetDirectedEdges()[0] }));
    }

    [Fact]
    public void Constructor_NullEdges_Throws() {
        Assert.Throws<ArgumentNullException>(() => new VehiclePlacement(0, null!));
    }

    [Fact]
    public void Constructor_EmptyEdges_Throws() {
        Assert.Throws<ArgumentException>(() => new VehiclePlacement(0, Array.Empty<DirectedTrackEdge>()));
    }

    [Fact]
    public void Constructor_NonContiguousEdges_Throws() {
        var s0 = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)).GetDirectedEdges()[0];
        var s1 = new StraightSegment("S1", new GridPoint(10, 0), new GridPoint(11, 0)).GetDirectedEdges()[0];
        Assert.Throws<ArgumentException>(() => new VehiclePlacement(0, new[] { s0, s1 }));
    }
}
