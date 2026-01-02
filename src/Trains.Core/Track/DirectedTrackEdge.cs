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
    Direction ExitHeading,
    int Distance
) {
    public TrackState FromState => new(this.FromNode, this.EntryHeading);
    public TrackState ToState => new(this.ToNode, this.ExitHeading);

    public DirectedTrackEdge Reverse() =>
        new(
            SegmentId: this.SegmentId,
            FromNode: this.ToNode,
            ToNode: this.FromNode,
            EntryHeading: this.ExitHeading.Opposite(),
            ExitHeading: this.EntryHeading.Opposite(),
            Distance: this.Distance
        );
}
