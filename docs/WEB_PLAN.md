# Trains Web (Razor Pages) – Architecture / Interfaces (Draft)

Goal: a minimal ASP.NET Razor Pages website (no JS frameworks, minimal styling) where:
- Anyone can browse and play puzzles anonymously.
- Registered users can filter by solved/unsolved, rate puzzles (difficulty + score), and submit new puzzles.
- Submitted puzzles must include a solution; the server verifies solvability before publishing.
- No puzzle titles (use SVG thumbnails).

## Projects (Solution Layout)

- `Trains.Core` (existing): puzzle engine and validation.
- `Trains.Persistence` (new): EF Core model + PostgreSQL (Npgsql) mappings.
- `Trains.Web` (new): ASP.NET 10 Razor Pages UI + auth.

## Serialization Boundary (Core ↔ DB/UI)

Persisted puzzles and per-user progress must be stored as stable snapshots, not raw engine objects.

### Snapshot Types (in `Trains.Core`)

Namespace: `Trains.Puzzle.Serialization` (new)

- `PuzzleSnapshot`
  - `int SchemaVersion`
  - `TrackLayoutSnapshot Track`
  - `List<RollingStockSpecSnapshot> RollingStock`
  - `PuzzleStateSnapshot InitialState`
  - `GoalSnapshot Goal`
  - `SolutionHistorySnapshot? VerifiedSolutionHistory` (required for published puzzles)

- `RollingStockSpecSnapshot`
  - `int Id`
  - `int Length`
  - `int Weight`
  - `bool IsEngine`
  - `int? ForwardPower`
  - `int? BackwardPower`

- `TrackLayoutSnapshot`
  - `List<SegmentSnapshot>` (straight/curve)
  - `List<TurntableSnapshot>`

- `SegmentSnapshot` (discriminated by `Kind`)
  - `string Id`
  - `GridPoint A`, `GridPoint B`
  - `CurveBias? Bias` (for curves)

- `TurntableSnapshot`
  - `string Id`
  - `GridPoint Center`
  - `int Radius`
  - `List<TurntablePortSnapshot> Ports`
  - `List<TurntableAlignmentSnapshot> Alignments`

- `TurntablePortSnapshot`
  - `GridPoint Point`
  - `Direction OutboundDirection`

- `TurntableAlignmentSnapshot`
  - `int PortAIndex`
  - `int PortBIndex`

- `PuzzleStateSnapshot`
  - `List<SwitchStateSnapshot> SwitchStates`
  - `Dictionary<string,int> TurntableStates`
  - `List<VehiclePlacementSnapshot> Placements`
  - `List<VehicleCouplingsSnapshot> Couplings`

- `SwitchStateSnapshot`
  - `TrackStateSnapshot State`
  - `int SelectedOptionIndex`

- `TrackStateSnapshot`
  - `int NodeX`
  - `int NodeY`
  - `Direction Heading`

- `VehiclePlacementSnapshot`
  - `int VehicleId`
  - `List<DirectedTrackEdgeSnapshot> Edges` (back-to-front)

- `DirectedTrackEdgeSnapshot`
  - `string SegmentId`
  - `GridPoint FromNode`
  - `GridPoint ToNode`
  - `Direction EntryHeading`
  - `Direction ExitHeading`

- `VehicleCouplingsSnapshot`
  - `int VehicleId`
  - `VehicleCouplingSnapshot? Back`
  - `VehicleCouplingSnapshot? Front`

- `VehicleCouplingSnapshot`
  - `int OtherVehicleId`
  - `VehicleEnd OtherEnd`

- `GoalSnapshot`
  - `List<SegmentGoalSnapshot> SegmentGoals`

- `SegmentGoalSnapshot`
  - `string SegmentId`
  - `List<int>? AllowedVehicleIds` (null = occupied by anything, empty = must be empty)

- JSON helpers:
  - `PuzzleJson.Serialize(PuzzleSnapshot)`
  - `PuzzleJson.Deserialize(string)`

Conversions:
- `ShuntingPuzzle PuzzleSnapshot.ToPuzzle()`
- `PuzzleSnapshot PuzzleSnapshot.FromPuzzle(ShuntingPuzzle puzzle, SolutionHistorySnapshot verifiedSolutionHistory)`

Validation gate:
- `VerifiedPuzzle.TryCreate(puzzle, solutionHistorySnapshot, ...)` is used by the web app before a puzzle is accepted/published.

## Persistence Boundary (Web ↔ PostgreSQL)

### EF Core Entities (in `Trains.Persistence`)

- `PuzzleEntity`
  - `Guid Id`
  - `DateTimeOffset CreatedAt`
  - `string CreatedByUserId` (nullable for seeded puzzles)
  - `string PuzzleJson` (jsonb)
  - `string SolutionHistoryJson` (jsonb)
  - `string ThumbnailSvg` (text)
  - `bool IsPublished`

- `PuzzleVoteEntity` (PK: `PuzzleId + UserId`)
  - `Guid PuzzleId`
  - `string UserId`
  - `short Difficulty` (1..5)
  - `short Score` (1..5)
  - `DateTimeOffset UpdatedAt`

- `PuzzleSolveEntity` (PK: `PuzzleId + UserId`)
  - `Guid PuzzleId`
  - `string UserId`
  - `DateTimeOffset? SolvedAt`
  - `int? BestMoveCount`
  - `DateTimeOffset LastPlayedAt`

### Repository Interfaces (in `Trains.Web`)

These are intentionally small and UI-driven.

- `IPuzzleCatalog`
  - `Task<PuzzleSummaryPage> ListAsync(PuzzleListQuery query, CancellationToken ct)`
  - `Task<PuzzleDetails?> GetAsync(Guid id, CancellationToken ct)`
  - `Task<Guid> CreateAsync(PuzzleSnapshot puzzle, SolutionHistorySnapshot solutionHistory, string? createdByUserId, CancellationToken ct)`
  - `Task PublishAsync(Guid puzzleId, CancellationToken ct)`

- `IPuzzleProgressStore`
  - `Task<UserPuzzleProgress?> GetAsync(Guid puzzleId, string userId, CancellationToken ct)`
  - `Task UpsertSolvedAsync(Guid puzzleId, string userId, int moveCount, CancellationToken ct)`

- `IPuzzleVotingStore`
  - `Task<UserPuzzleVote?> GetAsync(Guid puzzleId, string userId, CancellationToken ct)`
  - `Task UpsertAsync(Guid puzzleId, string userId, short difficulty, short score, CancellationToken ct)`

### Domain/Application Services (in `Trains.Web`)

- `PuzzleSubmissionService`
  - Takes user submission DTOs, validates JSON, runs `SolutionVerifier`/`VerifiedPuzzle`, generates a thumbnail, stores and publishes.

- `PlaySessionService`
  - Stateless by default (anonymous): accepts current state snapshot + move, returns next state snapshot + updated SVG + error.
  - For authenticated users: can optionally persist “solved” and/or last-played timestamps.

## Rendering Boundary (Core ↔ UI)

### SVG Rendering Service (in `Trains.Web`)

- `IPuzzleSvgRenderer`
  - `string RenderThumbnail(PuzzleSnapshot puzzle)`
  - `string RenderPlayfield(PuzzleSnapshot puzzle, PuzzleState state, SvgRenderOptions options)`

Inputs:
- `PuzzleSnapshot` (track + initial placements)
- `PuzzleState` (current state during play)

Output:
- Inline `<svg>` (no canvas).
- Trains/cars as rectangles along unit segments; curves as quarter-arc paths.

## Razor Pages (UI)

Anonymous:
- `/` – puzzle grid (thumbnails only)
- `/p/{id}` – play page
  - MVP: plain POST (works without JS)
  - Enhancement (still “no frameworks”): vanilla JS `fetch` to apply moves and patch the SVG/state without full page reload

Authenticated:
- `/` adds filters (solved/unsolved) and “my rating”
- `/p/{id}` adds vote form (difficulty+score)
- `/p/submit` – submit puzzle JSON + solution JSON (MVP), server verifies and publishes

No discussion/comments, no puzzle titles.

## MVP Decisions (to unblock implementation)

- Puzzle authoring starts as a JSON upload + server-side verification; graphical editor comes later.
- Anonymous play is supported without saving progress.
- Registered user progress is stored on successful solve (best move count).
