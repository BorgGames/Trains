using Trains.Puzzle;

namespace Trains.Engine;

public enum MoveError {
    Unknown = 0,
    InvalidState = 1,
    UnknownVehicle = 2,
    NotAnEngine = 3,
    InsufficientPower = 4,
    NoTrackAhead = 5,
    Collision = 6,
    InvalidSwitch = 7,
    InvalidCoupling = 8,
    NonLinearTrain = 9,
    LoopDetected = 10,
    InvalidTurntable = 11,
}

public readonly record struct MoveResult(bool IsSuccess, MoveError Error, string? Message, PuzzleState? State) {
    public static MoveResult Success(PuzzleState state) => new(true, MoveError.Unknown, null, state);
    public static MoveResult Fail(MoveError error, string message) => new(false, error, message, null);
}
