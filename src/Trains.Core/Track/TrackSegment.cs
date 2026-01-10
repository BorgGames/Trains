using Trains.Geometry;

namespace Trains.Track;

/// <summary>
/// Base type for a track segment connecting two grid nodes.
/// </summary>
public abstract class TrackSegment {
    protected TrackSegment(string id, GridPoint a, GridPoint b) {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Segment id must be non-empty.", nameof(id));
        if (a == b)
            throw new ArgumentException("Segment endpoints must be different.");

        this.Id = id;
        this.A = a;
        this.B = b;
    }

    public string Id { get; }
    public GridPoint A { get; }
    public GridPoint B { get; }

    /// <summary>
    /// The length of traversing this segment.
    /// Track segments are unit-length (1). Longer track should be represented as multiple segments.
    /// </summary>
    public int Length => 1;

    public abstract IReadOnlyList<DirectedTrackEdge> GetDirectedEdges();
}
