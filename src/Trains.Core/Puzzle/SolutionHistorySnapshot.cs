using Trains.Engine;
using Trains.Geometry;

namespace Trains.Puzzle;

public static class SolutionMoveKinds {
    public const string ToggleSwitch = "ToggleSwitch";
    public const string ToggleCoupling = "ToggleCoupling";
    public const string RotateTurntable = "RotateTurntable";
    public const string MoveEngine = "MoveEngine";
}

/// <summary>
/// Serializable snapshot of solution history.
/// </summary>
public sealed class SolutionHistorySnapshot {
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; }
    public int CurrentVersion { get; set; }
    public List<SolutionSnapshot> History { get; set; } = new();

    public SolutionHistorySnapshot DeepClone() {
        return new SolutionHistorySnapshot {
            SchemaVersion = this.SchemaVersion,
            CurrentVersion = this.CurrentVersion,
            History = this.History.Select(h => h.DeepClone()).ToList(),
        };
    }
}

public sealed class SolutionSnapshot {
    public List<SolutionMoveSnapshot> Moves { get; set; } = new();

    public Solution ToSolution() => new(this.Moves.Select(m => m.ToSolutionMove()).ToArray());

    public static SolutionSnapshot FromSolution(Solution solution) {
        if (solution is null)
            throw new ArgumentNullException(nameof(solution));

        return new SolutionSnapshot {
            Moves = solution.Moves.Select(SolutionMoveSnapshot.FromSolutionMove).ToList(),
        };
    }

    public SolutionSnapshot DeepClone() {
        return new SolutionSnapshot {
            Moves = this.Moves.Select(m => m.DeepClone()).ToList(),
        };
    }
}

public sealed class SolutionMoveSnapshot {
    public string Kind { get; set; } = "";

    public int? NodeX { get; set; }
    public int? NodeY { get; set; }
    public Direction? Heading { get; set; }

    public int? VehicleId { get; set; }
    public VehicleEnd? VehicleEnd { get; set; }

    public int? EngineId { get; set; }
    public EngineMoveDirection? EngineDirection { get; set; }

    public string? TurntableId { get; set; }

    public SolutionMove ToSolutionMove() {
        switch (Kind) {
            case SolutionMoveKinds.ToggleSwitch:
                return new ToggleSwitchSolutionMove(
                    NodeX ?? throw new InvalidOperationException("NodeX is required for ToggleSwitch."),
                    NodeY ?? throw new InvalidOperationException("NodeY is required for ToggleSwitch."),
                    Heading ?? throw new InvalidOperationException("Heading is required for ToggleSwitch.")
                );

            case SolutionMoveKinds.ToggleCoupling:
                return new ToggleCouplingSolutionMove(
                    VehicleId ?? throw new InvalidOperationException("VehicleId is required for ToggleCoupling."),
                    VehicleEnd ?? throw new InvalidOperationException("VehicleEnd is required for ToggleCoupling.")
                );

            case SolutionMoveKinds.RotateTurntable:
                return new RotateTurntableSolutionMove(
                    TurntableId ?? throw new InvalidOperationException("TurntableId is required for RotateTurntable.")
                );

            case SolutionMoveKinds.MoveEngine:
                return new MoveEngineSolutionMove(
                    EngineId ?? throw new InvalidOperationException("EngineId is required for MoveEngine."),
                    EngineDirection ?? throw new InvalidOperationException("EngineDirection is required for MoveEngine.")
                );

            default:
                throw new InvalidOperationException($"Unknown move kind '{Kind}'.");
        }
    }

    public static SolutionMoveSnapshot FromSolutionMove(SolutionMove move) =>
        move switch {
            ToggleSwitchSolutionMove m => new SolutionMoveSnapshot {
                Kind = SolutionMoveKinds.ToggleSwitch,
                NodeX = m.NodeX,
                NodeY = m.NodeY,
                Heading = m.Heading,
            },
            ToggleCouplingSolutionMove m => new SolutionMoveSnapshot {
                Kind = SolutionMoveKinds.ToggleCoupling,
                VehicleId = m.VehicleId,
                VehicleEnd = m.End,
            },
            RotateTurntableSolutionMove m => new SolutionMoveSnapshot {
                Kind = SolutionMoveKinds.RotateTurntable,
                TurntableId = m.TurntableId,
            },
            MoveEngineSolutionMove m => new SolutionMoveSnapshot {
                Kind = SolutionMoveKinds.MoveEngine,
                EngineId = m.EngineId,
                EngineDirection = m.Direction,
            },
            _ => throw new ArgumentException($"Unknown solution move type '{move.GetType().FullName}'.", nameof(move)),
        };

    public SolutionMoveSnapshot DeepClone() {
        // Structurally deep: all members are value types or immutable strings, so copying members is sufficient.
        return new SolutionMoveSnapshot {
            Kind = this.Kind,
            NodeX = this.NodeX,
            NodeY = this.NodeY,
            Heading = this.Heading,
            VehicleId = this.VehicleId,
            VehicleEnd = this.VehicleEnd,
            EngineId = this.EngineId,
            EngineDirection = this.EngineDirection,
            TurntableId = this.TurntableId,
        };
    }
}
