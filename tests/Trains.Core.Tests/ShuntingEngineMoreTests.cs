using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class ShuntingEngineMoreTests {
    [Fact]
    public void MoveEngine_NoTrackAhead_Fails() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
        };
        var track = TrackLayout.Create(segments);

        var engine = new EngineSpec(id: 0, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);
        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { segments[0].GetDirectedEdges()[0] }));

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { engine }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engine.Id, EngineMoveDirection.Forward));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.NoTrackAhead, result.Error);
    }

    [Fact]
    public void MoveEngine_OnCar_FailsWithNotAnEngine() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
        };
        var track = TrackLayout.Create(segments);

        var car = new CarSpec(id: 0, length: 1, weight: 0);
        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { segments[0].GetDirectedEdges()[0] }));

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { car }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(car.Id, EngineMoveDirection.Forward));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.NotAnEngine, result.Error);
    }

    [Fact]
    public void MoveEngine_Backward_MovesOneUnit() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
            new StraightSegment("S2", new GridPoint(2, 0), new GridPoint(3, 0)),
        };
        var track = TrackLayout.Create(segments);

        var engine = new EngineSpec(id: 0, length: 1, weight: 0, forwardPower: 0, backwardPower: 1);
        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { segments[1].GetDirectedEdges()[0] })); // 1->2

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { engine }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engine.Id, EngineMoveDirection.Backward));
        Assert.True(result.IsSuccess, result.Message);
        Assert.Contains(result.State!.Placements[0].Edges, e => e.SegmentId == "S0");
    }

    [Fact]
    public void ToggleSwitchMove_SucceedsAndCyclesIndex() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
            new CurvedSegment("C0", new GridPoint(1, 0), new GridPoint(2, 1), CurveBias.XFirst),
        };
        var track = TrackLayout.Create(segments);
        var puzzle = new ShuntingPuzzle(track, Array.Empty<RollingStockSpec>(), new PuzzleState(), new Goal(Array.Empty<SegmentGoal>()));

        var state = new PuzzleState();
        var key = new TrackState(new GridPoint(1, 0), Direction.East);
        state.SwitchStates[key] = 0;

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new ToggleSwitchMove(key));
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(1, result.State!.SwitchStates[key]);
    }

    [Fact]
    public void ToggleCouplingMove_Disconnects() {
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

        state.Couplings.Add(0, new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) });
        state.Couplings.Add(1, new VehicleCouplings { Back = new VehicleCoupling(0, VehicleEnd.Front) });

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { a, b }, state, new Goal(Array.Empty<SegmentGoal>()));

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new ToggleCouplingMove(0, VehicleEnd.Front));
        Assert.True(result.IsSuccess, result.Message);

        Assert.Null(result.State!.Couplings[0].Front);
        Assert.Null(result.State!.Couplings[1].Back);
    }

    [Fact]
    public void ToggleCouplingMove_Connects() {
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
        state.Couplings.Add(0, new VehicleCouplings());
        state.Couplings.Add(1, new VehicleCouplings());

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { a, b }, state, new Goal(Array.Empty<SegmentGoal>()));

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new ToggleCouplingMove(0, VehicleEnd.Front));
        Assert.True(result.IsSuccess, result.Message);

        Assert.NotNull(result.State!.Couplings[0].Front);
        Assert.NotNull(result.State!.Couplings[1].Back);
        Assert.Equal(1, result.State.Couplings[0].Front!.Value.OtherVehicleId);
        Assert.Equal(0, result.State.Couplings[1].Back!.Value.OtherVehicleId);
    }
}

