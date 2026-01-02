using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class ValidationAndBranchesTests {
    [Fact]
    public void TrackSegment_BaseValidation_BranchesCovered() {
        var p = new GridPoint(0, 0);

        Assert.Throws<ArgumentException>(() => new StraightSegment(" ", new GridPoint(0, 0), new GridPoint(1, 0)));
        Assert.Throws<ArgumentException>(() => new CurvedSegment("C", p, p, CurveBias.XFirst));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NegativeDistanceSegment("X", new GridPoint(0, 0), new GridPoint(1, 0)));
    }

    private sealed class NegativeDistanceSegment : TrackSegment {
        public NegativeDistanceSegment(string id, GridPoint a, GridPoint b) : base(id, a, b, distance: -1) { }
        public override IReadOnlyList<DirectedTrackEdge> GetDirectedEdges() => Array.Empty<DirectedTrackEdge>();
    }

    [Fact]
    public void Goal_FailureBranches() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var track = TrackLayout.Create(new[] { seg });

        var car0 = new CarSpec(0, length: 1, weight: 0);
        var car1 = new CarSpec(1, length: 1, weight: 0);

        // Occupied-required but empty -> false
        var puzzle1 = new ShuntingPuzzle(track, new RollingStockSpec[] { car0 }, new PuzzleState(), new Goal(new[] { new SegmentGoal("S0", allowedVehicleIds: null) }));
        Assert.False(puzzle1.IsSolved(new PuzzleState()));

        // Must be empty but occupied -> false
        var puzzle2 = new ShuntingPuzzle(track, new RollingStockSpec[] { car0 }, new PuzzleState(), new Goal(new[] { new SegmentGoal("S0", allowedVehicleIds: Array.Empty<int>()) }));
        var state2 = new PuzzleState();
        state2.Placements.Add(0, new VehiclePlacement(0, new[] { seg.GetDirectedEdges()[0] }));
        Assert.False(puzzle2.IsSolved(state2));

        // Allowed set but wrong vehicle -> false
        var puzzle3 = new ShuntingPuzzle(track, new RollingStockSpec[] { car0, car1 }, new PuzzleState(), new Goal(new[] { new SegmentGoal("S0", allowedVehicleIds: new[] { 1 }) }));
        var state3 = new PuzzleState();
        state3.Placements.Add(0, new VehiclePlacement(0, new[] { seg.GetDirectedEdges()[0] }));
        Assert.False(puzzle3.IsSolved(state3));
    }

    [Fact]
    public void ShuntingPuzzle_NullArgs_Throw() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var track = TrackLayout.Create(new[] { seg });
        var goal = new Goal(Array.Empty<SegmentGoal>());
        var state = new PuzzleState();

        Assert.Throws<ArgumentNullException>(() => new ShuntingPuzzle(null!, Array.Empty<RollingStockSpec>(), state, goal));
        Assert.Throws<ArgumentNullException>(() => new ShuntingPuzzle(track, null!, state, goal));
        Assert.Throws<ArgumentNullException>(() => new ShuntingPuzzle(track, Array.Empty<RollingStockSpec>(), null!, goal));
        Assert.Throws<ArgumentNullException>(() => new ShuntingPuzzle(track, Array.Empty<RollingStockSpec>(), state, null!));
    }

    [Fact]
    public void ShuntingEngine_StateValidation_RejectsBadSwitchState() {
        var segments = new TrackSegment[] { new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)) };
        var track = TrackLayout.Create(segments);
        var puzzle = new ShuntingPuzzle(track, Array.Empty<RollingStockSpec>(), new PuzzleState(), new Goal(Array.Empty<SegmentGoal>()));

        var state = new PuzzleState();
        state.SwitchStates[new TrackState(new GridPoint(0, 0), Direction.East)] = 0;

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new ToggleSwitchMove(new TrackState(new GridPoint(0, 0), Direction.East)));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.InvalidState, result.Error);
    }

    [Fact]
    public void ShuntingEngine_StateValidation_RejectsUnknownTurntableState() {
        var segments = new TrackSegment[] { new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)) };
        var track = TrackLayout.Create(segments);
        var puzzle = new ShuntingPuzzle(track, Array.Empty<RollingStockSpec>(), new PuzzleState(), new Goal(Array.Empty<SegmentGoal>()));

        var state = new PuzzleState();
        state.TurntableStates["NOPE"] = 0;

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new ToggleCouplingMove(0, VehicleEnd.Back));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.InvalidState, result.Error);
    }

    [Fact]
    public void ShuntingEngine_StateValidation_RejectsNonSymmetricCoupling() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
        };
        var track = TrackLayout.Create(segments);

        var a = new EngineSpec(id: 0, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);
        var b = new CarSpec(id: 1, length: 1, weight: 0);

        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { segments[0].GetDirectedEdges()[0] }));
        state.Placements.Add(1, new VehiclePlacement(1, new[] { segments[1].GetDirectedEdges()[0] }));

        // Only one side of the coupling exists.
        state.Couplings.Add(0, new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) });

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { a, b }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(a.Id, EngineMoveDirection.Forward));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.InvalidState, result.Error);
    }

    [Fact]
    public void ShuntingEngine_StateValidation_RejectsInvalidPlacementEdge() {
        var segments = new TrackSegment[] { new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)) };
        var track = TrackLayout.Create(segments);

        var car = new CarSpec(id: 0, length: 1, weight: 0);
        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { new DirectedTrackEdge("BAD", new GridPoint(0, 0), new GridPoint(1, 0), Direction.East, Direction.East, 1) }));

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { car }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new ToggleCouplingMove(0, VehicleEnd.Back));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.InvalidState, result.Error);
    }

    [Fact]
    public void ShuntingEngine_StateValidation_RejectsLongVehicleBlockingNode() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
            new StraightSegment("S2", new GridPoint(1, -1), new GridPoint(1, 0)),
        };
        var track = TrackLayout.Create(segments);

        var longCar = new CarSpec(id: 0, length: 2, weight: 0);
        var car = new CarSpec(id: 1, length: 1, weight: 0);

        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { segments[0].GetDirectedEdges()[0], segments[1].GetDirectedEdges()[0] }));
        state.Placements.Add(1, new VehiclePlacement(1, new[] { segments[2].GetDirectedEdges()[0] })); // passes through (1,0)

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { longCar, car }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new ToggleCouplingMove(1, VehicleEnd.Back));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.InvalidState, result.Error);
    }

    [Fact]
    public void ShuntingEngine_MoveEngine_WhenEngineHasNoPlacement_FailsInvalidState() {
        var segments = new TrackSegment[] { new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)) };
        var track = TrackLayout.Create(segments);

        var engine = new EngineSpec(id: 0, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);
        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { engine }, new PuzzleState(), new Goal(Array.Empty<SegmentGoal>()));

        var result = ShuntingEngine.TryApplyMove(puzzle, new PuzzleState(), new MoveEngineMove(engine.Id, EngineMoveDirection.Forward));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.InvalidState, result.Error);
    }
}
