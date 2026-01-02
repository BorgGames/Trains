using Trains.Geometry;

namespace Trains.Track;

/// <summary>
/// Represents being at a grid node with a current heading (the direction of travel when arriving at the node).
/// </summary>
public readonly record struct TrackState(GridPoint Node, Direction Heading);
