using Trains.Geometry;

namespace Trains.Track;

/// <summary>
/// A curved segment between diagonally adjacent grid nodes (length 1).
/// The curvature is disambiguated by choosing whether the tangent at the start is along X or along Y first.
/// </summary>
public sealed class CurvedSegment : TrackSegment {
    public CurvedSegment(string id, GridPoint a, GridPoint b, CurveBias bias) : base(id, a, b) {
        int dx = b.X - a.X;
        int dy = b.Y - a.Y;

        bool isDiagonalNeighbor = Math.Abs(dx) == 1 && Math.Abs(dy) == 1;
        if (!isDiagonalNeighbor)
            throw new ArgumentException("Curved segments must connect diagonally adjacent nodes.");

        this.Bias = bias;
    }

    public CurveBias Bias { get; }

    public override IReadOnlyList<DirectedTrackEdge> GetDirectedEdges() {
        int dx = this.B.X - this.A.X;
        int dy = this.B.Y - this.A.Y;

        Direction xDir = dx > 0 ? Direction.East : Direction.West;
        Direction yDir = dy > 0 ? Direction.North : Direction.South;

        Direction entry, exit;
        switch (this.Bias) {
            case CurveBias.XFirst:
                entry = xDir;
                exit = yDir;
                break;
            case CurveBias.YFirst:
                entry = yDir;
                exit = xDir;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(this.Bias), this.Bias, "Unknown curve bias.");
        }

        var aToB = new DirectedTrackEdge(
            SegmentId: this.Id,
            FromNode: this.A,
            ToNode: this.B,
            EntryHeading: entry,
            ExitHeading: exit
        );
        var bToA = aToB.Reverse();

        return new[] { aToB, bToA };
    }
}

public enum CurveBias {
    XFirst = 0,
    YFirst = 1,
}
