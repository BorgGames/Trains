using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Puzzle.Serialization;
using Trains.Track;
using Trains.Web.Services;

namespace Trains.Web.Pages.Puzzles;

public sealed class PlayModel : PageModel {
    private readonly PuzzleCatalog _catalog;
    private readonly PuzzleSvgRenderer _svg;
    private readonly PuzzleProgressStore _progress;
    private readonly PuzzleVotingStore _votes;
    private readonly PlayPayloadProtector _payloadProtector;

    public PlayModel(PuzzleCatalog catalog, PuzzleSvgRenderer svg, PuzzleProgressStore progress, PuzzleVotingStore votes, PlayPayloadProtector payloadProtector) {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _svg = svg ?? throw new ArgumentNullException(nameof(svg));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _votes = votes ?? throw new ArgumentNullException(nameof(votes));
        _payloadProtector = payloadProtector ?? throw new ArgumentNullException(nameof(payloadProtector));
    }

    [FromRoute]
    public Guid Id { get; set; }

    public string Svg { get; private set; } = "";
    public string Payload { get; private set; } = "";
    public int MoveCount { get; private set; }
    public bool IsSolved { get; private set; }
    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<int> VehicleIds { get; private set; } = Array.Empty<int>();
    public IReadOnlyList<int> EngineIds { get; private set; } = Array.Empty<int>();
    public IReadOnlyList<(string Value, string Label)> SwitchKeys { get; private set; } = Array.Empty<(string, string)>();
    public IReadOnlyList<string> TurntableIds { get; private set; } = Array.Empty<string>();
    public short? MyDifficulty { get; private set; }
    public short? MyScore { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) {
        var loaded = await LoadPuzzleAsync(this.Id, cancellationToken);
        if (loaded is null)
            return NotFound();

        var (snapshot, puzzle) = loaded.Value;
        var state = snapshot.InitialState.ToPuzzleState();

        PopulateLists(snapshot, puzzle);
        await LoadUserExtrasAsync(cancellationToken);
        SetViewState(snapshot, puzzle, state, moveCount: 0, errorMessage: null);
        return Page();
    }

    public async Task<IActionResult> OnPostResetAsync(CancellationToken cancellationToken) {
        var loaded = await LoadPuzzleAsync(this.Id, cancellationToken);
        if (loaded is null)
            return NotFound();

        var (snapshot, puzzle) = loaded.Value;
        var state = snapshot.InitialState.ToPuzzleState();

        PopulateLists(snapshot, puzzle);
        await LoadUserExtrasAsync(cancellationToken);
        SetViewState(snapshot, puzzle, state, moveCount: 0, errorMessage: null);
        return Page();
    }

    public async Task<IActionResult> OnPostMoveEngineAsync(
        [FromForm] string? payload,
        [FromForm] int engineId,
        [FromForm] EngineMoveDirection direction,
        CancellationToken cancellationToken
    ) {
        var loaded = await LoadPuzzleAsync(this.Id, cancellationToken);
        if (loaded is null)
            return NotFound();

        var (snapshot, puzzle) = loaded.Value;
        var (state, moveCount, payloadOk) = DeserializePayloadOrFallback(snapshot, payload);

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engineId, direction));
        if (result.IsSuccess) {
            state = result.State!;
            moveCount++;
            await RecordProgressAsync(puzzle, state, moveCount, cancellationToken);
        }

        PopulateLists(snapshot, puzzle);
        await LoadUserExtrasAsync(cancellationToken);
        string? msg = result.IsSuccess ? null : result.Message;
        if (msg is null && !payloadOk)
            msg = "State expired; reset to initial state.";
        SetViewState(snapshot, puzzle, state, moveCount, errorMessage: msg);
        return Page();
    }

    public async Task<IActionResult> OnPostToggleCouplingAsync(
        [FromForm] string? payload,
        [FromForm] int vehicleId,
        [FromForm] VehicleEnd end,
        CancellationToken cancellationToken
    ) {
        var loaded = await LoadPuzzleAsync(this.Id, cancellationToken);
        if (loaded is null)
            return NotFound();

        var (snapshot, puzzle) = loaded.Value;
        var (state, moveCount, payloadOk) = DeserializePayloadOrFallback(snapshot, payload);

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new ToggleCouplingMove(vehicleId, end));
        if (result.IsSuccess) {
            state = result.State!;
            moveCount++;
            await RecordProgressAsync(puzzle, state, moveCount, cancellationToken);
        }

        PopulateLists(snapshot, puzzle);
        await LoadUserExtrasAsync(cancellationToken);
        string? msg = result.IsSuccess ? null : result.Message;
        if (msg is null && !payloadOk)
            msg = "State expired; reset to initial state.";
        SetViewState(snapshot, puzzle, state, moveCount, errorMessage: msg);
        return Page();
    }

    public async Task<IActionResult> OnPostToggleSwitchAsync(
        [FromForm] string? payload,
        [FromForm] string? switchKey,
        CancellationToken cancellationToken
    ) {
        var loaded = await LoadPuzzleAsync(this.Id, cancellationToken);
        if (loaded is null)
            return NotFound();

        var (snapshot, puzzle) = loaded.Value;
        var (state, moveCount, payloadOk) = DeserializePayloadOrFallback(snapshot, payload);

        if (!TryParseSwitchKey(switchKey, out var trackState)) {
            PopulateLists(snapshot, puzzle);
            SetViewState(snapshot, puzzle, state, moveCount, errorMessage: "Invalid switch selection.");
            return Page();
        }

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new ToggleSwitchMove(trackState));
        if (result.IsSuccess) {
            state = result.State!;
            moveCount++;
            await RecordProgressAsync(puzzle, state, moveCount, cancellationToken);
        }

        PopulateLists(snapshot, puzzle);
        await LoadUserExtrasAsync(cancellationToken);
        string? msg = result.IsSuccess ? null : result.Message;
        if (msg is null && !payloadOk)
            msg = "State expired; reset to initial state.";
        SetViewState(snapshot, puzzle, state, moveCount, errorMessage: msg);
        return Page();
    }

    public async Task<IActionResult> OnPostRotateTurntableAsync(
        [FromForm] string? payload,
        [FromForm] string? turntableId,
        CancellationToken cancellationToken
    ) {
        var loaded = await LoadPuzzleAsync(this.Id, cancellationToken);
        if (loaded is null)
            return NotFound();

        var (snapshot, puzzle) = loaded.Value;
        var (state, moveCount, payloadOk) = DeserializePayloadOrFallback(snapshot, payload);

        var result = ShuntingEngine.TryApplyMove(puzzle, state, new RotateTurntableMove(turntableId ?? ""));
        if (result.IsSuccess) {
            state = result.State!;
            moveCount++;
            await RecordProgressAsync(puzzle, state, moveCount, cancellationToken);
        }

        PopulateLists(snapshot, puzzle);
        await LoadUserExtrasAsync(cancellationToken);
        string? msg = result.IsSuccess ? null : result.Message;
        if (msg is null && !payloadOk)
            msg = "State expired; reset to initial state.";
        SetViewState(snapshot, puzzle, state, moveCount, errorMessage: msg);
        return Page();
    }

    public async Task<IActionResult> OnPostVoteAsync(
        [FromForm] string? payload,
        [FromForm] short difficulty,
        [FromForm] short score,
        CancellationToken cancellationToken
    ) {
        var loaded = await LoadPuzzleAsync(this.Id, cancellationToken);
        if (loaded is null)
            return NotFound();

        var (snapshot, puzzle) = loaded.Value;
        var (state, moveCount, payloadOk) = DeserializePayloadOrFallback(snapshot, payload);

        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
            await _votes.UpsertAsync(this.Id, userId, difficulty, score, cancellationToken);

        PopulateLists(snapshot, puzzle);
        await LoadUserExtrasAsync(cancellationToken);
        SetViewState(snapshot, puzzle, state, moveCount, errorMessage: null);
        return Page();
    }

    private async Task<(PuzzleSnapshot snapshot, ShuntingPuzzle puzzle)?> LoadPuzzleAsync(Guid id, CancellationToken ct) {
        var entity = await _catalog.GetAsync(id, ct);
        if (entity is null)
            return null;

        var snapshot = PuzzleJson.Deserialize(entity.PuzzleJson);
        var puzzle = snapshot.ToPuzzle();
        return (snapshot, puzzle);
    }

    private (PuzzleState state, int moveCount, bool payloadOk) DeserializePayloadOrFallback(PuzzleSnapshot snapshot, string? payload) {
        if (!_payloadProtector.TryUnprotect(payload, out var parsed))
            return (snapshot.InitialState.ToPuzzleState(), 0, false);

        try {
            return (parsed!.State.ToPuzzleState(), parsed.MoveCount, true);
        }
        catch {
            return (snapshot.InitialState.ToPuzzleState(), 0, false);
        }
    }

    private void PopulateLists(PuzzleSnapshot snapshot, ShuntingPuzzle puzzle) {
        var vehicles = snapshot.RollingStock.Select(v => v.Id).OrderBy(v => v).ToArray();
        VehicleIds = vehicles;
        EngineIds = snapshot.RollingStock.Where(v => v.IsEngine).Select(v => v.Id).OrderBy(v => v).ToArray();

        SwitchKeys = puzzle.Track.StaticSwitchOptions.Keys
            .OrderBy(k => k.Node.X).ThenBy(k => k.Node.Y).ThenBy(k => k.Heading)
            .Select(k => (Value: FormatSwitchKey(k), Label: $"{k.Node.X},{k.Node.Y} {k.Heading}"))
            .ToArray();

        TurntableIds = snapshot.Track.Turntables.Select(t => t.Id).OrderBy(s => s, StringComparer.Ordinal).ToArray();
    }

    private async Task LoadUserExtrasAsync(CancellationToken ct) {
        MyDifficulty = null;
        MyScore = null;

        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var vote = await _votes.GetAsync(this.Id, userId, ct);
        if (vote is not null) {
            MyDifficulty = vote.Difficulty;
            MyScore = vote.Score;
        }
    }

    private async Task RecordProgressAsync(ShuntingPuzzle puzzle, PuzzleState state, int moveCount, CancellationToken ct) {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return;

        await _progress.UpsertPlayedAsync(this.Id, userId, ct);
        if (puzzle.IsSolved(state))
            await _progress.UpsertSolvedAsync(this.Id, userId, moveCount, ct);
    }

    private void SetViewState(PuzzleSnapshot snapshot, ShuntingPuzzle puzzle, PuzzleState state, int moveCount, string? errorMessage) {
        Svg = _svg.RenderPlayfield(snapshot, state);
        MoveCount = moveCount;
        Payload = _payloadProtector.Protect(new PlayPayload(PuzzleStateSnapshot.FromPuzzleState(state), moveCount));
        IsSolved = puzzle.IsSolved(state);
        ErrorMessage = errorMessage;
    }

    private static string FormatSwitchKey(TrackState key) => string.Create(CultureInfo.InvariantCulture, $"{key.Node.X};{key.Node.Y};{key.Heading}");

    private static bool TryParseSwitchKey(string? value, out TrackState key) {
        key = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split(';');
        if (parts.Length != 3)
            return false;
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x))
            return false;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
            return false;
        if (!Enum.TryParse(parts[2], ignoreCase: true, out Direction heading))
            return false;

        key = new TrackState(new GridPoint(x, y), heading);
        return true;
    }
}
