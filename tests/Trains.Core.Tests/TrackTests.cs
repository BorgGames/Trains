using Trains.Geometry;
using Trains.Track;
using Trains.Engine;
using Trains.Puzzle;

namespace Trains.Core.Tests;

public sealed class TrackTests {
    [Fact]
    public void StraightSegment_InvalidEndpoints_Throws() {
        Assert.Throws<ArgumentException>(() => new StraightSegment("S", new GridPoint(0, 0), new GridPoint(0, 2)));
        Assert.Throws<ArgumentException>(() => new StraightSegment("S", new GridPoint(0, 0), new GridPoint(1, 1)));
    }

    [Fact]
    public void CurvedSegment_Bias_DisambiguatesTurn() {
        var a = new GridPoint(0, 0);
        var b = new GridPoint(1, 1);

        var xFirst = new CurvedSegment("C1", a, b, CurveBias.XFirst).GetDirectedEdges()[0];
        Assert.Equal(Direction.East, xFirst.EntryHeading);
        Assert.Equal(Direction.North, xFirst.ExitHeading);

        var yFirst = new CurvedSegment("C2", a, b, CurveBias.YFirst).GetDirectedEdges()[0];
        Assert.Equal(Direction.North, yFirst.EntryHeading);
        Assert.Equal(Direction.East, yFirst.ExitHeading);
    }

    [Fact]
    public void TrackLayout_DuplicateSegmentId_Throws() {
        var segments = new TrackSegment[] {
            new StraightSegment("S", new GridPoint(0, 0), new GridPoint(1, 0)),
            new StraightSegment("S", new GridPoint(1, 0), new GridPoint(2, 0)),
        };

        Assert.Throws<ArgumentException>(() => TrackLayout.Create(segments));
    }

    [Fact]
    public void Turntable_ValidatesPortsAndAlignments() {
        var tt = new Turntable(
            id: "T",
            center: new GridPoint(0, 0),
            radius: 1,
            ports: new[] {
                new TurntablePort(new GridPoint(1, 0), Direction.East),
                new TurntablePort(new GridPoint(-1, 0), Direction.West),
            },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        var edges = tt.GetDirectedEdgesForAlignment(0).ToArray();
        Assert.Equal(4, edges.Length); // 2*Radius segments, each yields 2 directed edges
        Assert.Contains(edges, e => e.SegmentId == "Turntable:T:0");
        Assert.Contains(edges, e => e.SegmentId == "Turntable:T:1");
    }

    [Fact]
    public void Turntable_VerticalAlignment_Generates2RUnitSegments() {
        var tt = new Turntable(
            id: "T",
            center: new GridPoint(0, 0),
            radius: 2,
            ports: new[] {
                new TurntablePort(new GridPoint(0, 2), Direction.North),
                new TurntablePort(new GridPoint(0, -2), Direction.South),
            },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        var edges = tt.GetDirectedEdgesForAlignment(0).ToArray();
        Assert.Equal(8, edges.Length); // 2*Radius segments, each yields 2 directed edges
        Assert.Contains(edges, e => e.SegmentId == "Turntable:T:0");
        Assert.Contains(edges, e => e.SegmentId == "Turntable:T:3");
    }

    [Fact]
    public void Turntable_AlignmentNotOnCenterLine_Throws() {
        Assert.Throws<ArgumentException>(() =>
            new Turntable(
                id: "T",
                center: new GridPoint(0, 0),
                radius: 2,
                ports: new[] {
                    new TurntablePort(new GridPoint(2, 1), Direction.East),
                    new TurntablePort(new GridPoint(-2, 1), Direction.West),
                },
                alignments: new[] { new TurntableAlignment(0, 1) }
            )
        );
    }

    [Fact]
    public void Turntable_Length4_CanFitTwoLength2Trains() {
        var tt = new Turntable(
            id: "T",
            center: new GridPoint(0, 0),
            radius: 2, // bridge length 4
            ports: new[] {
                new TurntablePort(new GridPoint(-2, 0), Direction.West),
                new TurntablePort(new GridPoint(2, 0), Direction.East),
            },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        var track = TrackLayout.Create(Array.Empty<TrackSegment>(), new[] { tt });

        var edges = tt.GetDirectedEdgesForAlignment(0).ToArray();
        DirectedTrackEdge GetEdge(string segmentId, GridPoint from, GridPoint to) =>
            edges.Single(e => e.SegmentId == segmentId && e.FromNode == from && e.ToNode == to);

        // Train A occupies the left half: [-2..-1] and [-1..0]
        var aEdges = new[] {
            GetEdge("Turntable:T:0", new GridPoint(-2, 0), new GridPoint(-1, 0)),
            GetEdge("Turntable:T:1", new GridPoint(-1, 0), new GridPoint(0, 0)),
        };

        // Train B occupies the right half: [0..1] and [1..2]
        var bEdges = new[] {
            GetEdge("Turntable:T:2", new GridPoint(0, 0), new GridPoint(1, 0)),
            GetEdge("Turntable:T:3", new GridPoint(1, 0), new GridPoint(2, 0)),
        };

        var trainA = new CarSpec(id: 0, length: 2, weight: 0);
        var trainB = new CarSpec(id: 1, length: 2, weight: 0);

        var state = new PuzzleState();
        state.TurntableStates["T"] = 0;
        state.Placements[0] = new VehiclePlacement(0, aEdges);
        state.Placements[1] = new VehiclePlacement(1, bEdges);

        var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { trainA, trainB }, state, new Goal(Array.Empty<SegmentGoal>()));

        // State should be valid (i.e. the engine should get past validation and fail only because the "engine" is a car).
        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(trainA.Id, EngineMoveDirection.Forward));
        Assert.False(result.IsSuccess);
        Assert.Equal(MoveError.NotAnEngine, result.Error);
    }

    [Fact]
    public void Turntable_InvalidPort_Throws() {
        Assert.Throws<ArgumentException>(() =>
            new Turntable(
                id: "T",
                center: new GridPoint(0, 0),
                radius: 1,
                ports: new[] { new TurntablePort(new GridPoint(0, 0), Direction.North), new TurntablePort(new GridPoint(1, 0), Direction.East) },
                alignments: new[] { new TurntableAlignment(0, 1) }
            )
        );
    }

    [Fact]
    public void Turntable_InvalidAlignment_Throws() {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Turntable(
                id: "T",
                center: new GridPoint(0, 0),
                radius: 1,
                ports: new[] { new TurntablePort(new GridPoint(1, 0), Direction.East), new TurntablePort(new GridPoint(-1, 0), Direction.West) },
                alignments: new[] { new TurntableAlignment(0, 9) }
            )
        );
    }

    [Fact]
    public void Turntable_ContainsStrictly_Works() {
        var tt = new Turntable(
            id: "T",
            center: new GridPoint(0, 0),
            radius: 2,
            ports: new[] {
                new TurntablePort(new GridPoint(2, 0), Direction.East),
                new TurntablePort(new GridPoint(-2, 0), Direction.West),
            },
            alignments: new[] { new TurntableAlignment(0, 1) }
        );

        Assert.True(tt.ContainsStrictly(new GridPoint(1, 0)));
        Assert.False(tt.ContainsStrictly(new GridPoint(2, 0))); // border
        Assert.False(tt.ContainsStrictly(new GridPoint(3, 0))); // outside
    }
}
