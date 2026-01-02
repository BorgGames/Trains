namespace Trains.Puzzle;

/// <summary>
/// A set of segment occupancy constraints that define puzzle completion.
/// </summary>
public sealed class Goal {
    public Goal(IReadOnlyList<SegmentGoal> segmentGoals) {
        if (segmentGoals is null)
            throw new ArgumentNullException(nameof(segmentGoals));

        this.SegmentGoals = segmentGoals;
    }

    public IReadOnlyList<SegmentGoal> SegmentGoals { get; }

    public bool IsSatisfied(ShuntingPuzzle puzzle, PuzzleState state) {
        if (puzzle is null)
            throw new ArgumentNullException(nameof(puzzle));
        if (state is null)
            throw new ArgumentNullException(nameof(state));

        var occupancy = state.BuildSegmentOccupancy();
        foreach (var goal in this.SegmentGoals) {
            if (!goal.IsSatisfied(puzzle, occupancy))
                return false;
        }

        return true;
    }
}

/// <summary>
/// A constraint for a single segment id.
/// </summary>
public sealed class SegmentGoal {
    public SegmentGoal(string segmentId, IReadOnlyCollection<int>? allowedVehicleIds = null) {
        if (string.IsNullOrWhiteSpace(segmentId))
            throw new ArgumentException("Segment id must be non-empty.", nameof(segmentId));

        this.SegmentId = segmentId;
        this.AllowedVehicleIds = allowedVehicleIds;
    }

    public string SegmentId { get; }

    /// <summary>
    /// Null means: any vehicle allowed. Empty means: must be empty.
    /// Otherwise: must be occupied by a vehicle in the set.
    /// </summary>
    public IReadOnlyCollection<int>? AllowedVehicleIds { get; }

    internal bool IsSatisfied(ShuntingPuzzle puzzle, IReadOnlyDictionary<string, int> occupancy) {
        if (!puzzle.Track.IsKnownSegment(this.SegmentId))
            throw new InvalidOperationException($"Goal refers to unknown segment '{this.SegmentId}'.");

        bool occupied = occupancy.TryGetValue(this.SegmentId, out int vehicleId);

        if (this.AllowedVehicleIds is null)
            return occupied;

        if (this.AllowedVehicleIds.Count == 0)
            return !occupied;

        return occupied && this.AllowedVehicleIds.Contains(vehicleId);
    }
}
