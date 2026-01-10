using System.Runtime.Serialization;
using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class PuzzleSolutionCoverageMoreTests {
    [Fact]
    public void ToggleCouplingMove_AmbiguousAdjacentVehicle_Fails() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
            new CurvedSegment("C0", new GridPoint(1, 0), new GridPoint(2, 1), CurveBias.XFirst),
        };
        var track = TrackLayout.Create(segments);

        var a = new CarSpec(id: 0, length: 1, weight: 0);
        var b = new CarSpec(id: 1, length: 1, weight: 0);
        var c = new CarSpec(id: 2, length: 1, weight: 0);

        var state = new PuzzleState();
        // Vehicle 0 ends at (1,0) with outward heading East on its front end.
        state.Placements.Add(0, new VehiclePlacement(0, new[] { segments[0].GetDirectedEdges()[0] })); // (0,0)->(1,0) East
        // Vehicle 1 has its back end at (1,0) with inward heading East.
        state.Placements.Add(1, new VehiclePlacement(1, new[] { segments[1].GetDirectedEdges()[0] })); // (1,0)->(2,0) East
        // Vehicle 2 also has its back end at (1,0) with inward heading East (curve entry East).
        state.Placements.Add(2, new VehiclePlacement(2, new[] { segments[2].GetDirectedEdges()[0] })); // (1,0)->(2,1) East->North

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { a, b, c }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new ToggleCouplingMove(0, VehicleEnd.Front));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.InvalidCoupling, result.Error);
    }

    [Fact]
    public void MoveEngine_CyclicCouplings_FailsWithNonLinearTrain() {
        // A 4-curve diamond loop where each curve changes heading by 90°.
        // This allows a physically valid cycle of couplings (no endpoints), which the engine rejects as non-linear.
        var c0 = new CurvedSegment("C0", new GridPoint(0, 0), new GridPoint(1, 1), CurveBias.XFirst);   // E->N
        var c1 = new CurvedSegment("C1", new GridPoint(1, 1), new GridPoint(0, 2), CurveBias.YFirst);   // N->W
        var c2 = new CurvedSegment("C2", new GridPoint(0, 2), new GridPoint(-1, 1), CurveBias.XFirst);  // W->S
        var c3 = new CurvedSegment("C3", new GridPoint(-1, 1), new GridPoint(0, 0), CurveBias.YFirst);  // S->E

        var track = TrackLayout.Create(new TrackSegment[] { c0, c1, c2, c3 });

        var car0 = new CarSpec(id: 0, length: 1, weight: 0);
        var car1 = new CarSpec(id: 1, length: 1, weight: 0);
        var car2 = new CarSpec(id: 2, length: 1, weight: 0);
        var engine3 = new EngineSpec(id: 3, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);

        var state = new PuzzleState();
        state.Placements[0] = new VehiclePlacement(0, new[] { c0.GetDirectedEdges()[0] });
        state.Placements[1] = new VehiclePlacement(1, new[] { c1.GetDirectedEdges()[0] });
        state.Placements[2] = new VehiclePlacement(2, new[] { c2.GetDirectedEdges()[0] });
        state.Placements[3] = new VehiclePlacement(3, new[] { c3.GetDirectedEdges()[0] });

        // Cycle couplings: 0->1->2->3->0 (front to back around the loop).
        state.Couplings[0] = new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) };
        state.Couplings[1] = new VehicleCouplings { Back = new VehicleCoupling(0, VehicleEnd.Front), Front = new VehicleCoupling(2, VehicleEnd.Back) };
        state.Couplings[2] = new VehicleCouplings { Back = new VehicleCoupling(1, VehicleEnd.Front), Front = new VehicleCoupling(3, VehicleEnd.Back) };
        state.Couplings[3] = new VehicleCouplings { Back = new VehicleCoupling(2, VehicleEnd.Front), Front = new VehicleCoupling(0, VehicleEnd.Back) };
        state.Couplings[0].Back = new VehicleCoupling(3, VehicleEnd.Front);

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { car0, car1, car2, engine3 }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engine3.Id, EngineMoveDirection.Forward));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.NonLinearTrain, result.Error);
    }

    [Fact]
    public void MoveEngine_SnakeStyleRotation_Succeeds() {
        var c0 = new CurvedSegment("C0", new GridPoint(0, 0), new GridPoint(1, 1), CurveBias.XFirst);   // E->N
        var c1 = new CurvedSegment("C1", new GridPoint(1, 1), new GridPoint(0, 2), CurveBias.YFirst);   // N->W
        var c2 = new CurvedSegment("C2", new GridPoint(0, 2), new GridPoint(-1, 1), CurveBias.XFirst);  // W->S
        var c3 = new CurvedSegment("C3", new GridPoint(-1, 1), new GridPoint(0, 0), CurveBias.YFirst);  // S->E

        var track = TrackLayout.Create(new TrackSegment[] { c0, c1, c2, c3 });

        var car0 = new CarSpec(id: 0, length: 1, weight: 0);
        var car1 = new CarSpec(id: 1, length: 1, weight: 0);
        var car2 = new CarSpec(id: 2, length: 1, weight: 0);
        var engine3 = new EngineSpec(id: 3, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);

        var state = new PuzzleState();
        state.Placements[0] = new VehiclePlacement(0, new[] { c0.GetDirectedEdges()[0] });
        state.Placements[1] = new VehiclePlacement(1, new[] { c1.GetDirectedEdges()[0] });
        state.Placements[2] = new VehiclePlacement(2, new[] { c2.GetDirectedEdges()[0] });
        state.Placements[3] = new VehiclePlacement(3, new[] { c3.GetDirectedEdges()[0] });

        // Linear chain occupying the whole loop (tail=0 on C0 ... head=3 on C3).
        state.Couplings[0] = new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) };
        state.Couplings[1] = new VehicleCouplings { Back = new VehicleCoupling(0, VehicleEnd.Front), Front = new VehicleCoupling(2, VehicleEnd.Back) };
        state.Couplings[2] = new VehicleCouplings { Back = new VehicleCoupling(1, VehicleEnd.Front), Front = new VehicleCoupling(3, VehicleEnd.Back) };
        state.Couplings[3] = new VehicleCouplings { Back = new VehicleCoupling(2, VehicleEnd.Front) };

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { car0, car1, car2, engine3 }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engine3.Id, EngineMoveDirection.Forward));
        Assert.True(result.IsSuccess, result.Message);

        // Head moves into the segment the tail vacates (snake-style rotation).
        Assert.Equal("C1", result.State!.Placements[0].Edges[0].SegmentId);
        Assert.Equal("C2", result.State!.Placements[1].Edges[0].SegmentId);
        Assert.Equal("C3", result.State!.Placements[2].Edges[0].SegmentId);
        Assert.Equal("C0", result.State!.Placements[3].Edges[0].SegmentId);
    }

    [Fact]
    public void MoveEngine_SnakeStyleRotation_WithLongTailVehicle_Succeeds() {
        var c0 = new CurvedSegment("C0", new GridPoint(0, 0), new GridPoint(1, 1), CurveBias.XFirst);   // E->N
        var c1 = new CurvedSegment("C1", new GridPoint(1, 1), new GridPoint(0, 2), CurveBias.YFirst);   // N->W
        var c2 = new CurvedSegment("C2", new GridPoint(0, 2), new GridPoint(-1, 1), CurveBias.XFirst);  // W->S
        var c3 = new CurvedSegment("C3", new GridPoint(-1, 1), new GridPoint(0, 0), CurveBias.YFirst);  // S->E

        var track = TrackLayout.Create(new TrackSegment[] { c0, c1, c2, c3 });

        var longTail = new CarSpec(id: 0, length: 2, weight: 0);
        var car1 = new CarSpec(id: 1, length: 1, weight: 0);
        var engine2 = new EngineSpec(id: 2, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);

        var state = new PuzzleState();
        state.Placements[0] = new VehiclePlacement(0, new[] { c0.GetDirectedEdges()[0], c1.GetDirectedEdges()[0] });
        state.Placements[1] = new VehiclePlacement(1, new[] { c2.GetDirectedEdges()[0] });
        state.Placements[2] = new VehiclePlacement(2, new[] { c3.GetDirectedEdges()[0] });

        state.Couplings[0] = new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) };
        state.Couplings[1] = new VehicleCouplings { Back = new VehicleCoupling(0, VehicleEnd.Front), Front = new VehicleCoupling(2, VehicleEnd.Back) };
        state.Couplings[2] = new VehicleCouplings { Back = new VehicleCoupling(1, VehicleEnd.Front) };

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { longTail, car1, engine2 }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engine2.Id, EngineMoveDirection.Forward));
        Assert.True(result.IsSuccess, result.Message);

        // Long tail car shifts by one edge: C0,C1 -> C1,C2; other cars shift accordingly.
        Assert.Equal(new[] { "C1", "C2" }, result.State!.Placements[0].Edges.Select(e => e.SegmentId).ToArray());
        Assert.Equal("C3", result.State!.Placements[1].Edges[0].SegmentId);
        Assert.Equal("C0", result.State!.Placements[2].Edges[0].SegmentId);
    }

    [Fact]
    public void MoveEngine_LoopDetected_WhenHeadEntersNonTailSegment_Fails() {
        // A contrived segment that allows jumping the head onto a segment already occupied by the train (not the tail),
        // to exercise the LoopDetected branch without relying on impossible geometry.
        var s0 = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var s1 = new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0));
        var jump = new JumpIntoSegment("J0", new GridPoint(2, 0), new GridPoint(3, 0), targetSegmentId: "S1");

        var track = TrackLayout.Create(new TrackSegment[] { s0, s1, jump });

        var tailCar = new CarSpec(id: 0, length: 1, weight: 0);
        var engine = new EngineSpec(id: 1, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);

        var state = new PuzzleState();
        state.Placements[0] = new VehiclePlacement(0, new[] { s0.GetDirectedEdges()[0] });
        state.Placements[1] = new VehiclePlacement(1, new[] { s1.GetDirectedEdges()[0] });
        state.Couplings[0] = new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) };
        state.Couplings[1] = new VehicleCouplings { Back = new VehicleCoupling(0, VehicleEnd.Front) };

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { tailCar, engine }, state, new Goal(Array.Empty<SegmentGoal>()));
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engine.Id, EngineMoveDirection.Forward));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.LoopDetected, result.Error);
    }

    private sealed record WeirdMove() : Move;

    [Fact]
    public void TryApplyMove_UnknownMoveType_FailsWithUnknown() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var track = TrackLayout.Create(new[] { seg });
        var puzzle = new ShuntingPuzzle(track, Array.Empty<RollingStockSpec>(), new PuzzleState(), new Goal(Array.Empty<SegmentGoal>()));

        var result = ShuntingEngine.TryApplyMove(puzzle, new PuzzleState(), new WeirdMove());
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.Unknown, result.Error);
    }

    [Fact]
    public void SolutionVerifier_ToggleSwitchThenMove_Solves() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
            new CurvedSegment("C0", new GridPoint(1, 0), new GridPoint(2, 1), CurveBias.XFirst),
            new StraightSegment("S2", new GridPoint(2, 1), new GridPoint(2, 2)),
        };

        var track = TrackLayout.Create(segments);
        var engine = new EngineSpec(id: 1, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);

        var state = new PuzzleState();
        state.Placements.Add(1, new VehiclePlacement(1, new[] { segments[0].GetDirectedEdges()[0] })); // on S0

        var puzzle = new ShuntingPuzzle(
            track,
            new RollingStockSpec[] { engine },
            state,
            new Goal(new[] { new SegmentGoal("S2", allowedVehicleIds: new[] { 1 }) })
        );

        var solution = new Solution(new SolutionMove[] {
            new ToggleSwitchSolutionMove(1, 0, Direction.East),
            new MoveEngineSolutionMove(1, EngineMoveDirection.Forward),
            new MoveEngineSolutionMove(1, EngineMoveDirection.Forward),
        });

        var verification = SolutionVerifier.Verify(puzzle, solution);
        Assert.True(verification.IsValid, verification.Message);
        Assert.True(verification.IsSolved, verification.Message);
        Assert.Contains(verification.FinalState!.Placements[1].Edges, e => e.SegmentId == "S2");
    }

    [Fact]
    public void SolutionHistoryJson_Deserialize_InvalidJson_Throws() {
        Assert.Throws<SerializationException>(() => SolutionHistoryJson.Deserialize("{not json"));
    }

    private sealed class JumpIntoSegment : TrackSegment {
        private readonly string _segmentId;

        public JumpIntoSegment(string id, GridPoint a, GridPoint b, string targetSegmentId) : base(id, a, b) {
            _segmentId = targetSegmentId;
        }

        public override IReadOnlyList<DirectedTrackEdge> GetDirectedEdges() {
            // Produce a "teleport" edge but with a segment id that matches another segment.
            // This keeps geometry valid while forcing a LoopDetected situation based on SegmentId occupancy.
            return new[] {
                new DirectedTrackEdge(_segmentId, this.A, this.B, Direction.East, Direction.East),
                new DirectedTrackEdge(_segmentId, this.B, this.A, Direction.West, Direction.West),
            };
        }
    }
}
