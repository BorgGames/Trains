using Trains.Track;

namespace Trains.Puzzle;

/// <summary>
/// Immutable puzzle definition: track layout, rolling stock definitions, initial state, and goal.
/// </summary>
public sealed class ShuntingPuzzle {
    public ShuntingPuzzle(
        TrackLayout track,
        IReadOnlyList<RollingStockSpec> rollingStock,
        PuzzleState initialState,
        Goal goal
    ) {
        this.Track = track ?? throw new ArgumentNullException(nameof(track));
        if (rollingStock is null)
            throw new ArgumentNullException(nameof(rollingStock));
        this.InitialState = initialState ?? throw new ArgumentNullException(nameof(initialState));
        this.Goal = goal ?? throw new ArgumentNullException(nameof(goal));

        var byId = new Dictionary<int, RollingStockSpec>();
        foreach (var spec in rollingStock) {
            if (byId.ContainsKey(spec.Id))
                throw new ArgumentException($"Duplicate rolling stock id {spec.Id}.", nameof(rollingStock));
            byId.Add(spec.Id, spec);
        }
        this.RollingStock = byId;
    }

    public TrackLayout Track { get; }
    public IReadOnlyDictionary<int, RollingStockSpec> RollingStock { get; }
    public PuzzleState InitialState { get; }
    public Goal Goal { get; }

    public bool IsSolved(PuzzleState state) => this.Goal.IsSatisfied(this, state);
}
