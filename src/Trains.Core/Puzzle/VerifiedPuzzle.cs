using Trains.Engine;

namespace Trains.Puzzle;

/// <summary>
/// A puzzle together with a solution history whose current version is verified to solve the puzzle.
/// Intended for accepting new puzzles into a curated set.
/// </summary>
public sealed class VerifiedPuzzle {
    private VerifiedPuzzle(ShuntingPuzzle puzzle, SolutionHistorySnapshot solutionHistory) {
        this.Puzzle = puzzle;
        this.SolutionHistory = solutionHistory.DeepClone();
    }

    public ShuntingPuzzle Puzzle { get; }
    public SolutionHistorySnapshot SolutionHistory { get; }

    public Solution CurrentSolution => GetCurrentSolution(this.SolutionHistory);

    public static bool TryCreate(
        ShuntingPuzzle puzzle,
        SolutionHistorySnapshot solutionHistory,
        out VerifiedPuzzle? verifiedPuzzle,
        out SolutionVerificationResult verification
    ) {
        if (puzzle is null)
            throw new ArgumentNullException(nameof(puzzle));
        if (solutionHistory is null)
            throw new ArgumentNullException(nameof(solutionHistory));

        var solution = GetCurrentSolution(solutionHistory);
        verification = SolutionVerifier.Verify(puzzle, solution);
        if (!verification.IsValid || !verification.IsSolved) {
            verifiedPuzzle = null;
            return false;
        }

        verifiedPuzzle = new VerifiedPuzzle(puzzle, solutionHistory);
        return true;
    }

    private static Solution GetCurrentSolution(SolutionHistorySnapshot snapshot) {
        if (snapshot.History is null || snapshot.History.Count == 0)
            throw new ArgumentException("Solution history must contain at least one version.", nameof(snapshot));
        if (snapshot.CurrentVersion < 0 || snapshot.CurrentVersion >= snapshot.History.Count)
            throw new ArgumentOutOfRangeException(nameof(snapshot.CurrentVersion), snapshot.CurrentVersion, "CurrentVersion is out of range.");

        return snapshot.History[snapshot.CurrentVersion].ToSolution();
    }
}
