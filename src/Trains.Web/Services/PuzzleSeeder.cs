using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Trains.Engine;
using Trains.Geometry;
using Trains.Persistence;
using Trains.Puzzle;
using Trains.Puzzle.Serialization;
using Trains.Track;

namespace Trains.Web.Services;

public static class PuzzleSeeder {
    public static async Task EnsureCreatedAndSeedAsync(TrainsDbContext db, PuzzleSvgRenderer svg, CancellationToken ct) {
        if (db is null)
            throw new ArgumentNullException(nameof(db));
        if (svg is null)
            throw new ArgumentNullException(nameof(svg));

        // Allow running before migrations exist.
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync(ct);
        else
            await db.Database.EnsureCreatedAsync(ct);

        if (await db.Puzzles.AnyAsync(ct))
            return;

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

        var solution = new Solution(new SolutionMove[] { new MoveEngineSolutionMove(1, EngineMoveDirection.Forward) });
        var history = new InMemorySolutionHistory(solution);

        var puzzleSnapshot = PuzzleSnapshot.FromPuzzle(puzzle);
        var solutionHistorySnapshot = history.ToSnapshot();

        var entity = new PuzzleEntity {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = null,
            PuzzleJson = PuzzleJson.Serialize(puzzleSnapshot),
            SolutionHistoryJson = SolutionHistoryJson.Serialize(solutionHistorySnapshot),
            ThumbnailSvg = svg.RenderThumbnail(puzzleSnapshot),
            IsPublished = true,
        };

        db.Puzzles.Add(entity);
        await db.SaveChangesAsync(ct);
    }
}
