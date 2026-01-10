namespace Trains.Puzzle;

/// <summary>
/// An immutable sequence of moves.
/// </summary>
public sealed class Solution {
    public Solution(IReadOnlyList<SolutionMove> moves) {
        if (moves is null)
            throw new ArgumentNullException(nameof(moves));

        for (int i = 0; i < moves.Count; i++) {
            if (moves[i] is null)
                throw new ArgumentException("Solution moves cannot contain null entries.", nameof(moves));
        }

        this.Moves = moves.ToArray();
    }

    public IReadOnlyList<SolutionMove> Moves { get; }

    /// <summary>
    /// Clones the solution by copying the move list.
    /// This is a shallow clone of the moves (they are immutable records).
    /// </summary>
    public Solution Clone() => new(this.Moves.ToArray());
}
