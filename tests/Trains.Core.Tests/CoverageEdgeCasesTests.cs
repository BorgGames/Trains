using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

namespace Trains.Core.Tests;

public sealed class CoverageEdgeCasesTests {
    [Fact]
    public void DirectionExtensions_InvalidEnum_Throws() {
        var bad = (Direction)123;
        Assert.Throws<ArgumentOutOfRangeException>(() => bad.Opposite());
        Assert.Throws<ArgumentOutOfRangeException>(() => bad.ToOffset());
    }

    [Fact]
    public void VehicleEndExtensions_InvalidEnum_Throws() {
        var bad = (VehicleEnd)123;
        Assert.Throws<ArgumentOutOfRangeException>(() => bad.Opposite());
    }

    [Fact]
    public void RollingStockSpec_InvalidArgs_Throw() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(id: -1, length: 1, weight: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(id: 0, length: 0, weight: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(id: 0, length: 1, weight: -1));

        Assert.Throws<ArgumentOutOfRangeException>(() => new EngineSpec(id: 0, length: 1, weight: 0, forwardPower: -1, backwardPower: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EngineSpec(id: 0, length: 1, weight: 0, forwardPower: 0, backwardPower: -1));
    }

    [Fact]
    public void ShuntingPuzzle_DuplicateRollingStockId_Throws() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var track = TrackLayout.Create(new[] { seg });

        var a = new CarSpec(0, length: 1, weight: 0);
        var b = new CarSpec(0, length: 1, weight: 0);

        Assert.Throws<ArgumentException>(() =>
            new ShuntingPuzzle(track, new RollingStockSpec[] { a, b }, new PuzzleState(), new Goal(Array.Empty<SegmentGoal>()))
        );
    }

    [Fact]
    public void Goal_UnknownSegment_ThrowsWhenEvaluating() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var puzzle = new ShuntingPuzzle(
            TrackLayout.Create(new[] { seg }),
            Array.Empty<RollingStockSpec>(),
            new PuzzleState(),
            new Goal(new[] { new SegmentGoal("NOPE", allowedVehicleIds: null) })
        );

        Assert.Throws<InvalidOperationException>(() => puzzle.IsSolved(new PuzzleState()));
    }

    [Fact]
    public void ShuntingEngine_UnknownMoveType_Fails() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var puzzle = new ShuntingPuzzle(
            TrackLayout.Create(new[] { seg }),
            Array.Empty<RollingStockSpec>(),
            new PuzzleState(),
            new Goal(Array.Empty<SegmentGoal>())
        );

        var result = ShuntingEngine.TryApplyMove(puzzle, new PuzzleState(), new DummyMove());
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.Unknown, result.Error);
    }

    private sealed record DummyMove : Move;

    [Fact]
    public void TrackLayout_TooManyStaticSwitchOptions_Throws() {
        var segments = new TrackSegment[] {
            new FakeMultiEdgeSegment("F0", new GridPoint(0, 0), entry: Direction.East),
        };

        Assert.Throws<ArgumentException>(() => TrackLayout.Create(segments));
    }

    private sealed class FakeMultiEdgeSegment : TrackSegment {
        private readonly Direction _entry;

        public FakeMultiEdgeSegment(string id, GridPoint from, Direction entry)
            : base(id, from, new GridPoint(from.X + 100, from.Y), distance: 1) {
            _entry = entry;
        }

        public override IReadOnlyList<DirectedTrackEdge> GetDirectedEdges() {
            // Four outgoing edges from the same (node, heading) to force a >3 switch.
            return new[] {
                new DirectedTrackEdge(this.Id + "A", this.A, new GridPoint(1, 0), _entry, Direction.East, 1),
                new DirectedTrackEdge(this.Id + "B", this.A, new GridPoint(0, 1), _entry, Direction.North, 1),
                new DirectedTrackEdge(this.Id + "C", this.A, new GridPoint(0, -1), _entry, Direction.South, 1),
                new DirectedTrackEdge(this.Id + "D", this.A, new GridPoint(-1, 0), _entry, Direction.West, 1),
            };
        }
    }

    [Fact]
    public void TrackLayout_DuplicateTurntableId_Throws() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var tt1 = new Turntable(
            id: "T",
            center: new GridPoint(0, 0),
            radius: 1,
            ports: new[] { new TurntablePort(new GridPoint(1, 0), Direction.East), new TurntablePort(new GridPoint(0, 1), Direction.North) },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );
        var tt2 = new Turntable(
            id: "T",
            center: new GridPoint(10, 10),
            radius: 1,
            ports: new[] { new TurntablePort(new GridPoint(11, 10), Direction.East), new TurntablePort(new GridPoint(10, 11), Direction.North) },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        Assert.Throws<ArgumentException>(() => TrackLayout.Create(new[] { seg }, new[] { tt1, tt2 }));
    }

    [Fact]
    public void TrackLayout_SegmentInsideTurntable_Throws() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var tt = new Turntable(
            id: "T",
            center: new GridPoint(0, 0),
            radius: 2,
            ports: new[] { new TurntablePort(new GridPoint(2, 0), Direction.East), new TurntablePort(new GridPoint(0, 2), Direction.North) },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        // Endpoint (1,0) lies strictly inside the square (-2..2), so TrackLayout should reject it.
        Assert.Throws<ArgumentException>(() => TrackLayout.Create(new[] { seg }, new[] { tt }));
    }

    [Fact]
    public void Turntable_GetDirectedEdgesForAlignment_InvalidIndex_Throws() {
        var tt = new Turntable(
            id: "T",
            center: new GridPoint(0, 0),
            radius: 1,
            ports: new[] { new TurntablePort(new GridPoint(1, 0), Direction.East), new TurntablePort(new GridPoint(0, 1), Direction.North) },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        Assert.Throws<ArgumentOutOfRangeException>(() => tt.GetDirectedEdgesForAlignment(-1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => tt.GetDirectedEdgesForAlignment(1).ToArray());
    }

    [Fact]
    public void Turntable_PortOutwardDirectionWrong_Throws() {
        Assert.Throws<ArgumentException>(() =>
            new Turntable(
                id: "T",
                center: new GridPoint(0, 0),
                radius: 1,
                ports: new[] {
                    new TurntablePort(new GridPoint(1, 0), Direction.North), // should be East
                    new TurntablePort(new GridPoint(0, 1), Direction.North),
                },
                alignments: new[] { new TurntableAlignment(0, 1) }
            )
        );
    }

    [Fact]
    public void Turntable_AlignmentSamePort_Throws() {
        Assert.Throws<ArgumentException>(() =>
            new Turntable(
                id: "T",
                center: new GridPoint(0, 0),
                radius: 1,
                ports: new[] {
                    new TurntablePort(new GridPoint(1, 0), Direction.East),
                    new TurntablePort(new GridPoint(0, 1), Direction.North),
                },
                alignments: new[] { new TurntableAlignment(0, 0) }
            )
        );
    }

    [Fact]
    public void TrackLayout_EdgeComparer_Order_Correct() {
        var comparer = TrackLayout.EdgeComparer.Instance;
        var a = new DirectedTrackEdge("A", new GridPoint(0, 0), new GridPoint(1, 0), Direction.East, Direction.East, Distance: 1);
        var b = new DirectedTrackEdge("B", new GridPoint(0, 0), new GridPoint(0, 1), Direction.East, Direction.North, Distance: 0);
        var c = new DirectedTrackEdge("C", new GridPoint(0, 0), new GridPoint(0, -1), Direction.East, Direction.South, Distance: 1);
        var d = new DirectedTrackEdge("D", new GridPoint(0, 0), new GridPoint(0, -1), Direction.East, Direction.South, Distance: 1);

        // distance branch
        Assert.True(comparer.Compare(a, b) < 0);
        // heading branch
        Assert.True(comparer.Compare(a, c) < 0);
        // segment id branch (same distance+heading, different id)
        Assert.True(comparer.Compare(c, d) < 0);
    }
}
