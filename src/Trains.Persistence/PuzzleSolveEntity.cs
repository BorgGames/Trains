using System;

namespace Trains.Persistence;

public sealed class PuzzleSolveEntity {
    public Guid PuzzleId { get; set; }
    public string UserId { get; set; } = "";

    public DateTimeOffset? SolvedAt { get; set; }
    public int? BestMoveCount { get; set; }
    public DateTimeOffset LastPlayedAt { get; set; }
}
