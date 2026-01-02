using Trains.Geometry;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class TrackLayoutValidationTests {
    [Fact]
    public void TrackLayout_InvalidEdge_FromEqualsTo_Throws() {
        Assert.Throws<ArgumentException>(() => TrackLayout.Create(new TrackSegment[] {
            new BadEdgeSegment("B0", new GridPoint(0,0), invalidKind: BadEdgeKind.FromEqualsTo),
        }));
    }

    [Fact]
    public void TrackLayout_InvalidEdge_EmptySegmentId_Throws() {
        Assert.Throws<ArgumentException>(() => TrackLayout.Create(new TrackSegment[] {
            new BadEdgeSegment("B0", new GridPoint(0,0), invalidKind: BadEdgeKind.EmptySegmentId),
        }));
    }

    [Fact]
    public void TrackLayout_InvalidEdge_NegativeDistance_Throws() {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackLayout.Create(new TrackSegment[] {
            new BadEdgeSegment("B0", new GridPoint(0,0), invalidKind: BadEdgeKind.NegativeDistance),
        }));
    }

    [Fact]
    public void TrackLayout_GetOutgoingEdges_IncludesTurntableEdges_AndDefaultsAlignment() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var tt = new Turntable(
            id: "T",
            center: new GridPoint(10, 10),
            radius: 1,
            ports: new[] {
                new TurntablePort(new GridPoint(11, 10), Direction.East),
                new TurntablePort(new GridPoint(10, 11), Direction.North),
            },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        var track = TrackLayout.Create(new[] { seg }, new[] { tt });
        var from = new TrackState(new GridPoint(11, 10), Direction.West); // entering port 0

        var edges = track.GetOutgoingEdges(from, new Dictionary<string, int>());
        Assert.Contains(edges, e => e.SegmentId == "Turntable:T");
        Assert.True(track.IsKnownSegment("Turntable:T"));
    }

    private enum BadEdgeKind {
        FromEqualsTo,
        EmptySegmentId,
        NegativeDistance,
    }

    private sealed class BadEdgeSegment : TrackSegment {
        private readonly BadEdgeKind _kind;

        public BadEdgeSegment(string id, GridPoint a, BadEdgeKind invalidKind) : base(id, a, new GridPoint(a.X + 1, a.Y), distance: 1) {
            _kind = invalidKind;
        }

        public override IReadOnlyList<DirectedTrackEdge> GetDirectedEdges() {
            switch (_kind) {
                case BadEdgeKind.FromEqualsTo:
                    return new[] { new DirectedTrackEdge("X", this.A, this.A, Direction.East, Direction.East, 1) };
                case BadEdgeKind.EmptySegmentId:
                    return new[] { new DirectedTrackEdge("", this.A, this.B, Direction.East, Direction.East, 1) };
                case BadEdgeKind.NegativeDistance:
                    return new[] { new DirectedTrackEdge("X", this.A, this.B, Direction.East, Direction.East, -1) };
                default:
                    throw new ArgumentOutOfRangeException(nameof(_kind));
            }
        }
    }
}
