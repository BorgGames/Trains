using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

namespace Trains.Puzzle.Serialization;

/// <summary>
/// Serializable snapshot of a shunting puzzle definition (track + rolling stock + initial state + goal),
/// optionally carrying a verified solution history used for publishing/curation.
/// </summary>
public sealed class PuzzleSnapshot {
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public TrackLayoutSnapshot Track { get; set; } = new();

    public List<RollingStockSpecSnapshot> RollingStock { get; set; } = new();

    public PuzzleStateSnapshot InitialState { get; set; } = new();

    public GoalSnapshot Goal { get; set; } = new();

    public SolutionHistorySnapshot? VerifiedSolutionHistory { get; set; }

    public ShuntingPuzzle ToPuzzle() {
        if (this.SchemaVersion != CurrentSchemaVersion)
            throw new NotSupportedException($"Unsupported puzzle schema version {this.SchemaVersion}.");

        var track = this.Track.ToTrackLayout();
        var rollingStock = this.RollingStock.Select(s => s.ToSpec()).ToArray();
        var initialState = this.InitialState.ToPuzzleState();
        var goal = this.Goal.ToGoal();

        return new ShuntingPuzzle(track, rollingStock, initialState, goal);
    }

    public static PuzzleSnapshot FromPuzzle(ShuntingPuzzle puzzle, SolutionHistorySnapshot? verifiedSolutionHistory = null) {
        if (puzzle is null)
            throw new ArgumentNullException(nameof(puzzle));

        var snapshot = new PuzzleSnapshot {
            SchemaVersion = CurrentSchemaVersion,
            Track = TrackLayoutSnapshot.FromTrackLayout(puzzle.Track),
            RollingStock = puzzle.RollingStock.Values.Select(RollingStockSpecSnapshot.FromSpec).ToList(),
            InitialState = PuzzleStateSnapshot.FromPuzzleState(puzzle.InitialState),
            Goal = GoalSnapshot.FromGoal(puzzle.Goal),
            VerifiedSolutionHistory = verifiedSolutionHistory?.DeepClone(),
        };

        return snapshot;
    }
}

public sealed class RollingStockSpecSnapshot {
    public int Id { get; set; }
    public int Length { get; set; }
    public int Weight { get; set; }

    public bool IsEngine { get; set; }
    public int? ForwardPower { get; set; }
    public int? BackwardPower { get; set; }

    public RollingStockSpec ToSpec() {
        if (this.Id < 0)
            throw new ArgumentOutOfRangeException(nameof(this.Id), this.Id, "Id must be non-negative.");
        if (this.Length <= 0)
            throw new ArgumentOutOfRangeException(nameof(this.Length), this.Length, "Length must be positive.");
        if (this.Weight < 0)
            throw new ArgumentOutOfRangeException(nameof(this.Weight), this.Weight, "Weight must be non-negative.");

        if (!this.IsEngine)
            return new CarSpec(this.Id, this.Length, this.Weight);

        return new EngineSpec(
            id: this.Id,
            length: this.Length,
            weight: this.Weight,
            forwardPower: this.ForwardPower ?? 0,
            backwardPower: this.BackwardPower ?? 0
        );
    }

    public static RollingStockSpecSnapshot FromSpec(RollingStockSpec spec) {
        if (spec is null)
            throw new ArgumentNullException(nameof(spec));

        if (spec.IsEngine) {
            return new RollingStockSpecSnapshot {
                Id = spec.Id,
                Length = spec.Length,
                Weight = spec.Weight,
                IsEngine = true,
                ForwardPower = spec.ForwardPower,
                BackwardPower = spec.BackwardPower,
            };
        }

        return new RollingStockSpecSnapshot {
            Id = spec.Id,
            Length = spec.Length,
            Weight = spec.Weight,
            IsEngine = false,
            ForwardPower = null,
            BackwardPower = null,
        };
    }
}

public sealed class GoalSnapshot {
    public List<SegmentGoalSnapshot> SegmentGoals { get; set; } = new();

    public Goal ToGoal() => new(this.SegmentGoals.Select(g => g.ToSegmentGoal()).ToArray());

    public static GoalSnapshot FromGoal(Goal goal) {
        if (goal is null)
            throw new ArgumentNullException(nameof(goal));

        return new GoalSnapshot {
            SegmentGoals = goal.SegmentGoals.Select(SegmentGoalSnapshot.FromSegmentGoal).ToList(),
        };
    }
}

public sealed class SegmentGoalSnapshot {
    public string SegmentId { get; set; } = "";

    /// <summary>
    /// Null means: any vehicle allowed. Empty means: must be empty.
    /// Otherwise: must be occupied by a vehicle in the set.
    /// </summary>
    public List<int>? AllowedVehicleIds { get; set; }

    public SegmentGoal ToSegmentGoal() {
        if (string.IsNullOrWhiteSpace(this.SegmentId))
            throw new ArgumentException("SegmentId must be non-empty.", nameof(this.SegmentId));

        return new SegmentGoal(this.SegmentId, this.AllowedVehicleIds);
    }

    public static SegmentGoalSnapshot FromSegmentGoal(SegmentGoal goal) {
        if (goal is null)
            throw new ArgumentNullException(nameof(goal));

        return new SegmentGoalSnapshot {
            SegmentId = goal.SegmentId,
            AllowedVehicleIds = goal.AllowedVehicleIds is null ? null : goal.AllowedVehicleIds.ToList(),
        };
    }
}

public sealed class TrackLayoutSnapshot {
    public List<SegmentSnapshot> Segments { get; set; } = new();
    public List<TurntableSnapshot> Turntables { get; set; } = new();

    public TrackLayout ToTrackLayout() {
        var segments = this.Segments.Select(s => s.ToSegment()).ToArray();
        var turntables = this.Turntables.Select(t => t.ToTurntable()).ToArray();
        return TrackLayout.Create(segments, turntables);
    }

    public static TrackLayoutSnapshot FromTrackLayout(TrackLayout track) {
        if (track is null)
            throw new ArgumentNullException(nameof(track));

        return new TrackLayoutSnapshot {
            Segments = track.Segments.Values.Select(SegmentSnapshot.FromSegment).ToList(),
            Turntables = track.Turntables.Select(TurntableSnapshot.FromTurntable).ToList(),
        };
    }
}

public sealed class SegmentSnapshot {
    public string Kind { get; set; } = "";
    public string Id { get; set; } = "";
    public GridPoint A { get; set; }
    public GridPoint B { get; set; }
    public CurveBias? Bias { get; set; }

    public TrackSegment ToSegment() {
        if (string.Equals(this.Kind, SegmentKinds.Straight, StringComparison.Ordinal))
            return new StraightSegment(this.Id, this.A, this.B);
        if (string.Equals(this.Kind, SegmentKinds.Curve, StringComparison.Ordinal))
            return new CurvedSegment(this.Id, this.A, this.B, this.Bias ?? throw new InvalidOperationException("Curve Bias is required."));

        throw new InvalidOperationException($"Unknown segment kind '{this.Kind}'.");
    }

    public static SegmentSnapshot FromSegment(TrackSegment segment) {
        if (segment is null)
            throw new ArgumentNullException(nameof(segment));

        return segment switch {
            StraightSegment s => new SegmentSnapshot { Kind = SegmentKinds.Straight, Id = s.Id, A = s.A, B = s.B, Bias = null },
            CurvedSegment c => new SegmentSnapshot { Kind = SegmentKinds.Curve, Id = c.Id, A = c.A, B = c.B, Bias = c.Bias },
            _ => throw new ArgumentException($"Unknown segment type '{segment.GetType().FullName}'.", nameof(segment)),
        };
    }
}

public static class SegmentKinds {
    public const string Straight = "Straight";
    public const string Curve = "Curve";
}

public sealed class TurntableSnapshot {
    public string Id { get; set; } = "";
    public GridPoint Center { get; set; }
    public int Radius { get; set; }
    public List<TurntablePortSnapshot> Ports { get; set; } = new();
    public List<TurntableAlignmentSnapshot> Alignments { get; set; } = new();

    public Turntable ToTurntable() {
        return new Turntable(
            id: this.Id,
            center: this.Center,
            radius: this.Radius,
            ports: this.Ports.Select(p => new TurntablePort(p.Point, p.OutboundDirection)).ToArray(),
            alignments: this.Alignments.Select(a => new TurntableAlignment(a.PortAIndex, a.PortBIndex)).ToArray()
        );
    }

    public static TurntableSnapshot FromTurntable(Turntable t) {
        return new TurntableSnapshot {
            Id = t.Id,
            Center = t.Center,
            Radius = t.Radius,
            Ports = t.Ports.Select(p => new TurntablePortSnapshot { Point = p.Point, OutboundDirection = p.OutboundDirection }).ToList(),
            Alignments = t.Alignments.Select(a => new TurntableAlignmentSnapshot { PortAIndex = a.PortAIndex, PortBIndex = a.PortBIndex }).ToList(),
        };
    }
}

public sealed class TurntablePortSnapshot {
    public GridPoint Point { get; set; }
    public Direction OutboundDirection { get; set; }
}

public sealed class TurntableAlignmentSnapshot {
    public int PortAIndex { get; set; }
    public int PortBIndex { get; set; }
}

public sealed class PuzzleStateSnapshot {
    public List<SwitchStateSnapshot> SwitchStates { get; set; } = new();
    public Dictionary<string, int> TurntableStates { get; set; } = new(StringComparer.Ordinal);
    public List<VehiclePlacementSnapshot> Placements { get; set; } = new();
    public List<VehicleCouplingsSnapshot> Couplings { get; set; } = new();

    public PuzzleState ToPuzzleState() {
        var state = new PuzzleState();

        foreach (var sw in this.SwitchStates) {
            var ts = sw.State.ToTrackState();
            state.SwitchStates[ts] = sw.SelectedOptionIndex;
        }

        foreach (var kvp in this.TurntableStates)
            state.TurntableStates[kvp.Key] = kvp.Value;

        foreach (var p in this.Placements)
            state.Placements[p.VehicleId] = p.ToPlacement();

        foreach (var c in this.Couplings)
            state.Couplings[c.VehicleId] = c.ToCouplings();

        return state;
    }

    public static PuzzleStateSnapshot FromPuzzleState(PuzzleState state) {
        if (state is null)
            throw new ArgumentNullException(nameof(state));

        return new PuzzleStateSnapshot {
            SwitchStates = state.SwitchStates.Select(kvp => new SwitchStateSnapshot {
                State = TrackStateSnapshot.FromTrackState(kvp.Key),
                SelectedOptionIndex = kvp.Value,
            }).ToList(),
            TurntableStates = new Dictionary<string, int>(state.TurntableStates, StringComparer.Ordinal),
            Placements = state.Placements.Values.Select(VehiclePlacementSnapshot.FromPlacement).ToList(),
            Couplings = state.Couplings.Select(kvp => VehicleCouplingsSnapshot.FromCouplings(kvp.Key, kvp.Value)).ToList(),
        };
    }
}

public sealed class SwitchStateSnapshot {
    public TrackStateSnapshot State { get; set; } = new();
    public int SelectedOptionIndex { get; set; }
}

public sealed class TrackStateSnapshot {
    public int NodeX { get; set; }
    public int NodeY { get; set; }
    public Direction Heading { get; set; }

    public TrackState ToTrackState() => new(new GridPoint(this.NodeX, this.NodeY), this.Heading);

    public static TrackStateSnapshot FromTrackState(TrackState state) {
        return new TrackStateSnapshot { NodeX = state.Node.X, NodeY = state.Node.Y, Heading = state.Heading };
    }
}

public sealed class VehiclePlacementSnapshot {
    public int VehicleId { get; set; }
    public List<DirectedTrackEdgeSnapshot> Edges { get; set; } = new();

    public VehiclePlacement ToPlacement() => new(this.VehicleId, this.Edges.Select(e => e.ToEdge()).ToArray());

    public static VehiclePlacementSnapshot FromPlacement(VehiclePlacement placement) {
        if (placement is null)
            throw new ArgumentNullException(nameof(placement));

        return new VehiclePlacementSnapshot {
            VehicleId = placement.VehicleId,
            Edges = placement.Edges.Select(DirectedTrackEdgeSnapshot.FromEdge).ToList(),
        };
    }
}

public sealed class DirectedTrackEdgeSnapshot {
    public string SegmentId { get; set; } = "";
    public GridPoint FromNode { get; set; }
    public GridPoint ToNode { get; set; }
    public Direction EntryHeading { get; set; }
    public Direction ExitHeading { get; set; }

    public DirectedTrackEdge ToEdge() {
        return new DirectedTrackEdge(this.SegmentId, this.FromNode, this.ToNode, this.EntryHeading, this.ExitHeading);
    }

    public static DirectedTrackEdgeSnapshot FromEdge(DirectedTrackEdge edge) {
        return new DirectedTrackEdgeSnapshot {
            SegmentId = edge.SegmentId,
            FromNode = edge.FromNode,
            ToNode = edge.ToNode,
            EntryHeading = edge.EntryHeading,
            ExitHeading = edge.ExitHeading,
        };
    }
}

public sealed class VehicleCouplingsSnapshot {
    public int VehicleId { get; set; }
    public VehicleCouplingSnapshot? Back { get; set; }
    public VehicleCouplingSnapshot? Front { get; set; }

    public VehicleCouplings ToCouplings() {
        return new VehicleCouplings {
            Back = this.Back?.ToCoupling(),
            Front = this.Front?.ToCoupling(),
        };
    }

    public static VehicleCouplingsSnapshot FromCouplings(int vehicleId, VehicleCouplings couplings) {
        if (couplings is null)
            throw new ArgumentNullException(nameof(couplings));

        return new VehicleCouplingsSnapshot {
            VehicleId = vehicleId,
            Back = couplings.Back.HasValue ? VehicleCouplingSnapshot.FromCoupling(couplings.Back.Value) : null,
            Front = couplings.Front.HasValue ? VehicleCouplingSnapshot.FromCoupling(couplings.Front.Value) : null,
        };
    }
}

public sealed class VehicleCouplingSnapshot {
    public int OtherVehicleId { get; set; }
    public VehicleEnd OtherEnd { get; set; }

    public VehicleCoupling ToCoupling() => new(this.OtherVehicleId, this.OtherEnd);

    public static VehicleCouplingSnapshot FromCoupling(VehicleCoupling coupling) {
        return new VehicleCouplingSnapshot {
            OtherVehicleId = coupling.OtherVehicleId,
            OtherEnd = coupling.OtherEnd,
        };
    }
}
