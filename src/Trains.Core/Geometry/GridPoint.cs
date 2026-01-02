namespace Trains.Geometry;

/// <summary>
/// A point on the integer grid.
/// </summary>
public readonly record struct GridPoint(int X, int Y) {
    public GridPoint Offset(Direction direction) {
        var (dx, dy) = direction.ToOffset();
        return new GridPoint(this.X + dx, this.Y + dy);
    }
}
