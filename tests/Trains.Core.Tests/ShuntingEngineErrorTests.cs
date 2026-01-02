using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class ShuntingEngineErrorTests {
    [Fact]
    public void MoveEngine_InsufficientPower_Fails() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
        };
        var track = TrackLayout.Create(segments);

        var engine = new EngineSpec(id: 0, length: 1, weight: 0, forwardPower: 0, backwardPower: 1);
        var car = new CarSpec(id: 1, length: 1, weight: 1);

        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { segments[0].GetDirectedEdges()[0] }));
        state.Placements.Add(1, new VehiclePlacement(1, new[] { segments[1].GetDirectedEdges()[0] }));
        state.Couplings.Add(0, new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) });
        state.Couplings.Add(1, new VehicleCouplings { Back = new VehicleCoupling(0, VehicleEnd.Front) });

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { engine, car }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engine.Id, EngineMoveDirection.Forward));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.InsufficientPower, result.Error);
    }

    [Fact]
    public void MoveEngine_Collision_Fails() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
        };
        var track = TrackLayout.Create(segments);

        var engine = new EngineSpec(id: 0, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);
        var blocker = new CarSpec(id: 1, length: 1, weight: 0);

        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { segments[0].GetDirectedEdges()[0] }));
        state.Placements.Add(1, new VehiclePlacement(1, new[] { segments[1].GetDirectedEdges()[0] }));

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { engine, blocker }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engine.Id, EngineMoveDirection.Forward));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.Collision, result.Error);
    }

    [Fact]
    public void ToggleSwitchMove_OnNonSwitch_Fails() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
        };
        var puzzle = new ShuntingPuzzle(TrackLayout.Create(segments), Array.Empty<RollingStockSpec>(), new PuzzleState(), new Goal(Array.Empty<SegmentGoal>()));
        var state = new PuzzleState();

        var result = ShuntingEngine.TryApplyMove(
            puzzle,
            state,
            new ToggleSwitchMove(new TrackState(new GridPoint(0, 0), Direction.East))
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.InvalidSwitch, result.Error);
    }

    [Fact]
    public void ToggleCoupling_NoAdjacentVehicle_Fails() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var puzzle = new ShuntingPuzzle(
            TrackLayout.Create(new[] { seg }),
            new RollingStockSpec[] { new CarSpec(0, length: 1, weight: 0) },
            new PuzzleState(),
            new Goal(Array.Empty<SegmentGoal>())
        );

        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { seg.GetDirectedEdges()[0] }));

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new ToggleCouplingMove(0, VehicleEnd.Front));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.InvalidCoupling, result.Error);
    }
}
