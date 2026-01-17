using System.Runtime.Serialization;
using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Puzzle.Serialization;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class PuzzleSnapshotTests {
    [Fact]
    public void PuzzleSnapshot_RoundTrips_Json_AndToPuzzle_Works() {
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

        var snapshot = PuzzleSnapshot.FromPuzzle(puzzle);
        var json = PuzzleJson.Serialize(snapshot);
        var restoredSnapshot = PuzzleJson.Deserialize(json);

        var restoredPuzzle = restoredSnapshot.ToPuzzle();

        var solution = new Solution(new SolutionMove[] { new MoveEngineSolutionMove(1, EngineMoveDirection.Forward) });
        var verification = SolutionVerifier.Verify(restoredPuzzle, solution);
        Assert.True(verification.IsValid, verification.Message);
        Assert.True(verification.IsSolved, verification.Message);
    }

    [Fact]
    public void PuzzleSnapshot_SchemaVersionMismatch_Throws() {
        var snapshot = new PuzzleSnapshot {
            SchemaVersion = 999,
            Track = new TrackLayoutSnapshot(),
            RollingStock = new List<RollingStockSpecSnapshot>(),
            InitialState = new PuzzleStateSnapshot(),
            Goal = new GoalSnapshot(),
        };

        Assert.Throws<NotSupportedException>(() => snapshot.ToPuzzle());
    }

    [Fact]
    public void SegmentSnapshot_UnknownKind_Throws() {
        var s = new SegmentSnapshot { Kind = "Nope", Id = "X", A = new GridPoint(0, 0), B = new GridPoint(1, 0) };
        Assert.Throws<InvalidOperationException>(() => s.ToSegment());
    }

    [Fact]
    public void SegmentSnapshot_CurveMissingBias_Throws() {
        var s = new SegmentSnapshot { Kind = SegmentKinds.Curve, Id = "C", A = new GridPoint(0, 0), B = new GridPoint(1, 1), Bias = null };
        Assert.Throws<InvalidOperationException>(() => s.ToSegment());
    }

    [Fact]
    public void RollingStockSpecSnapshot_FromSpec_AndToSpec_CoversCarAndEngine() {
        var engine = new EngineSpec(id: 1, length: 1, weight: 0, forwardPower: 2, backwardPower: 0);
        var car = new CarSpec(id: 2, length: 2, weight: 3);

        var engineSnap = RollingStockSpecSnapshot.FromSpec(engine);
        Assert.True(engineSnap.IsEngine);
        Assert.Equal(2, engineSnap.ForwardPower);
        Assert.Equal(0, engineSnap.BackwardPower);

        var restoredEngine = engineSnap.ToSpec();
        Assert.True(restoredEngine.IsEngine);
        Assert.Equal(2, restoredEngine.ForwardPower);

        var carSnap = RollingStockSpecSnapshot.FromSpec(car);
        Assert.False(carSnap.IsEngine);
        Assert.Null(carSnap.ForwardPower);
        Assert.Null(carSnap.BackwardPower);

        var restoredCar = carSnap.ToSpec();
        Assert.False(restoredCar.IsEngine);
        Assert.Equal(2, restoredCar.Length);
        Assert.Equal(3, restoredCar.Weight);
    }

    [Fact]
    public void GoalSnapshot_NullVsEmptyAllowedVehicleIds_BehaveDifferently() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var track = TrackLayout.Create(new[] { seg });

        var car = new CarSpec(0, length: 1, weight: 0);

        var occupiedRequired = new GoalSnapshot {
            SegmentGoals = new List<SegmentGoalSnapshot> {
                new SegmentGoalSnapshot { SegmentId = "S0", AllowedVehicleIds = null },
            },
        }.ToGoal();

        var mustBeEmpty = new GoalSnapshot {
            SegmentGoals = new List<SegmentGoalSnapshot> {
                new SegmentGoalSnapshot { SegmentId = "S0", AllowedVehicleIds = new List<int>() },
            },
        }.ToGoal();

        var emptyState = new PuzzleState();
        Assert.False(new ShuntingPuzzle(track, new RollingStockSpec[] { car }, emptyState, occupiedRequired).IsSolved(emptyState));
        Assert.True(new ShuntingPuzzle(track, new RollingStockSpec[] { car }, emptyState, mustBeEmpty).IsSolved(emptyState));
    }

    [Fact]
    public void PuzzleStateSnapshot_SwitchStates_RoundTrip_Works() {
        var state = new PuzzleState();
        state.SwitchStates[new TrackState(new GridPoint(1, 2), Direction.North)] = 1;

        var snap = PuzzleStateSnapshot.FromPuzzleState(state);
        var restored = snap.ToPuzzleState();

        Assert.Equal(1, restored.SwitchStates[new TrackState(new GridPoint(1, 2), Direction.North)]);
    }

    [Fact]
    public void PuzzleJson_NullArguments_Throw() {
        Assert.Throws<ArgumentNullException>(() => PuzzleJson.Serialize(null!));
        Assert.Throws<ArgumentNullException>(() => PuzzleJson.Deserialize(null!));
    }

    [Fact]
    public void PuzzleJson_Deserialize_InvalidJson_Throws() {
        Assert.Throws<SerializationException>(() => PuzzleJson.Deserialize("{not json"));
    }

    [Fact]
    public void TrackLayoutSnapshot_WithTurntable_RoundTrip_Works() {
        var tt = new Turntable(
            id: "T",
            center: new GridPoint(0, 0),
            radius: 2,
            ports: new[] {
                new TurntablePort(new GridPoint(-2, 0), Direction.West),
                new TurntablePort(new GridPoint(2, 0), Direction.East),
            },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        var track = TrackLayout.Create(Array.Empty<TrackSegment>(), new[] { tt });
        var snap = TrackLayoutSnapshot.FromTrackLayout(track);
        var restored = snap.ToTrackLayout();

        Assert.True(restored.IsKnownSegment("Turntable:T:0"));
        Assert.True(restored.IsKnownSegment("Turntable:T:3"));
    }
}

