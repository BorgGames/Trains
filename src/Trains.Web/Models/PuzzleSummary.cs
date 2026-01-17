using System;

namespace Trains.Web.Models;

public sealed record PuzzleSummary(Guid Id, DateTimeOffset CreatedAt, string ThumbnailSvg, bool IsSolved);
