using Trains.Puzzle;

namespace Trains.Engine;

public readonly record struct SolutionVerificationResult(
    bool IsValid,
    bool IsSolved,
    int AppliedMoveCount,
    int? FailedMoveIndex,
    MoveError? Error,
    string? Message,
    PuzzleState? FinalState
);

public static class SolutionVerifier {
    public static SolutionVerificationResult Verify(ShuntingPuzzle puzzle, Solution solution) {
        if (puzzle is null)
            throw new ArgumentNullException(nameof(puzzle));
        if (solution is null)
            throw new ArgumentNullException(nameof(solution));

        var state = puzzle.InitialState.Clone();

        for (int i = 0; i < solution.Moves.Count; i++) {
            var move = solution.Moves[i].ToEngineMove();
            var result = ShuntingEngine.TryApplyMove(puzzle, state, move);
            if (!result.IsSuccess) {
                return new SolutionVerificationResult(
                    IsValid: false,
                    IsSolved: false,
                    AppliedMoveCount: i,
                    FailedMoveIndex: i,
                    Error: result.Error,
                    Message: result.Message,
                    FinalState: null
                );
            }

            state = result.State!;
        }

        bool solved = puzzle.IsSolved(state);
        return new SolutionVerificationResult(
            IsValid: true,
            IsSolved: solved,
            AppliedMoveCount: solution.Moves.Count,
            FailedMoveIndex: null,
            Error: null,
            Message: solved ? null : "Solution executed successfully but did not satisfy the goal.",
            FinalState: state
        );
    }
}

