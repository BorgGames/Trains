using Trains.Engine;
using Trains.Geometry;
using Trains.Track;

namespace Trains.Puzzle;

/// <summary>
/// A serializable move used in <see cref="Solution"/> and UI/solver integrations.
/// This mirrors <see cref="Trains.Engine.Move"/> but is intentionally data-oriented.
/// </summary>
public abstract record SolutionMove {
    public abstract Move ToEngineMove();

    public static SolutionMove FromEngineMove(Move move) =>
        move switch {
            ToggleSwitchMove m => new ToggleSwitchSolutionMove(m.SwitchKey.Node.X, m.SwitchKey.Node.Y, m.SwitchKey.Heading),
            ToggleCouplingMove m => new ToggleCouplingSolutionMove(m.VehicleId, m.End),
            RotateTurntableMove m => new RotateTurntableSolutionMove(m.TurntableId),
            MoveEngineMove m => new MoveEngineSolutionMove(m.EngineId, m.Direction),
            _ => throw new ArgumentException($"Unknown move type '{move.GetType().FullName}'.", nameof(move)),
        };
}

public sealed record ToggleSwitchSolutionMove(int NodeX, int NodeY, Direction Heading) : SolutionMove {
    public TrackState SwitchKey => new(new GridPoint(this.NodeX, this.NodeY), this.Heading);

    public override Move ToEngineMove() => new ToggleSwitchMove(this.SwitchKey);
}

public sealed record ToggleCouplingSolutionMove : SolutionMove {
    public ToggleCouplingSolutionMove(int vehicleId, VehicleEnd end) {
        if (vehicleId < 0)
            throw new ArgumentOutOfRangeException(nameof(vehicleId), vehicleId, "Vehicle id must be non-negative.");

        this.VehicleId = vehicleId;
        this.End = end;
    }

    public int VehicleId { get; }
    public VehicleEnd End { get; }

    public override Move ToEngineMove() => new ToggleCouplingMove(this.VehicleId, this.End);
}

public sealed record RotateTurntableSolutionMove : SolutionMove {
    public RotateTurntableSolutionMove(string turntableId) {
        if (string.IsNullOrWhiteSpace(turntableId))
            throw new ArgumentException("Turntable id must be non-empty.", nameof(turntableId));

        this.TurntableId = turntableId;
    }

    public string TurntableId { get; }

    public override Move ToEngineMove() => new RotateTurntableMove(this.TurntableId);
}

public sealed record MoveEngineSolutionMove : SolutionMove {
    public MoveEngineSolutionMove(int engineId, EngineMoveDirection direction) {
        if (engineId < 0)
            throw new ArgumentOutOfRangeException(nameof(engineId), engineId, "Engine id must be non-negative.");

        this.EngineId = engineId;
        this.Direction = direction;
    }

    public int EngineId { get; }
    public EngineMoveDirection Direction { get; }

    public override Move ToEngineMove() => new MoveEngineMove(this.EngineId, this.Direction);
}
