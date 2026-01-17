using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Trains.Persistence;

namespace Trains.Web.Services;

public sealed class PuzzleVotingStore {
    private readonly TrainsDbContext _db;
    private readonly ILogger<PuzzleVotingStore> _log;

    public PuzzleVotingStore(TrainsDbContext db, ILogger<PuzzleVotingStore> log) {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Task<PuzzleVoteEntity?> GetAsync(Guid puzzleId, string userId, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id must be non-empty.", nameof(userId));

        return _db.PuzzleVotes.AsNoTracking().FirstOrDefaultAsync(x => x.PuzzleId == puzzleId && x.UserId == userId, ct);
    }

    public async Task UpsertAsync(Guid puzzleId, string userId, short difficulty, short score, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id must be non-empty.", nameof(userId));
        if (difficulty is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Difficulty must be 1..5.");
        if (score is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(score), score, "Score must be 1..5.");

        var row = await _db.PuzzleVotes.FirstOrDefaultAsync(x => x.PuzzleId == puzzleId && x.UserId == userId, ct);
        if (row is null) {
            row = new PuzzleVoteEntity {
                PuzzleId = puzzleId,
                UserId = userId,
                Difficulty = difficulty,
                Score = score,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _db.PuzzleVotes.Add(row);
        }
        else {
            row.Difficulty = difficulty;
            row.Score = score;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        try {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) {
            if (row is not null)
                _db.Entry(row).State = EntityState.Detached;

            _log.LogDebug("Unique violation when upserting vote; retrying once.");

            var row2 = await _db.PuzzleVotes.FirstOrDefaultAsync(x => x.PuzzleId == puzzleId && x.UserId == userId, ct);
            if (row2 is null)
                throw new InvalidOperationException("Concurrent insert failed and record was not found.");
            row2.Difficulty = difficulty;
            row2.Score = score;
            row2.UpdatedAt = DateTimeOffset.UtcNow;
            try {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex2) when (IsUniqueViolation(ex2)) {
                throw new InvalidOperationException("Retry limit exceeded on unique violation.", ex2);
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && string.Equals(pg.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal);
}
