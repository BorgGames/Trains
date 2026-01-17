using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Trains.Persistence;

namespace Trains.Web.Services;

public sealed class PuzzleProgressStore {
    private readonly TrainsDbContext _db;
    private readonly ILogger<PuzzleProgressStore> _log;

    public PuzzleProgressStore(TrainsDbContext db, ILogger<PuzzleProgressStore> log) {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Task<PuzzleSolveEntity?> GetAsync(Guid puzzleId, string userId, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id must be non-empty.", nameof(userId));

        return _db.PuzzleSolves.AsNoTracking().FirstOrDefaultAsync(x => x.PuzzleId == puzzleId && x.UserId == userId, ct);
    }

    public async Task UpsertPlayedAsync(Guid puzzleId, string userId, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id must be non-empty.", nameof(userId));

        var row = await _db.PuzzleSolves.FirstOrDefaultAsync(x => x.PuzzleId == puzzleId && x.UserId == userId, ct);
        if (row is null) {
            row = new PuzzleSolveEntity {
                PuzzleId = puzzleId,
                UserId = userId,
                SolvedAt = null,
                BestMoveCount = null,
                LastPlayedAt = DateTimeOffset.UtcNow,
            };
            _db.PuzzleSolves.Add(row);
        }
        else {
            row.LastPlayedAt = DateTimeOffset.UtcNow;
        }

        try {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) {
            if (row is not null)
                _db.Entry(row).State = EntityState.Detached;

            _log.LogDebug("Unique violation when upserting play progress; retrying once.");

            var row2 = await _db.PuzzleSolves.FirstOrDefaultAsync(x => x.PuzzleId == puzzleId && x.UserId == userId, ct);
            if (row2 is null)
                throw new InvalidOperationException("Concurrent insert failed and record was not found.");
            row2.LastPlayedAt = DateTimeOffset.UtcNow;
            try {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex2) when (IsUniqueViolation(ex2)) {
                throw new InvalidOperationException("Retry limit exceeded on unique violation.", ex2);
            }
        }
    }

    public async Task UpsertSolvedAsync(Guid puzzleId, string userId, int moveCount, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id must be non-empty.", nameof(userId));
        if (moveCount < 0)
            throw new ArgumentOutOfRangeException(nameof(moveCount), moveCount, "Move count must be non-negative.");

        var row = await _db.PuzzleSolves.FirstOrDefaultAsync(x => x.PuzzleId == puzzleId && x.UserId == userId, ct);
        if (row is null) {
            row = new PuzzleSolveEntity {
                PuzzleId = puzzleId,
                UserId = userId,
                SolvedAt = DateTimeOffset.UtcNow,
                BestMoveCount = moveCount,
                LastPlayedAt = DateTimeOffset.UtcNow,
            };
            _db.PuzzleSolves.Add(row);
        }
        else {
            row.LastPlayedAt = DateTimeOffset.UtcNow;
            if (row.SolvedAt is null)
                row.SolvedAt = DateTimeOffset.UtcNow;
            if (row.BestMoveCount is null || moveCount < row.BestMoveCount.Value)
                row.BestMoveCount = moveCount;
        }

        try {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) {
            if (row is not null)
                _db.Entry(row).State = EntityState.Detached;

            _log.LogDebug("Unique violation when upserting solve progress; retrying once.");

            var row2 = await _db.PuzzleSolves.FirstOrDefaultAsync(x => x.PuzzleId == puzzleId && x.UserId == userId, ct);
            if (row2 is null)
                throw new InvalidOperationException("Concurrent insert failed and record was not found.");

            row2.LastPlayedAt = DateTimeOffset.UtcNow;
            if (row2.SolvedAt is null)
                row2.SolvedAt = DateTimeOffset.UtcNow;
            if (row2.BestMoveCount is null || moveCount < row2.BestMoveCount.Value)
                row2.BestMoveCount = moveCount;

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
