using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class GoalTests {
    [Fact]
    public void Goal_NullArgs_Throw() {
        var goal = new Goal(Array.Empty<SegmentGoal>());
        Assert.Throws<ArgumentNullException>(() => goal.IsSatisfied(null!, new PuzzleState()));
        Assert.Throws<ArgumentNullException>(() => goal.IsSatisfied(
            new ShuntingPuzzle(
                TrackLayout.Create(new[] { new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)) }),
                Array.Empty<RollingStockSpec>(),
                new PuzzleState(),
                new Goal(Array.Empty<SegmentGoal>())
            ),
            null!
        ));
    }

    [Fact]
    public void SegmentGoal_NullAllowed_MeansOccupied() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var puzzle = new ShuntingPuzzle(
            TrackLayout.Create(new[] { seg }),
            new RollingStockSpec[] { new CarSpec(0, length: 1, weight: 0) },
            new PuzzleState(),
            goal: new Goal(new[] { new SegmentGoal("S0", allowedVehicleIds: null) })
        );

        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { seg.GetDirectedEdges()[0] }));
        Assert.True(puzzle.IsSolved(state));
    }

    [Fact]
    public void SegmentGoal_EmptyAllowed_MeansEmpty() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var puzzle = new ShuntingPuzzle(
            TrackLayout.Create(new[] { seg }),
            new RollingStockSpec[] { new CarSpec(0, length: 1, weight: 0) },
            new PuzzleState(),
            goal: new Goal(new[] { new SegmentGoal("S0", allowedVehicleIds: Array.Empty<int>()) })
        );

        Assert.True(puzzle.IsSolved(new PuzzleState()));
    }

    [Fact]
    public void SegmentGoal_ExplicitAllowedIds_Matches() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var puzzle = new ShuntingPuzzle(
            TrackLayout.Create(new[] { seg }),
            new RollingStockSpec[] { new CarSpec(0, length: 1, weight: 0), new CarSpec(1, length: 1, weight: 0) },
            new PuzzleState(),
            goal: new Goal(new[] { new SegmentGoal("S0", allowedVehicleIds: new[] { 1 }) })
        );

        var state = new PuzzleState();
        state.Placements.Add(1, new VehiclePlacement(1, new[] { seg.GetDirectedEdges()[0] }));
        Assert.True(puzzle.IsSolved(state));
    }
}
