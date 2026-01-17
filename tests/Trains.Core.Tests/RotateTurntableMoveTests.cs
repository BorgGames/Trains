using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;
using Xunit;
using System.Linq;

namespace Trains.Core.Tests;

public sealed class RotateTurntableMoveTests {
    [Fact]
    public void RotateTurntable_CyclesAlignment() {
        var tt = BuildSimpleTurntable();
        var puzzle = new ShuntingPuzzle(
            track: TrackLayout.Create(Array.Empty<TrackSegment>(), new[] { tt }),
            rollingStock: Array.Empty<RollingStockSpec>(),
            initialState: new PuzzleState(),
            goal: new Goal(Array.Empty<SegmentGoal>())
        );

        var state = puzzle.InitialState.Clone();

        var r1 = ShuntingEngine.TryApplyMove(puzzle, state, new RotateTurntableMove(tt.Id));
        Assert.True(r1.IsSuccess);
        Assert.Equal(1, r1.State!.TurntableStates[tt.Id]);

        var r2 = ShuntingEngine.TryApplyMove(puzzle, r1.State!, new RotateTurntableMove(tt.Id));
        Assert.True(r2.IsSuccess);
        Assert.Equal(0, r2.State!.TurntableStates[tt.Id]);
    }

    [Fact]
    public void RotateTurntable_FailsWhenOccupied() {
        var tt = BuildSimpleTurntable();
        var track = TrackLayout.Create(Array.Empty<TrackSegment>(), new[] { tt });

        var car0 = new CarSpec(id: 0, length: 1, weight: 1);

        var state = new PuzzleState();
        var edge = tt.GetDirectedEdgesForAlignment(0).First();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { edge }));

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { car0 }, state, new Goal(Array.Empty<SegmentGoal>()));

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new RotateTurntableMove(tt.Id));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.InvalidTurntable, result.Error);
    }

    private static Turntable BuildSimpleTurntable() {
        var center = new GridPoint(0, 0);
        var ports = new[] {
            new TurntablePort(new GridPoint(-1, 0), Direction.West),
            new TurntablePort(new GridPoint(1, 0), Direction.East),
            new TurntablePort(new GridPoint(0, -1), Direction.South),
            new TurntablePort(new GridPoint(0, 1), Direction.North),
        };
        var alignments = new[] {
            new TurntableAlignment(0, 1),
            new TurntableAlignment(2, 3),
        };
        return new Turntable("T0", center, radius: 1, ports, alignments);
    }
}
