using Trains.Geometry;

namespace Trains.Track;

/// <summary>
/// A straight segment between two orthogonally adjacent grid nodes (distance 1).
/// </summary>
public sealed class StraightSegment : TrackSegment {
    public StraightSegment(string id, GridPoint a, GridPoint b) : base(id, a, b, distance: 1) {
        int dx = b.X - a.X;
        int dy = b.Y - a.Y;
        bool isOrthogonalNeighbor = Math.Abs(dx) + Math.Abs(dy) == 1;
        if (!isOrthogonalNeighbor)
            throw new ArgumentException("Straight segments must connect orthogonally adjacent nodes.");
    }

    public override IReadOnlyList<DirectedTrackEdge> GetDirectedEdges() {
        int dx = this.B.X - this.A.X;
        int dy = this.B.Y - this.A.Y;
        var dir = DirectionExtensions.FromOffset(dx, dy);

        return new[] {
            new DirectedTrackEdge(
                SegmentId: this.Id,
                FromNode: this.A,
                ToNode: this.B,
                EntryHeading: dir,
                ExitHeading: dir,
                Distance: this.Distance
            ),
            new DirectedTrackEdge(
                SegmentId: this.Id,
                FromNode: this.B,
                ToNode: this.A,
                EntryHeading: dir.Opposite(),
                ExitHeading: dir.Opposite(),
                Distance: this.Distance
            ),
        };
    }
}
