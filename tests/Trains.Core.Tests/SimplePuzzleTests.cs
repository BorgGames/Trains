using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class SimplePuzzleTests {
    [Fact]
    public void ExamplePuzzle_MovingForward_Solves() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
            new StraightSegment("S2", new GridPoint(2, 0), new GridPoint(3, 0)),
        };

        var track = TrackLayout.Create(segments);

        var car0 = new CarSpec(id: 0, length: 1, weight: 1);
        var engine1 = new EngineSpec(id: 1, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);

        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { segments[0].GetDirectedEdges()[0] }));
        state.Placements.Add(1, new VehiclePlacement(1, new[] { segments[1].GetDirectedEdges()[0] }));

        state.Couplings.Add(0, new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) });
        state.Couplings.Add(1, new VehicleCouplings { Back = new VehicleCoupling(0, VehicleEnd.Front) });

        var goal = new Goal(new[] {
            new SegmentGoal("S1", allowedVehicleIds: new[] { 0, 1 }),
            new SegmentGoal("S2", allowedVehicleIds: new[] { 1 }),
        });

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { car0, engine1 }, state, goal);

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engine1.Id, EngineMoveDirection.Forward));
        Assert.True(result.IsSuccess, result.Message);

        Assert.NotNull(result.State);
        Assert.True(puzzle.IsSolved(result.State!));
    }

    [Fact]
    public void CurvedSegment_AllowsTurn_AndAutoTogglesSwitch() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
            new CurvedSegment("C0", new GridPoint(1, 0), new GridPoint(2, 1), CurveBias.XFirst),
            new StraightSegment("S2", new GridPoint(2, 1), new GridPoint(2, 2)),
        };

        var track = TrackLayout.Create(segments);

        var engine = new EngineSpec(id: 1, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);

        var state = new PuzzleState();
        state.Placements.Add(1, new VehiclePlacement(1, new[] { segments[0].GetDirectedEdges()[0] }));

        // At (1,0) heading East, choose the curved option (index 1; index 0 is the straight segment).
        state.SwitchStates[new TrackState(new GridPoint(1, 0), Direction.East)] = 1;

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { engine }, state, new Goal(Array.Empty<SegmentGoal>()));

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engine.Id, EngineMoveDirection.Forward));
        Assert.True(result.IsSuccess, result.Message);

        Assert.NotNull(result.State);
        Assert.Equal(0, result.State!.SwitchStates[new TrackState(new GridPoint(1, 0), Direction.East)]);
        Assert.Equal(1, VehiclePlacement.CountUnitEdges(result.State.Placements[1].Edges));
        Assert.Contains(result.State.Placements[1].Edges, e => e.SegmentId == "C0");
        Assert.Contains(result.State.Placements[1].Edges, e => e.SegmentId == "S2");
    }
}
