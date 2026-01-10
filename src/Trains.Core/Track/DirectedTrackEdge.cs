using Trains.Geometry;

namespace Trains.Track;

/// <summary>
/// A directed traversal of a track segment.
/// </summary>
public readonly record struct DirectedTrackEdge(
    string SegmentId,
    GridPoint FromNode,
    GridPoint ToNode,
    Direction EntryHeading,
    Direction ExitHeading
) {
    /// <summary>
    /// Track edges are unit-length (1). Longer track is represented as multiple edges.
    /// </summary>
    public int Length => 1;

    public TrackState FromState => new(this.FromNode, this.EntryHeading);
    public TrackState ToState => new(this.ToNode, this.ExitHeading);

    public DirectedTrackEdge Reverse() =>
        new(
            SegmentId: this.SegmentId,
            FromNode: this.ToNode,
            ToNode: this.FromNode,
            EntryHeading: this.ExitHeading.Opposite(),
            ExitHeading: this.EntryHeading.Opposite()
        );
}
