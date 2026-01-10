using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class SolutionHistoryTests {
    [Fact]
    public void InMemorySolutionHistory_Add_Undo_Redo_AndTrim_Work() {
        var s0 = new Solution(Array.Empty<SolutionMove>());
        var history = new InMemorySolutionHistory(s0);

        Assert.Equal(0, history.CurrentVersion);
        Assert.Equal(0, history.LatestVersion);
        Assert.Empty(history.CurrentSolution.Moves);

        var s1 = new Solution(new SolutionMove[] { new MoveEngineSolutionMove(1, EngineMoveDirection.Forward) });
        history.Add(s1);
        Assert.Equal(1, history.CurrentVersion);
        Assert.Equal(1, history.LatestVersion);
        Assert.Single(history.CurrentSolution.Moves);

        var s2 = new Solution(new SolutionMove[] { new MoveEngineSolutionMove(1, EngineMoveDirection.Backward) });
        history.Add(s2);
        Assert.Equal(2, history.CurrentVersion);
        Assert.Equal(2, history.LatestVersion);

        var undo = history.Undo();
        Assert.Equal(1, history.CurrentVersion);
        Assert.Single(undo.Moves);
        Assert.IsType<MoveEngineSolutionMove>(undo.Moves[0]);
        Assert.Equal(EngineMoveDirection.Forward, ((MoveEngineSolutionMove)undo.Moves[0]).Direction);

        // Adding after Undo trims redo history.
        var s3 = new Solution(new SolutionMove[] { new ToggleCouplingSolutionMove(0, VehicleEnd.Front) });
        history.Add(s3);
        Assert.Equal(2, history.CurrentVersion);
        Assert.Equal(2, history.LatestVersion);
        Assert.IsType<ToggleCouplingSolutionMove>(history.CurrentSolution.Moves[0]);

        Assert.Throws<InvalidOperationException>(() => history.Redo());
    }

    [Fact]
    public void InMemorySolutionHistory_UndoAtStart_Throws() {
        var history = new InMemorySolutionHistory(new Solution(Array.Empty<SolutionMove>()));
        Assert.Throws<InvalidOperationException>(() => history.Undo());
    }

    [Fact]
    public void InMemorySolutionHistory_RedoAtLatest_Throws() {
        var history = new InMemorySolutionHistory(new Solution(Array.Empty<SolutionMove>()));
        Assert.Throws<InvalidOperationException>(() => history.Redo());
    }

    [Fact]
    public void SolutionHistorySnapshot_RoundTrips_Json() {
        var history = new InMemorySolutionHistory(new Solution(Array.Empty<SolutionMove>()));
        history.Add(new Solution(new SolutionMove[] { new MoveEngineSolutionMove(1, EngineMoveDirection.Forward) }));
        history.Undo();

        var snapshot = history.ToSnapshot();
        var json = SolutionHistoryJson.Serialize(snapshot);
        var restoredSnapshot = SolutionHistoryJson.Deserialize(json);

        var restored = InMemorySolutionHistory.FromSnapshot(restoredSnapshot);
        Assert.Equal(snapshot.CurrentVersion, restored.CurrentVersion);
        Assert.Equal(snapshot.History.Count, restoredSnapshot.History.Count);
    }

    [Fact]
    public void InMemorySolutionHistory_FromSnapshot_ValidatesSchemaAndBounds() {
        var baseSnapshot = new SolutionHistorySnapshot {
            SchemaVersion = SolutionHistorySnapshot.CurrentSchemaVersion,
            CurrentVersion = 0,
            History = new List<SolutionSnapshot> { new SolutionSnapshot { Moves = new List<SolutionMoveSnapshot>() } },
        };

        Assert.Throws<NotSupportedException>(() => InMemorySolutionHistory.FromSnapshot(new SolutionHistorySnapshot {
            SchemaVersion = 999,
            CurrentVersion = 0,
            History = baseSnapshot.History,
        }));

        Assert.Throws<ArgumentException>(() => InMemorySolutionHistory.FromSnapshot(new SolutionHistorySnapshot {
            SchemaVersion = SolutionHistorySnapshot.CurrentSchemaVersion,
            CurrentVersion = 0,
            History = new List<SolutionSnapshot>(),
        }));

        Assert.Throws<ArgumentOutOfRangeException>(() => InMemorySolutionHistory.FromSnapshot(new SolutionHistorySnapshot {
            SchemaVersion = SolutionHistorySnapshot.CurrentSchemaVersion,
            CurrentVersion = 5,
            History = baseSnapshot.History,
        }));
    }

    [Fact]
    public void SolutionHistoryJson_NullArguments_Throw() {
        Assert.Throws<ArgumentNullException>(() => SolutionHistoryJson.Serialize(null!));
        Assert.Throws<ArgumentNullException>(() => SolutionHistoryJson.Deserialize(null!));
    }

    [Fact]
    public void SolutionVerifier_ValidSolution_SolvesPuzzle() {
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
        var solution = new Solution(new SolutionMove[] { new MoveEngineSolutionMove(1, EngineMoveDirection.Forward) });

        var verification = SolutionVerifier.Verify(puzzle, solution);
        Assert.True(verification.IsValid, verification.Message);
        Assert.True(verification.IsSolved, verification.Message);
        Assert.NotNull(verification.FinalState);
        Assert.True(puzzle.IsSolved(verification.FinalState!));
    }

    [Fact]
    public void SolutionVerifier_InvalidSolution_FailsOnMove() {
        var segments = new TrackSegment[] {
            new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
        };
        var track = TrackLayout.Create(segments);

        var car0 = new CarSpec(id: 0, length: 1, weight: 2);
        var engine1 = new EngineSpec(id: 1, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);

        var state = new PuzzleState();
        state.Placements.Add(0, new VehiclePlacement(0, new[] { segments[0].GetDirectedEdges()[0] }));
        state.Placements.Add(1, new VehiclePlacement(1, new[] { segments[1].GetDirectedEdges()[0] }));
        state.Couplings.Add(0, new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) });
        state.Couplings.Add(1, new VehicleCouplings { Back = new VehicleCoupling(0, VehicleEnd.Front) });

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { car0, engine1 }, state, new Goal(Array.Empty<SegmentGoal>()));
        var solution = new Solution(new SolutionMove[] { new MoveEngineSolutionMove(1, EngineMoveDirection.Forward) });

        var verification = SolutionVerifier.Verify(puzzle, solution);
        Assert.False(verification.IsValid);
        Assert.Equal(0, verification.FailedMoveIndex);
        Assert.Equal(MoveError.InsufficientPower, verification.Error);
    }

    [Fact]
    public void VerifiedPuzzle_TryCreate_RejectsUnsolvedSolution() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var track = TrackLayout.Create(new TrackSegment[] { seg });

        var engine = new EngineSpec(id: 1, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);
        var state = new PuzzleState();
        state.Placements.Add(1, new VehiclePlacement(1, new[] { seg.GetDirectedEdges()[0] }));

        var goal = new Goal(new[] { new SegmentGoal("S0", allowedVehicleIds: new[] { 0 }) }); // impossible for engine-only puzzle
        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { engine }, state, goal);

        var history = new InMemorySolutionHistory(new Solution(Array.Empty<SolutionMove>()));
        var snapshot = history.ToSnapshot();

        Assert.False(VerifiedPuzzle.TryCreate(puzzle, snapshot, out var verified, out var verification));
        Assert.Null(verified);
        Assert.True(verification.IsValid);
        Assert.False(verification.IsSolved);
    }

    [Fact]
    public void SolutionHistorySnapshot_DeepClone_PreventsExternalMutation() {
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

        var history = new InMemorySolutionHistory(new Solution(new SolutionMove[] { new MoveEngineSolutionMove(1, EngineMoveDirection.Forward) }));
        var snapshot = history.ToSnapshot();

        Assert.True(VerifiedPuzzle.TryCreate(puzzle, snapshot, out var verified, out var verification), verification.Message);

        // Mutate the original snapshot after verification.
        snapshot.History[0].Moves.Clear();

        // VerifiedPuzzle should still contain the original verified solution.
        Assert.NotNull(verified);
        Assert.Single(verified!.CurrentSolution.Moves);
    }

    [Fact]
    public void SolutionMoveSnapshot_UnknownKind_Throws() {
        var snapshot = new SolutionMoveSnapshot { Kind = "Nope" };
        Assert.Throws<InvalidOperationException>(() => snapshot.ToSolutionMove());
    }

    [Fact]
    public void SolutionMoveSnapshot_ToggleSwitch_RequiresFields() {
        Assert.Throws<InvalidOperationException>(() => new SolutionMoveSnapshot { Kind = "ToggleSwitch" }.ToSolutionMove());
        Assert.Throws<InvalidOperationException>(() => new SolutionMoveSnapshot { Kind = "ToggleCoupling", VehicleId = 0 }.ToSolutionMove());
        Assert.Throws<InvalidOperationException>(() => new SolutionMoveSnapshot { Kind = "MoveEngine", EngineId = 1 }.ToSolutionMove());
    }

    [Fact]
    public void SolutionMoveSnapshot_RoundTrip_SwitchMove_Works() {
        var move = new ToggleSwitchSolutionMove(1, 2, Direction.North);
        var snap = SolutionMoveSnapshot.FromSolutionMove(move);
        var restored = snap.ToSolutionMove();

        var restoredSwitch = Assert.IsType<ToggleSwitchSolutionMove>(restored);
        Assert.Equal(1, restoredSwitch.NodeX);
        Assert.Equal(2, restoredSwitch.NodeY);
        Assert.Equal(Direction.North, restoredSwitch.Heading);
    }

    private sealed record WeirdMove() : Move;

    private sealed record UnknownSolutionMove : SolutionMove {
        public override Move ToEngineMove() => new ToggleSwitchMove(new TrackState(new GridPoint(0, 0), Direction.North));
    }

    [Fact]
    public void SolutionMove_UnknownMappings_Throw() {
        Assert.Throws<ArgumentException>(() => SolutionMove.FromEngineMove(new WeirdMove()));
        Assert.Throws<ArgumentException>(() => SolutionMoveSnapshot.FromSolutionMove(new UnknownSolutionMove()));
    }
}
