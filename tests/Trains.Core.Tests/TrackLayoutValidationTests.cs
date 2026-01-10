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
    public void TrackLayout_GetOutgoingEdges_IncludesTurntableEdges_AndDefaultsAlignment() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var tt = new Turntable(
            id: "T",
            center: new GridPoint(10, 10),
            radius: 1,
            ports: new[] {
                new TurntablePort(new GridPoint(11, 10), Direction.East),
                new TurntablePort(new GridPoint(9, 10), Direction.West),
            },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        var track = TrackLayout.Create(new[] { seg }, new[] { tt });
        var from = new TrackState(new GridPoint(11, 10), Direction.West); // entering port 0

        var edges = track.GetOutgoingEdges(from, new Dictionary<string, int>());
        Assert.Contains(edges, e => e.SegmentId.StartsWith("Turntable:T:", StringComparison.Ordinal));
        Assert.True(track.IsKnownSegment("Turntable:T:0"));
    }

    private enum BadEdgeKind {
        FromEqualsTo,
        EmptySegmentId,
    }

    private sealed class BadEdgeSegment : TrackSegment {
        private readonly BadEdgeKind _kind;

        public BadEdgeSegment(string id, GridPoint a, BadEdgeKind invalidKind) : base(id, a, new GridPoint(a.X + 1, a.Y)) {
            _kind = invalidKind;
        }

        public override IReadOnlyList<DirectedTrackEdge> GetDirectedEdges() {
            switch (_kind) {
                case BadEdgeKind.FromEqualsTo:
                    return new[] { new DirectedTrackEdge("X", this.A, this.A, Direction.East, Direction.East) };
                case BadEdgeKind.EmptySegmentId:
                    return new[] { new DirectedTrackEdge("", this.A, this.B, Direction.East, Direction.East) };
                default:
                    throw new ArgumentOutOfRangeException(nameof(_kind));
            }
        }
    }
}
