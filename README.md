# Trains

Train shunting puzzle engine for .NET.

This repo contains a small “core logic” library that models:
- A track layout on an integer grid (straight + curved segments, plus turntables)
- A puzzle state (rolling stock placements, couplings, switch/turntable states)
- A discrete move engine (toggle couplings/switches, move an engine by length 1)

It is inspired by the style of https://github.com/lostmsu/RoboZZle.Core/ (small, testable core types with immutable “puzzle definition” + mutable “state”).

## Projects

- `src/Trains.Core/Trains.Core.csproj`: library (`netstandard2.0;net10.0`, C# 14)
- `src/Trains.Persistence/Trains.Persistence.csproj`: EF Core model + PostgreSQL (`net10.0`)
- `src/Trains.Web/Trains.Web.csproj`: Razor Pages website (`net10.0`)
- `tests/Trains.Core.Tests/Trains.Core.Tests.csproj`: xUnit tests (`net10.0`)
- `Trains.slnx`: solution file

## Concepts

### Grid and headings

- The track is laid on integer grid nodes (`Trains.Geometry.GridPoint`).
- Routing is heading-based: a train is “at node (x,y) with a current heading” (`Trains.Track.TrackState`).

### Track segments

All segments produce directed traversals (`Trains.Track.DirectedTrackEdge`) that include:
- From/To nodes
- Entry/Exit headings (enforces “no 90° turns unless a curve exists”)
- Unit length (`Length = 1`) per edge

Implemented segment types:

- `Trains.Track.StraightSegment`
  - Orthogonally adjacent nodes
  - `Length = 1`, heading preserved
- `Trains.Track.CurvedSegment`
  - Diagonally adjacent nodes (delta is `(±1,±1)`)
  - `Length = 1`, heading changes by 90°
  - `CurveBias` disambiguates the two curves between the same diagonal endpoints

All track segments are unit-length; longer paths are represented as multiple segments.

### Switches

Any `TrackState` with multiple outgoing edges acts like a switch.

- `Trains.Track.TrackLayout.StaticSwitchOptions` exposes switch states and their outgoing options (deterministic order).
- `Trains.Puzzle.PuzzleState.SwitchStates` stores the selected option index per switch state.

When moving a train through a switch, the engine automatically advances the selection after passing through.
There is also an explicit move to toggle a switch (`ToggleSwitchMove`).

Switch option ordering is stable: it sorts straight edges first, then by exit heading, then by segment id.

### Turntables

Turntables are optional and are part of the track layout (`Trains.Track.Turntable`).

- A turntable is centered on a grid node with an integer square radius.
- “Ports” live on the square border and are defined as `(GridPoint point, Direction outboundDirection)`.
- Each “alignment” connects two ports and provides a straight “bridge” of total length `2*Radius`.
  - This is modeled as `2*Radius` unit segments (`Length = 1` each), with segment ids `Turntable:{id}:{i}`.
  - Current limitation: an alignment must connect opposite ports on the center line (East/West or North/South).
  - Because the bridge is multiple unit segments, multiple trains can occupy different parts at once (e.g. a length-4 bridge can fit two length-2 trains).

Turntable alignments are controlled by `Trains.Puzzle.PuzzleState.TurntableStates` (`turntableId -> alignmentIndex`).
The engine also supports rotating a turntable to its next alignment (`RotateTurntableMove`); rotation is rejected if any train occupies the bridge segments.

## Rolling stock

Rolling stock is defined in the puzzle definition (`Trains.Puzzle.RollingStockSpec`):

- `Length` is measured in "unit segments" (every edge has `Length = 1`).
- `Weight` is an arbitrary unit.
- Engines are `EngineSpec` and have `ForwardPower` and `BackwardPower` (either can be 0).
- Cars are `CarSpec` (power is always 0).

For a train to move, the sum of engine power in that direction must be `>=` the total weight of the coupled component being moved.
Power is directional: moving “forward” uses `ForwardPower`, moving “backward” uses `BackwardPower`.

## Puzzle model

- `Trains.Puzzle.ShuntingPuzzle` is the immutable definition: `TrackLayout`, rolling stock list, initial state, and goal.
- `Trains.Puzzle.PuzzleState` is the mutable state: placements, couplings, switch states, and turntable states.
- `Trains.Puzzle.Goal` is a list of `SegmentGoal` constraints.

### "Passing through" at 90° crossings

The engine uses two collision rules:
- Two vehicles cannot occupy the same segment id.
- Vehicles with `Length > 1` block the internal grid nodes between their unit segments, preventing other trains from passing through them at those nodes.

### Loops

When moving by one unit, the engine advances the head by one segment and removes one segment from the tail in the same tick.
This allows “snake-style” rotation in closed loops: the head may enter the segment that the tail is vacating.
The head cannot enter segments occupied by other parts of the train (that still produces `MoveError.LoopDetected`).

## Moves and the engine

Moves are in `Trains.Engine`:

- `ToggleSwitchMove(TrackState switchKey)`
- `ToggleCouplingMove(int vehicleId, VehicleEnd end)`
- `RotateTurntableMove(string turntableId)`
- `MoveEngineMove(int engineId, EngineMoveDirection direction)`

The engine entry point is:

- `Trains.Engine.ShuntingEngine.TryApplyMove(ShuntingPuzzle puzzle, PuzzleState state, Move move)`

Moves are atomic: `TryApplyMove` clones the input state and returns a new state on success; on failure the original state is unchanged.
It also validates state integrity before applying a move (placements, lengths, overlaps, switch indices, coupling symmetry, turntable state indices, etc.).

`MoveEngineMove` advances the whole coupled component by length `1` (one segment) and applies switch auto-toggling as the train passes through switch states.

On success you get a cloned next state (`MoveResult.State`); on failure you get `MoveResult.Error` + `MoveResult.Message`.
Common `MoveError` values include `InsufficientPower`, `Collision`, `NoTrackAhead`, `InvalidSwitch`, and `InvalidCoupling`.

## Solutions

- `Trains.Puzzle.Solution` is an immutable list of `Trains.Puzzle.SolutionMove` items (a data-oriented mirror of `Trains.Engine.Move`).
- `Trains.Puzzle.InMemorySolutionHistory` is a RoboZZle-style snapshot history with `Add`, `Undo`, and `Redo`.
- `Trains.Puzzle.SolutionHistoryJson` serializes/deserializes `Trains.Puzzle.SolutionHistorySnapshot` to JSON.
- `Trains.Engine.SolutionVerifier.Verify` executes a solution against a puzzle and reports whether it solved the goal.
- `Trains.Puzzle.VerifiedPuzzle.TryCreate` is a helper for accepting/rejecting puzzles based on their shipped solution.

## Example

This is the "3 straight segments" example:

```csharp
using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

var segments = new TrackSegment[] {
    new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0)),
    new StraightSegment("S1", new GridPoint(1, 0), new GridPoint(2, 0)),
    new StraightSegment("S2", new GridPoint(2, 0), new GridPoint(3, 0)),
};

var track = TrackLayout.Create(segments);

var car0 = new CarSpec(id: 0, length: 1, weight: 1);
var engine1 = new EngineSpec(id: 1, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);

var state = new PuzzleState();
state.Placements.Add(0, new VehiclePlacement(0, new[] { segments[0].GetDirectedEdges()[0] }));
state.Placements.Add(1, new VehiclePlacement(1, new[] { segments[1].GetDirectedEdges()[0] }));
state.Couplings.Add(0, new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) });
state.Couplings.Add(1, new VehicleCouplings { Back = new VehicleCoupling(0, VehicleEnd.Front) });

var goal = new Goal(new[] {
    new SegmentGoal("S1", allowedVehicleIds: new[] { 0, 1 }),
    new SegmentGoal("S2", allowedVehicleIds: new[] { 1 }),
});

var puzzle = new ShuntingPuzzle(track, new RollingStockSpec[] { car0, engine1 }, state, goal);

var result = ShuntingEngine.TryApplyMove(puzzle, state, new MoveEngineMove(engine1.Id, EngineMoveDirection.Forward));
if (!result.IsSuccess)
    throw new InvalidOperationException(result.Message);

Console.WriteLine($"Solved: {puzzle.IsSolved(result.State!)}");
```

For a more complete set of examples (curves, switches, turntables, and error cases), see the tests in `tests/Trains.Core.Tests/`.

## Web app (play + submit puzzles)

`Trains.Web` is a minimal Razor Pages UI (no JS frameworks, minimal CSS) backed by PostgreSQL:
- Anyone can browse and play puzzles anonymously (`/` and `/p/{id}`).
- With an account, you can filter by solved/unsolved, rate puzzles (difficulty+score 1..5), and submit puzzles (`/p/submit`).
- Submitted puzzles must include a solution history; the server verifies the current solution solves the puzzle before publishing.

### Run locally

1. Start PostgreSQL and create a database (example name `trains`).
2. Configure the connection string via either:
   - `src/Trains.Web/appsettings.Development.json` (`ConnectionStrings:Trains`), or
   - env var `ConnectionStrings__Trains`
3. Apply migrations:

   `dotnet tool run dotnet-ef database update --project src/Trains.Persistence --startup-project src/Trains.Web --context TrainsDbContext`

4. Run:

   `dotnet run --project src/Trains.Web`

On first run the app seeds a sample puzzle.

## PostgreSQL integration tests

There is an opt-in integration test project that runs EF Core against a real PostgreSQL instance using a temporary database per test:

- Project: `tests/Trains.Persistence.IntegrationTests/Trains.Persistence.IntegrationTests.csproj`
- Enable: set `TRAINS_PG_TESTS=1`
- Optional config: `TRAINS_PG_ADMIN` and `TRAINS_PG_BASE`

See `tests/Trains.Persistence.IntegrationTests/README.md` for details.
