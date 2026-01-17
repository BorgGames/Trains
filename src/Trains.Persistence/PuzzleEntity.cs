using System;

namespace Trains.Persistence;

public sealed class PuzzleEntity {
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedByUserId { get; set; }

    public string PuzzleJson { get; set; } = "";

    public string SolutionHistoryJson { get; set; } = "";

    public string ThumbnailSvg { get; set; } = "";

    public bool IsPublished { get; set; }
}
