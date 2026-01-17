using Trains.Puzzle;
using Trains.Track;

namespace Trains.Engine;

public abstract record Move;

public sealed record ToggleSwitchMove(TrackState SwitchKey) : Move;

public sealed record ToggleCouplingMove(int VehicleId, VehicleEnd End) : Move;

public sealed record RotateTurntableMove(string TurntableId) : Move;

public enum EngineMoveDirection {
    Forward = 0,
    Backward = 1,
}

public sealed record MoveEngineMove(int EngineId, EngineMoveDirection Direction) : Move;
