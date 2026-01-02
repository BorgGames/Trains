using Trains.Geometry;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class TrackTests {
    [Fact]
    public void StraightSegment_InvalidEndpoints_Throws() {
        Assert.Throws<ArgumentException>(() => new StraightSegment("S", new GridPoint(0, 0), new GridPoint(0, 2)));
        Assert.Throws<ArgumentException>(() => new StraightSegment("S", new GridPoint(0, 0), new GridPoint(1, 1)));
    }

    [Fact]
    public void CurvedSegment_Bias_DisambiguatesTurn() {
        var a = new GridPoint(0, 0);
        var b = new GridPoint(1, 1);

        var xFirst = new CurvedSegment("C1", a, b, CurveBias.XFirst).GetDirectedEdges()[0];
        Assert.Equal(Direction.East, xFirst.EntryHeading);
        Assert.Equal(Direction.North, xFirst.ExitHeading);

        var yFirst = new CurvedSegment("C2", a, b, CurveBias.YFirst).GetDirectedEdges()[0];
        Assert.Equal(Direction.North, yFirst.EntryHeading);
        Assert.Equal(Direction.East, yFirst.ExitHeading);
    }

    [Fact]
    public void TrackLayout_DuplicateSegmentId_Throws() {
        var segments = new TrackSegment[] {
            new StraightSegment("S", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S", new GridPoint(1, 0), new GridPoint(2, 0)),
        };

        Assert.Throws<ArgumentException>(() => TrackLayout.Create(segments));
    }

    [Fact]
    public void Turntable_ValidatesPortsAndAlignments() {
        var tt = new Turntable(
            id: "T",
            center: new GridPoint(0, 0),
            radius: 1,
            ports: new[] {
                new TurntablePort(new GridPoint(1, 0), Direction.East),
                new TurntablePort(new GridPoint(0, 1), Direction.North),
            },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        var edges = tt.GetDirectedEdgesForAlignment(0).ToArray();
        Assert.Equal(2, edges.Length);
        Assert.All(edges, e => Assert.Equal(0, e.Distance));
    }

    [Fact]
    public void Turntable_InvalidPort_Throws() {
        Assert.Throws<ArgumentException>(() =>
            new Turntable(
                id: "T",
                center: new GridPoint(0, 0),
                radius: 1,
                ports: new[] { new TurntablePort(new GridPoint(0, 0), Direction.North), new TurntablePort(new GridPoint(1, 0), Direction.East) },
                alignments: new[] { new TurntableAlignment(0, 1) }
            )
        );
    }

    [Fact]
    public void Turntable_InvalidAlignment_Throws() {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Turntable(
                id: "T",
                center: new GridPoint(0, 0),
                radius: 1,
                ports: new[] { new TurntablePort(new GridPoint(1, 0), Direction.East), new TurntablePort(new GridPoint(0, 1), Direction.North) },
                alignments: new[] { new TurntableAlignment(0, 9) }
            )
        );
    }

    [Fact]
    public void Turntable_ContainsStrictly_Works() {
        var tt = new Turntable(
            id: "T",
            center: new GridPoint(0, 0),
            radius: 2,
            ports: new[] {
                new TurntablePort(new GridPoint(2, 0), Direction.East),
                new TurntablePort(new GridPoint(0, 2), Direction.North),
            },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        Assert.True(tt.ContainsStrictly(new GridPoint(1, 0)));
        Assert.False(tt.ContainsStrictly(new GridPoint(2, 0))); // border
        Assert.False(tt.ContainsStrictly(new GridPoint(3, 0))); // outside
    }
}
