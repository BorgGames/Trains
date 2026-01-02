using Trains.Geometry;
using Trains.Track;

namespace Trains.Puzzle;

/// <summary>
/// Mutable state of a shunting puzzle.
/// </summary>
public sealed class PuzzleState {
    public PuzzleState() {
        this.SwitchStates = new Dictionary<TrackState, int>();
        this.TurntableStates = new Dictionary<string, int>(StringComparer.Ordinal);
        this.Placements = new Dictionary<int, VehiclePlacement>();
        this.Couplings = new Dictionary<int, VehicleCouplings>();
    }

    public Dictionary<TrackState, int> SwitchStates { get; }
    public Dictionary<string, int> TurntableStates { get; }
    public Dictionary<int, VehiclePlacement> Placements { get; }
    public Dictionary<int, VehicleCouplings> Couplings { get; }

    public PuzzleState Clone() {
        var clone = new PuzzleState();

        foreach (var kvp in this.SwitchStates)
            clone.SwitchStates.Add(kvp.Key, kvp.Value);
        foreach (var kvp in this.TurntableStates)
            clone.TurntableStates.Add(kvp.Key, kvp.Value);

        foreach (var kvp in this.Placements)
            clone.Placements.Add(kvp.Key, new VehiclePlacement(kvp.Key, kvp.Value.Edges.ToArray()));

        foreach (var kvp in this.Couplings) {
            int vehicleId = kvp.Key;
            var couplings = kvp.Value;
            clone.Couplings.Add(
                vehicleId,
                new VehicleCouplings {
                    Back = couplings.Back,
                    Front = couplings.Front,
                }
            );
        }

        return clone;
    }

    public IReadOnlyDictionary<string, int> BuildSegmentOccupancy() {
        var occupancy = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var kvp in this.Placements) {
            int vehicleId = kvp.Key;
            var placement = kvp.Value;
            foreach (var edge in placement.Edges) {
                if (occupancy.TryGetValue(edge.SegmentId, out int other))
                    throw new InvalidOperationException($"Segment '{edge.SegmentId}' is occupied by both {other} and {vehicleId}.");
                occupancy.Add(edge.SegmentId, vehicleId);
            }
        }

        return occupancy;
    }

    public IReadOnlyCollection<GridPoint> BuildBlockedNodes(ShuntingPuzzle puzzle) {
        if (puzzle is null)
            throw new ArgumentNullException(nameof(puzzle));

        var blocked = new HashSet<GridPoint>();

        foreach (var kvp in this.Placements) {
            int vehicleId = kvp.Key;
            var placement = kvp.Value;
            if (!puzzle.RollingStock.TryGetValue(vehicleId, out var spec))
                throw new InvalidOperationException($"Unknown vehicle id {vehicleId}.");

            if (spec.Length <= 1)
                continue;

            // Block internal joints between segments within a single long vehicle.
            for (int i = 0; i < placement.Edges.Count - 1; i++) {
                blocked.Add(placement.Edges[i].ToNode);
            }
        }

        return blocked;
    }
}
