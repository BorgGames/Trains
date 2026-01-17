using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trains.Web.Models;
using Trains.Web.Services;

namespace Trains.Web.Pages;

public sealed class IndexModel : PageModel {
    private readonly PuzzleCatalog _catalog;

    public IndexModel(PuzzleCatalog catalog) {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyList<PuzzleSummary> Puzzles { get; private set; } = Array.Empty<PuzzleSummary>();

    public string Filter { get; private set; } = "all";

    public async Task OnGetAsync(string? filter, CancellationToken cancellationToken) {
        Filter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Puzzles = await _catalog.ListPublishedAsync(userId, Filter, cancellationToken);
    }
}
