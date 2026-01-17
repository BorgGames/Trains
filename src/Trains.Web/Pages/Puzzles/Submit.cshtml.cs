using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trains.Web.Services;

namespace Trains.Web.Pages.Puzzles;

[Authorize]
public sealed class SubmitModel : PageModel {
    private readonly PuzzleSubmissionService _submission;

    public SubmitModel(PuzzleSubmissionService submission) {
        _submission = submission ?? throw new ArgumentNullException(nameof(submission));
    }

    [BindProperty]
    [Required]
    public string PuzzleJson { get; set; } = "";

    [BindProperty]
    [Required]
    public string SolutionHistoryJson { get; set; } = "";

    public string? ErrorMessage { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) {
        if (!ModelState.IsValid)
            return Page();

        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var result = await _submission.SubmitAsync(PuzzleJson, SolutionHistoryJson, userId, cancellationToken);
        if (!result.IsAccepted) {
            ErrorMessage = result.Message;
            return Page();
        }

        return Redirect($"/p/{result.PuzzleId}");
    }
}

