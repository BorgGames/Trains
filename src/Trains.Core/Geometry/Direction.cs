namespace Trains.Geometry;

/// <summary>
/// A 4-connected grid direction.
/// </summary>
public enum Direction {
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

public static class DirectionExtensions {
    public static Direction Opposite(this Direction direction) =>
        direction switch {
            Direction.North => Direction.South,
            Direction.East => Direction.West,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction."),
        };

    public static (int dx, int dy) ToOffset(this Direction direction) =>
        direction switch {
            Direction.North => (0, 1),
            Direction.East => (1, 0),
            Direction.South => (0, -1),
            Direction.West => (-1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction."),
        };

    public static Direction FromOffset(int dx, int dy) =>
        (dx, dy) switch {
            (0, 1) => Direction.North,
            (1, 0) => Direction.East,
            (0, -1) => Direction.South,
            (-1, 0) => Direction.West,
            _ => throw new ArgumentOutOfRangeException(nameof(dx), $"Offset ({dx},{dy}) is not a cardinal direction."),
        };
}
