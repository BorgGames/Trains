using Trains.Geometry;

namespace Trains.Track;

/// <summary>
/// A turntable centered at a grid node with a square "radius".
/// Ports are specified on the square border as a grid point plus the direction
/// that leads away from the turntable (outbound).
/// </summary>
public sealed class Turntable {
    public Turntable(
        string id,
        GridPoint center,
        int radius,
        IReadOnlyList<TurntablePort> ports,
        IReadOnlyList<TurntableAlignment> alignments
    ) {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Turntable id must be non-empty.", nameof(id));
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be positive.");
        if (ports is null)
            throw new ArgumentNullException(nameof(ports));
        if (alignments is null)
            throw new ArgumentNullException(nameof(alignments));
        if (ports.Count < 2)
            throw new ArgumentException("Turntable must have at least 2 ports.", nameof(ports));
        if (alignments.Count == 0)
            throw new ArgumentException("Turntable must have at least 1 alignment.", nameof(alignments));

        this.Id = id;
        this.Center = center;
        this.Radius = radius;
        this.Ports = ports;
        this.Alignments = alignments;

        ValidatePorts();
        ValidateAlignments();
    }

    public string Id { get; }
    public GridPoint Center { get; }
    public int Radius { get; }
    public IReadOnlyList<TurntablePort> Ports { get; }
    public IReadOnlyList<TurntableAlignment> Alignments { get; }

    public IEnumerable<DirectedTrackEdge> GetDirectedEdgesForAlignment(int alignmentIndex) {
        if (alignmentIndex < 0 || alignmentIndex >= this.Alignments.Count)
            throw new ArgumentOutOfRangeException(nameof(alignmentIndex), alignmentIndex, "Invalid alignment index.");

        var alignment = this.Alignments[alignmentIndex];

        var a = this.Ports[alignment.PortAIndex];
        var b = this.Ports[alignment.PortBIndex];

        // The turntable "bridge" is modeled as a chain of 2*Radius unit segments (length 1 each).
        // Current implementation supports a straight bridge between opposite ports on the same row/column,
        // passing through the turntable center, with length 2*Radius.
        var (points, bridgeDirection) = GetBridgePointsAndDirection(a, b);

        // Use stable segment ids for the physical bridge segments (independent of travel direction).
        // Segment id uniqueness per unit is important: it allows multiple trains to occupy disjoint parts of the bridge
        // and reuses normal segment-level collision/occupancy rules.
        for (int i = 0; i < points.Count - 1; i++) {
            string segmentId = $"Turntable:{this.Id}:{i}";

            var p0 = points[i];
            var p1 = points[i + 1];

            yield return new DirectedTrackEdge(
                SegmentId: segmentId,
                FromNode: p0,
                ToNode: p1,
                EntryHeading: bridgeDirection,
                ExitHeading: bridgeDirection
            );

            yield return new DirectedTrackEdge(
                SegmentId: segmentId,
                FromNode: p1,
                ToNode: p0,
                EntryHeading: bridgeDirection.Opposite(),
                ExitHeading: bridgeDirection.Opposite()
            );
        }
    }

    public bool ContainsStrictly(GridPoint p) {
        int minX = this.Center.X - this.Radius;
        int maxX = this.Center.X + this.Radius;
        int minY = this.Center.Y - this.Radius;
        int maxY = this.Center.Y + this.Radius;

        return p.X > minX && p.X < maxX && p.Y > minY && p.Y < maxY;
    }

    private void ValidatePorts() {
        for (int i = 0; i < this.Ports.Count; i++) {
            var port = this.Ports[i];
            int dx = port.Point.X - this.Center.X;
            int dy = port.Point.Y - this.Center.Y;

            int absDx = Math.Abs(dx);
            int absDy = Math.Abs(dy);

            bool onBorder = (absDx <= this.Radius && absDy <= this.Radius) && (absDx == this.Radius || absDy == this.Radius);
            if (!onBorder)
                throw new ArgumentException($"Port {i} is not on the turntable border.", nameof(Ports));

            bool outwardOk = false;
            if (absDx == this.Radius) {
                var expected = dx > 0 ? Direction.East : Direction.West;
                outwardOk |= port.OutboundDirection == expected;
            }
            if (absDy == this.Radius) {
                var expected = dy > 0 ? Direction.North : Direction.South;
                outwardOk |= port.OutboundDirection == expected;
            }

            if (!outwardOk)
                throw new ArgumentException($"Port {i} outbound direction does not point outward from the turntable.", nameof(Ports));
        }
    }

    private void ValidateAlignments() {
        for (int i = 0; i < this.Alignments.Count; i++) {
            var a = this.Alignments[i];

            if (a.PortAIndex < 0 || a.PortAIndex >= this.Ports.Count)
                throw new ArgumentOutOfRangeException(nameof(Alignments), $"Alignment {i} PortAIndex is out of range.");
            if (a.PortBIndex < 0 || a.PortBIndex >= this.Ports.Count)
                throw new ArgumentOutOfRangeException(nameof(Alignments), $"Alignment {i} PortBIndex is out of range.");
            if (a.PortAIndex == a.PortBIndex)
                throw new ArgumentException($"Alignment {i} must connect two different ports.", nameof(Alignments));

            // Ensure this alignment is representable as a straight bridge of length 2*Radius through Center.
            var pa = this.Ports[a.PortAIndex];
            var pb = this.Ports[a.PortBIndex];
            _ = GetBridgePointsAndDirection(pa, pb);
        }
    }

    private (IReadOnlyList<GridPoint> points, Direction bridgeDirection) GetBridgePointsAndDirection(TurntablePort a, TurntablePort b) {
        // Ports must be on opposite sides on a straight line through the center:
        // - Either East/West ports at y == Center.Y, or North/South ports at x == Center.X.
        // This matches a classic straight turntable bridge and guarantees total length 2*Radius.

        bool aIsHorizontal = a.Point.Y == this.Center.Y && (a.OutboundDirection == Direction.East || a.OutboundDirection == Direction.West);
        bool bIsHorizontal = b.Point.Y == this.Center.Y && (b.OutboundDirection == Direction.East || b.OutboundDirection == Direction.West);

        bool aIsVertical = a.Point.X == this.Center.X && (a.OutboundDirection == Direction.North || a.OutboundDirection == Direction.South);
        bool bIsVertical = b.Point.X == this.Center.X && (b.OutboundDirection == Direction.North || b.OutboundDirection == Direction.South);

        if (aIsHorizontal && bIsHorizontal) {
            // Must be on opposite sides at +/- Radius.
            if (a.Point.X != this.Center.X + this.Radius && a.Point.X != this.Center.X - this.Radius)
                throw new ArgumentException("Horizontal turntable ports must be at Center.X +/- Radius.", nameof(Ports));
            if (b.Point.X != this.Center.X + this.Radius && b.Point.X != this.Center.X - this.Radius)
                throw new ArgumentException("Horizontal turntable ports must be at Center.X +/- Radius.", nameof(Ports));
            if (a.Point.X == b.Point.X)
                throw new ArgumentException("Turntable alignment must connect opposite horizontal ports.", nameof(Alignments));

            // Build points in increasing X order; forward direction is East.
            int minX = Math.Min(a.Point.X, b.Point.X);
            int maxX = Math.Max(a.Point.X, b.Point.X);
            if (maxX - minX != 2 * this.Radius)
                throw new ArgumentException("Turntable bridge must have length 2*Radius.", nameof(Alignments));

            var pts = new List<GridPoint>(capacity: 2 * this.Radius + 1);
            for (int x = minX; x <= maxX; x++)
                pts.Add(new GridPoint(x, this.Center.Y));

            return (pts, Direction.East);
        }

        if (aIsVertical && bIsVertical) {
            // Must be on opposite sides at +/- Radius.
            if (a.Point.Y != this.Center.Y + this.Radius && a.Point.Y != this.Center.Y - this.Radius)
                throw new ArgumentException("Vertical turntable ports must be at Center.Y +/- Radius.", nameof(Ports));
            if (b.Point.Y != this.Center.Y + this.Radius && b.Point.Y != this.Center.Y - this.Radius)
                throw new ArgumentException("Vertical turntable ports must be at Center.Y +/- Radius.", nameof(Ports));
            if (a.Point.Y == b.Point.Y)
                throw new ArgumentException("Turntable alignment must connect opposite vertical ports.", nameof(Alignments));

            // Build points in increasing Y order; forward direction is North.
            int minY = Math.Min(a.Point.Y, b.Point.Y);
            int maxY = Math.Max(a.Point.Y, b.Point.Y);
            if (maxY - minY != 2 * this.Radius)
                throw new ArgumentException("Turntable bridge must have length 2*Radius.", nameof(Alignments));

            var pts = new List<GridPoint>(capacity: 2 * this.Radius + 1);
            for (int y = minY; y <= maxY; y++)
                pts.Add(new GridPoint(this.Center.X, y));

            return (pts, Direction.North);
        }

        throw new ArgumentException("Turntable alignments must connect two horizontal ports (East/West) or two vertical ports (North/South) on the center line.", nameof(Alignments));
    }
}

public readonly record struct TurntablePort(GridPoint Point, Direction OutboundDirection);

public readonly record struct TurntableAlignment(int PortAIndex, int PortBIndex);
