using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Trains.Persistence;
using Trains.Web.Models;

namespace Trains.Web.Services;

public sealed class PuzzleCatalog {
    private readonly TrainsDbContext _db;

    public PuzzleCatalog(TrainsDbContext db) {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<PuzzleSummary>> ListPublishedAsync(string? userId, string? filter, CancellationToken ct) {
        var baseQuery = _db.Puzzles.AsNoTracking().Where(p => p.IsPublished);

        if (string.IsNullOrWhiteSpace(userId)) {
            return await baseQuery
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PuzzleSummary(p.Id, p.CreatedAt, p.ThumbnailSvg, IsSolved: false))
                .ToListAsync(ct);
        }

        filter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();

        var q =
            from p in baseQuery
            join s0 in _db.PuzzleSolves.AsNoTracking().Where(s => s.UserId == userId) on p.Id equals s0.PuzzleId into ss
            from s in ss.DefaultIfEmpty()
            select new {
                p.Id,
                p.CreatedAt,
                p.ThumbnailSvg,
                IsSolved = s != null && s.SolvedAt != null,
            };

        if (string.Equals(filter, "solved", StringComparison.Ordinal))
            q = q.Where(x => x.IsSolved);
        else if (string.Equals(filter, "unsolved", StringComparison.Ordinal))
            q = q.Where(x => !x.IsSolved);

        return await q
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PuzzleSummary(x.Id, x.CreatedAt, x.ThumbnailSvg, x.IsSolved))
            .ToListAsync(ct);
    }

    public Task<PuzzleEntity?> GetAsync(Guid id, CancellationToken ct) {
        return _db.Puzzles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.IsPublished, ct);
    }
}
