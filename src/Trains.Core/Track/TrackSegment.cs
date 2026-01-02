using Trains.Geometry;

namespace Trains.Track;

/// <summary>
/// Base type for a track segment connecting two grid nodes.
/// </summary>
public abstract class TrackSegment {
    protected TrackSegment(string id, GridPoint a, GridPoint b, int distance) {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Segment id must be non-empty.", nameof(id));
        if (a == b)
            throw new ArgumentException("Segment endpoints must be different.");
        if (distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance), distance, "Distance must be non-negative.");

        this.Id = id;
        this.A = a;
        this.B = b;
        this.Distance = distance;
    }

    public string Id { get; }
    public GridPoint A { get; }
    public GridPoint B { get; }

    /// <summary>
    /// The distance cost of traversing this segment (curves can be 0).
    /// </summary>
    public int Distance { get; }

    public abstract IReadOnlyList<DirectedTrackEdge> GetDirectedEdges();
}
