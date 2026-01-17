using System;

namespace Trains.Persistence;

public sealed class PuzzleVoteEntity {
    public Guid PuzzleId { get; set; }
    public string UserId { get; set; } = "";

    public short Difficulty { get; set; }
    public short Score { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
