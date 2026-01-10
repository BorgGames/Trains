using Trains.Track;

namespace Trains.Puzzle;

/// <summary>
/// A placement of a vehicle as a directed path of track edges from back to front.
/// </summary>
public sealed class VehiclePlacement {
    public VehiclePlacement(int vehicleId, IReadOnlyList<DirectedTrackEdge> edges) {
        if (vehicleId < 0)
            throw new ArgumentOutOfRangeException(nameof(vehicleId), vehicleId, "Vehicle id must be non-negative.");
        if (edges is null)
            throw new ArgumentNullException(nameof(edges));
        if (edges.Count == 0)
            throw new ArgumentException("Placement must contain at least one edge.", nameof(edges));

        VehiclePlacementValidator.ValidateEdgeChain(edges);

        this.VehicleId = vehicleId;
        this.Edges = edges;
    }

    public int VehicleId { get; }

    /// <summary>
    /// Edges from back to front.
    /// </summary>
    public IReadOnlyList<DirectedTrackEdge> Edges { get; }

    public TrackState BackState => this.Edges[0].FromState;
    public TrackState FrontState => this.Edges[this.Edges.Count - 1].ToState;

    public TrackState GetEndState(VehicleEnd end) => end == VehicleEnd.Back ? this.BackState : this.FrontState;

    /// <summary>
    /// Returns the placement edges oriented so that traversal goes from tail to head for a particular move direction.
    /// Pass the vehicle end that should face the head (move direction).
    /// </summary>
    public IReadOnlyList<DirectedTrackEdge> GetEdgesTowardHead(VehicleEnd headEnd) {
        if (headEnd == VehicleEnd.Front)
            return this.Edges;

        var reversed = new DirectedTrackEdge[this.Edges.Count];
        for (int i = 0; i < this.Edges.Count; i++) {
            reversed[i] = this.Edges[this.Edges.Count - 1 - i].Reverse();
        }
        return reversed;
    }

    public static int CountUnitEdges(IReadOnlyList<DirectedTrackEdge> edges) {
        if (edges is null)
            throw new ArgumentNullException(nameof(edges));
        return edges.Count;
    }
}

internal static class VehiclePlacementValidator {
    public static void ValidateEdgeChain(IReadOnlyList<DirectedTrackEdge> edges) {
        for (int i = 0; i < edges.Count - 1; i++) {
            var a = edges[i];
            var b = edges[i + 1];

            if (a.ToNode != b.FromNode)
                throw new ArgumentException("Edges do not form a contiguous chain (node mismatch).");
            if (a.ExitHeading != b.EntryHeading)
                throw new ArgumentException("Edges do not form a contiguous chain (heading mismatch).");
        }
    }
}
