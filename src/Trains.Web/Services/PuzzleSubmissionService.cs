using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Trains.Engine;
using Trains.Persistence;
using Trains.Puzzle;
using Trains.Puzzle.Serialization;

namespace Trains.Web.Services;

public sealed record PuzzleSubmissionResult(bool IsAccepted, Guid? PuzzleId, string? Message);

public sealed class PuzzleSubmissionService {
    private readonly TrainsDbContext _db;
    private readonly PuzzleSvgRenderer _svg;
    private readonly ILogger<PuzzleSubmissionService> _log;

    public PuzzleSubmissionService(TrainsDbContext db, PuzzleSvgRenderer svg, ILogger<PuzzleSubmissionService> log) {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _svg = svg ?? throw new ArgumentNullException(nameof(svg));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<PuzzleSubmissionResult> SubmitAsync(
        string puzzleJson,
        string solutionHistoryJson,
        string createdByUserId,
        CancellationToken ct
    ) {
        if (string.IsNullOrWhiteSpace(puzzleJson))
            return new PuzzleSubmissionResult(false, null, "Puzzle JSON is required.");
        if (string.IsNullOrWhiteSpace(solutionHistoryJson))
            return new PuzzleSubmissionResult(false, null, "Solution history JSON is required.");
        if (string.IsNullOrWhiteSpace(createdByUserId))
            return new PuzzleSubmissionResult(false, null, "User id is required.");

        if (puzzleJson.Length > 1_000_000)
            return new PuzzleSubmissionResult(false, null, "Puzzle JSON is too large.");
        if (solutionHistoryJson.Length > 1_000_000)
            return new PuzzleSubmissionResult(false, null, "Solution history JSON is too large.");

        PuzzleSnapshot puzzleSnapshot;
        SolutionHistorySnapshot historySnapshot;
        try {
            puzzleSnapshot = PuzzleJson.Deserialize(puzzleJson);
        }
        catch (Exception ex) {
            _log.LogInformation(ex, "Puzzle submission rejected: invalid puzzle JSON.");
            return new PuzzleSubmissionResult(false, null, "Invalid puzzle JSON.");
        }

        try {
            historySnapshot = SolutionHistoryJson.Deserialize(solutionHistoryJson);
        }
        catch (Exception ex) {
            _log.LogInformation(ex, "Puzzle submission rejected: invalid solution history JSON.");
            return new PuzzleSubmissionResult(false, null, "Invalid solution history JSON.");
        }

        if (historySnapshot.SchemaVersion != SolutionHistorySnapshot.CurrentSchemaVersion)
            return new PuzzleSubmissionResult(false, null, $"Unsupported solution schema version {historySnapshot.SchemaVersion}.");

        ShuntingPuzzle puzzle;
        try {
            puzzle = puzzleSnapshot.ToPuzzle();
        }
        catch (Exception ex) {
            _log.LogInformation(ex, "Puzzle submission rejected: invalid puzzle definition.");
            return new PuzzleSubmissionResult(false, null, "Invalid puzzle definition.");
        }

        if (!VerifiedPuzzle.TryCreate(puzzle, historySnapshot, out var verified, out var verification)) {
            string msg =
                verification.IsValid
                    ? (verification.Message ?? "Solution did not satisfy the goal.")
                    : (verification.Message ?? "Solution could not be executed.");

            return new PuzzleSubmissionResult(false, null, $"Puzzle rejected: {msg}");
        }

        puzzleSnapshot.VerifiedSolutionHistory = verified!.SolutionHistory.DeepClone();

        var entity = new PuzzleEntity {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = createdByUserId,
            PuzzleJson = PuzzleJson.Serialize(puzzleSnapshot),
            SolutionHistoryJson = SolutionHistoryJson.Serialize(historySnapshot),
            ThumbnailSvg = _svg.RenderThumbnail(puzzleSnapshot),
            IsPublished = true,
        };

        _db.Puzzles.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new PuzzleSubmissionResult(true, entity.Id, null);
    }
}
