using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Puzzle.Serialization;
using Trains.Track;
using Trains.Web.Services;
using Xunit;

namespace Trains.Persistence.IntegrationTests;

public sealed class TrainsDbContextIntegrationTests {
    [SkippableFact]
    public async Task Migrations_ApplySuccessfully() {
        await using var db = await PostgresTempDatabase.CreateAndMigrateAsync();
        var options = new DbContextOptionsBuilder<TrainsDbContext>().UseNpgsql(db.ConnectionString).Options;
        await using var ctx = new TrainsDbContext(options);
        var applied = (await ctx.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.NotEmpty(applied);
    }

    [SkippableFact]
    public async Task VotesAndSolves_CascadeOnPuzzleDelete() {
        await using var db = await PostgresTempDatabase.CreateAndMigrateAsync();
        var options = new DbContextOptionsBuilder<TrainsDbContext>().UseNpgsql(db.ConnectionString).Options;
        await using var ctx = new TrainsDbContext(options);

        var user = new IdentityUser { Id = "u1", UserName = "u1" };
        ctx.Users.Add(user);

        var puzzle = new PuzzleEntity {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = user.Id,
            PuzzleJson = "{}",
            SolutionHistoryJson = "{}",
            ThumbnailSvg = "<svg/>",
            IsPublished = true,
        };
        ctx.Puzzles.Add(puzzle);

        ctx.PuzzleVotes.Add(new PuzzleVoteEntity { PuzzleId = puzzle.Id, UserId = user.Id, Difficulty = 3, Score = 4, UpdatedAt = DateTimeOffset.UtcNow });
        ctx.PuzzleSolves.Add(new PuzzleSolveEntity { PuzzleId = puzzle.Id, UserId = user.Id, SolvedAt = null, BestMoveCount = null, LastPlayedAt = DateTimeOffset.UtcNow });

        await ctx.SaveChangesAsync();

        ctx.Puzzles.Remove(puzzle);
        await ctx.SaveChangesAsync();

        Assert.Empty(await ctx.PuzzleVotes.Where(v => v.PuzzleId == puzzle.Id).ToListAsync());
        Assert.Empty(await ctx.PuzzleSolves.Where(s => s.PuzzleId == puzzle.Id).ToListAsync());
    }

    [SkippableFact]
    public async Task SubmissionService_PublishesOnlyIfVerifiedSolutionSolves() {
        await using var db = await PostgresTempDatabase.CreateAndMigrateAsync();
        var options = new DbContextOptionsBuilder<TrainsDbContext>().UseNpgsql(db.ConnectionString).Options;
        await using var ctx = new TrainsDbContext(options);

        var user = new IdentityUser { Id = "u2", UserName = "u2" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
            new StraightSegment("S2", new GridPoint(2, 0), new GridPoint(3, 0)),
        };
        var track = TrackLayout.Create(segments);

        var car0 = new CarSpec(id: 0, length: 1, weight: 1);
        var engine1 = new EngineSpec(id: 1, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);

        var initial = new PuzzleState();
        initial.Placements.Add(0, new VehiclePlacement(0, new[] { segments[0].GetDirectedEdges()[0] }));
        initial.Placements.Add(1, new VehiclePlacement(1, new[] { segments[1].GetDirectedEdges()[0] }));
        initial.Couplings.Add(0, new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) });
        initial.Couplings.Add(1, new VehicleCouplings { Back = new VehicleCoupling(0, VehicleEnd.Front) });

        var goal = new Goal(new[] {
            new SegmentGoal("S1", allowedVehicleIds: new[] { 0, 1 }),
            new SegmentGoal("S2", allowedVehicleIds: new[] { 1 }),
        });

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { car0, engine1 }, initial, goal);
        var puzzleJson = PuzzleJson.Serialize(PuzzleSnapshot.FromPuzzle(puzzle));

        var solution = new Solution(new SolutionMove[] { new MoveEngineSolutionMove(1, EngineMoveDirection.Forward) });
        var history = new InMemorySolutionHistory(solution).ToSnapshot();
        var solutionHistoryJson = SolutionHistoryJson.Serialize(history);

        var service = new PuzzleSubmissionService(ctx, new PuzzleSvgRenderer(), Microsoft.Extensions.Logging.Abstractions.NullLogger<PuzzleSubmissionService>.Instance);

        var result = await service.SubmitAsync(puzzleJson, solutionHistoryJson, user.Id, default);
        Assert.True(result.IsAccepted);

        var entity = await ctx.Puzzles.AsNoTracking().FirstAsync(p => p.Id == result.PuzzleId);
        Assert.True(entity.IsPublished);

        var storedSnapshot = PuzzleJson.Deserialize(entity.PuzzleJson);
        Assert.NotNull(storedSnapshot.VerifiedSolutionHistory);
    }
}
