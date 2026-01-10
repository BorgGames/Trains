using Trains.Geometry;
using Trains.Puzzle;
using Trains.Track;

namespace Trains.Engine;

public static class ShuntingEngine {
    public static MoveResult TryApplyMove(ShuntingPuzzle puzzle, PuzzleState state, Move move) {
        if (puzzle is null)
            throw new ArgumentNullException(nameof(puzzle));
        if (state is null)
            throw new ArgumentNullException(nameof(state));
        if (move is null)
            throw new ArgumentNullException(nameof(move));

        if (!TryValidateState(puzzle, state, out string? validationError))
            return MoveResult.Fail(MoveError.InvalidState, validationError!);

        var next = state.Clone();

        try {
            return move switch {
                ToggleSwitchMove m => ApplyToggleSwitch(puzzle, next, m),
                ToggleCouplingMove m => ApplyToggleCoupling(puzzle, next, m),
                MoveEngineMove m => ApplyMoveEngine(puzzle, next, m),
                _ => MoveResult.Fail(MoveError.Unknown, $"Unknown move type '{move.GetType().FullName}'."),
            };
        }
        catch (Exception ex) {
            return MoveResult.Fail(MoveError.Unknown, ex.Message);
        }
    }

    private static MoveResult ApplyToggleSwitch(ShuntingPuzzle puzzle, PuzzleState state, ToggleSwitchMove move) {
        var options = puzzle.Track.GetOutgoingEdges(move.SwitchKey, state.TurntableStates);
        if (options.Count < 2)
            return MoveResult.Fail(MoveError.InvalidSwitch, "No switch at the specified state.");

        if (!state.SwitchStates.TryGetValue(move.SwitchKey, out int index))
            index = 0;

        index %= options.Count;
        state.SwitchStates[move.SwitchKey] = (index + 1) % options.Count;

        return MoveResult.Success(state);
    }

    private static MoveResult ApplyToggleCoupling(ShuntingPuzzle puzzle, PuzzleState state, ToggleCouplingMove move) {
        if (!state.Placements.ContainsKey(move.VehicleId))
            return MoveResult.Fail(MoveError.UnknownVehicle, $"Unknown vehicle id {move.VehicleId}.");

        if (!state.Couplings.TryGetValue(move.VehicleId, out var couplings)) {
            couplings = new VehicleCouplings();
            state.Couplings.Add(move.VehicleId, couplings);
        }

        var endCoupling = GetCoupling(couplings, move.End);

        // Disconnect if already coupled.
        if (endCoupling.HasValue) {
            var other = endCoupling.Value;
            if (!state.Couplings.TryGetValue(other.OtherVehicleId, out var otherCouplings))
                return MoveResult.Fail(MoveError.InvalidCoupling, "Coupling is not symmetric.");

            var otherEndCoupling = GetCoupling(otherCouplings, other.OtherEnd);
            if (!otherEndCoupling.HasValue || otherEndCoupling.Value.OtherVehicleId != move.VehicleId || otherEndCoupling.Value.OtherEnd != move.End)
                return MoveResult.Fail(MoveError.InvalidCoupling, "Coupling is not symmetric.");

            SetCoupling(couplings, move.End, null);
            SetCoupling(otherCouplings, other.OtherEnd, null);
            return MoveResult.Success(state);
        }

        // Connect to the unique adjacent candidate at this end.
        if (!TryFindAdjacentVehicle(puzzle, state, move.VehicleId, move.End, out int otherVehicleId, out VehicleEnd otherEnd, out string? error))
            return MoveResult.Fail(MoveError.InvalidCoupling, error!);

        if (!state.Couplings.TryGetValue(otherVehicleId, out var otherCouplings2)) {
            otherCouplings2 = new VehicleCouplings();
            state.Couplings.Add(otherVehicleId, otherCouplings2);
        }

        var otherEndCoupling2 = GetCoupling(otherCouplings2, otherEnd);
        if (otherEndCoupling2.HasValue)
            return MoveResult.Fail(MoveError.InvalidCoupling, "Other vehicle end is already coupled.");

        SetCoupling(couplings, move.End, new VehicleCoupling(otherVehicleId, otherEnd));
        SetCoupling(otherCouplings2, otherEnd, new VehicleCoupling(move.VehicleId, move.End));

        return MoveResult.Success(state);
    }

    private static MoveResult ApplyMoveEngine(ShuntingPuzzle puzzle, PuzzleState state, MoveEngineMove move) {
        if (!puzzle.RollingStock.TryGetValue(move.EngineId, out var spec))
            return MoveResult.Fail(MoveError.UnknownVehicle, $"Unknown vehicle id {move.EngineId}.");
        if (!spec.IsEngine)
            return MoveResult.Fail(MoveError.NotAnEngine, "Selected vehicle is not an engine.");

        if (!state.Placements.TryGetValue(move.EngineId, out _))
            return MoveResult.Fail(MoveError.InvalidState, "Selected engine has no placement.");

        if (!TryBuildOrientedTrain(puzzle, state, move.EngineId, move.Direction, out var orientedTrain, out string? error))
            return MoveResult.Fail(MoveError.NonLinearTrain, error!);

        int totalWeight = orientedTrain.VehicleIds.Sum(id => puzzle.RollingStock[id].Weight);

        int totalPower = 0;
        foreach (var (vehicleId, headEnd) in orientedTrain.VehicleIds.Zip(orientedTrain.EndsTowardHead, (id, end) => (id, end))) {
            var rs = puzzle.RollingStock[vehicleId];
            if (!rs.IsEngine)
                continue;

            totalPower += headEnd == VehicleEnd.Front ? rs.ForwardPower : rs.BackwardPower;
        }

        if (totalPower < totalWeight)
            return MoveResult.Fail(MoveError.InsufficientPower, $"Insufficient power: {totalPower} < {totalWeight}.");

        // Build an oriented path for the entire train (tail -> head).
        var trainEdges = new List<DirectedTrackEdge>();
        for (int i = 0; i < orientedTrain.VehicleIds.Count; i++) {
            int id = orientedTrain.VehicleIds[i];
            VehicleEnd headEnd = orientedTrain.EndsTowardHead[i];
            var placement = state.Placements[id];
            trainEdges.AddRange(placement.GetEdgesTowardHead(headEnd));
        }

        // Sanity: contiguity.
        VehiclePlacementValidator.ValidateEdgeChain(trainEdges);

        // Occupancy for collision checks.
        var occupancy = state.BuildSegmentOccupancy();
        var blocked = state.BuildBlockedNodes(puzzle);

        var movingSet = new HashSet<int>(orientedTrain.VehicleIds);
        var otherOccupiedSegments = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kvp in occupancy) {
            if (!movingSet.Contains(kvp.Value))
                otherOccupiedSegments.Add(kvp.Key);
        }

        var otherBlockedNodes = new HashSet<GridPoint>(blocked);
        // Nodes blocked by moving vehicles are ignored for movement collision checks.
        foreach (int vehicleId in movingSet) {
            if (puzzle.RollingStock[vehicleId].Length <= 1)
                continue;

            var p = state.Placements[vehicleId];
            for (int i = 0; i < p.Edges.Count - 1; i++)
                otherBlockedNodes.Remove(p.Edges[i].ToNode);
        }

        // Use a working copy for switch mutations; apply only on success.
        var switchStates = new Dictionary<TrackState, int>(state.SwitchStates);

        // Advance the head by length 1 (one edge).
        var headState = trainEdges[trainEdges.Count - 1].ToState;
        if (!TryAdvanceByLengthOne(
                puzzle,
                state.TurntableStates,
                switchStates,
                headState,
                otherOccupiedSegments,
                otherBlockedNodes,
                trainEdges,
                out var appended,
                out var advanceErrorCode,
                out string? advanceError
            ))
            return MoveResult.Fail(advanceErrorCode, advanceError!);

        // Advance the tail by length 1 (one edge).
        if (!TryRemoveFromTailByLengthOne(trainEdges, out int removeCount, out string? removeError))
            return MoveResult.Fail(MoveError.InvalidState, removeError!);

        var newTrainEdges = new List<DirectedTrackEdge>(trainEdges.Count - removeCount + appended.Count);
        newTrainEdges.AddRange(trainEdges.Skip(removeCount));
        newTrainEdges.AddRange(appended);

        // Re-split back into vehicles.
        if (!TryAssignTrainEdgesToVehicles(puzzle, state, orientedTrain, newTrainEdges, out string? splitError))
            return MoveResult.Fail(MoveError.InvalidState, splitError!);

        state.SwitchStates.Clear();
        foreach (var kvp in switchStates)
            state.SwitchStates[kvp.Key] = kvp.Value;

        return MoveResult.Success(state);
    }

    private static bool TryAdvanceByLengthOne(
        ShuntingPuzzle puzzle,
        IReadOnlyDictionary<string, int> turntableStates,
        Dictionary<TrackState, int> switchStates,
        TrackState start,
        HashSet<string> otherOccupiedSegments,
        HashSet<GridPoint> otherBlockedNodes,
        List<DirectedTrackEdge> currentTrainEdges,
        out List<DirectedTrackEdge> appended,
        out MoveError errorCode,
        out string? error
    ) {
        appended = new List<DirectedTrackEdge>(capacity: 1);
        errorCode = MoveError.Unknown;
        error = null;

        if (currentTrainEdges.Count == 0) {
            errorCode = MoveError.InvalidState;
            error = "Train has no segments.";
            return false;
        }

        var occupiedByTrain = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in currentTrainEdges)
            occupiedByTrain.Add(e.SegmentId);

        // Snake-style movement: moving by length 1 simultaneously removes the tail segment and adds a head segment.
        // It's valid for the head to move into the segment that the tail is vacating this tick.
        // This is well-defined because tail removal is exactly one edge (see TryRemoveFromTailByLengthOne).
        string tailSegmentId = currentTrainEdges[0].SegmentId;

        var options = puzzle.Track.GetOutgoingEdges(start, turntableStates);
        if (options.Count == 0) {
            errorCode = MoveError.NoTrackAhead;
            error = $"No outgoing track from {start}.";
            return false;
        }

        int index = 0;
        if (options.Count > 1 && switchStates.TryGetValue(start, out int stored))
            index = stored;

        if (index < 0 || index >= options.Count) {
            errorCode = MoveError.InvalidSwitch;
            error = $"Switch state index {index} is out of range at {start} (options={options.Count}).";
            return false;
        }

        var edge = options[index];

        // Collision checks.
        if (otherOccupiedSegments.Contains(edge.SegmentId)) {
            errorCode = MoveError.Collision;
            error = $"Collision on segment '{edge.SegmentId}'.";
            return false;
        }

        if (occupiedByTrain.Contains(edge.SegmentId) && !string.Equals(edge.SegmentId, tailSegmentId, StringComparison.Ordinal)) {
            errorCode = MoveError.LoopDetected;
            error = $"Move would cause the train to loop onto itself via segment '{edge.SegmentId}'.";
            return false;
        }

        if (otherBlockedNodes.Contains(edge.FromNode) || otherBlockedNodes.Contains(edge.ToNode)) {
            errorCode = MoveError.Collision;
            error = $"Move would pass through a node blocked by a long vehicle (edge '{edge.SegmentId}').";
            return false;
        }

        appended.Add(edge);

        if (options.Count > 1) {
            // Automatically toggle switch after passing through.
            switchStates[start] = (index + 1) % options.Count;
        }

        return true;
    }

    private static bool TryRemoveFromTailByLengthOne(IReadOnlyList<DirectedTrackEdge> trainEdges, out int removeCount, out string? error) {
        removeCount = 0;
        error = null;

        if (trainEdges.Count == 0) {
            error = "Train has no segments.";
            return false;
        }

        removeCount = 1;
        return true;
    }

    private static bool TryAssignTrainEdgesToVehicles(
        ShuntingPuzzle puzzle,
        PuzzleState state,
        OrientedTrain orientedTrain,
        List<DirectedTrackEdge> trainEdges,
        out string? error
    ) {
        error = null;

        // Ensure the final train path is contiguous.
        VehiclePlacementValidator.ValidateEdgeChain(trainEdges);

        int index = 0;
        for (int i = 0; i < orientedTrain.VehicleIds.Count; i++) {
            int vehicleId = orientedTrain.VehicleIds[i];
            VehicleEnd headEnd = orientedTrain.EndsTowardHead[i];
            var spec = puzzle.RollingStock[vehicleId];

            var edges = new List<DirectedTrackEdge>();
            int units = 0;

            while (index < trainEdges.Count && units < spec.Length) {
                var e = trainEdges[index++];
                edges.Add(e);
                units++;
            }

            if (units != spec.Length) {
                error = $"Unable to assign {spec.Length} unit segments to vehicle {vehicleId}.";
                return false;
            }

            if (i == orientedTrain.VehicleIds.Count - 1 && index != trainEdges.Count) {
                error = $"Unexpected extra unit segment '{trainEdges[index].SegmentId}' after assigning all vehicles.";
                return false;
            }

            IReadOnlyList<DirectedTrackEdge> placementEdges;
            if (headEnd == VehicleEnd.Front) {
                placementEdges = edges;
            }
            else {
                placementEdges = edges.AsEnumerable().Reverse().Select(e => e.Reverse()).ToArray();
            }

            state.Placements[vehicleId] = new VehiclePlacement(vehicleId, placementEdges);
        }

        return TryValidateState(puzzle, state, out error);
    }

    private static VehicleCoupling? GetCoupling(VehicleCouplings couplings, VehicleEnd end) =>
        end == VehicleEnd.Back ? couplings.Back : couplings.Front;

    private static void SetCoupling(VehicleCouplings couplings, VehicleEnd end, VehicleCoupling? coupling) {
        if (end == VehicleEnd.Back)
            couplings.Back = coupling;
        else
            couplings.Front = coupling;
    }

    private static bool TryFindAdjacentVehicle(
        ShuntingPuzzle puzzle,
        PuzzleState state,
        int vehicleId,
        VehicleEnd end,
        out int otherVehicleId,
        out VehicleEnd otherEnd,
        out string? error
    ) {
        otherVehicleId = -1;
        otherEnd = default;
        error = null;

        var placement = state.Placements[vehicleId];
        var node = placement.GetEndState(end).Node;

        var outward = GetOutwardHeading(placement, end);

        var candidates = new List<(int otherId, VehicleEnd otherEnd)>();
        foreach (var kvp in state.Placements) {
            int id = kvp.Key;
            var p = kvp.Value;

            if (id == vehicleId)
                continue;

            foreach (var end2 in new[] { VehicleEnd.Back, VehicleEnd.Front }) {
                if (p.GetEndState(end2).Node != node)
                    continue;

                var inward2 = GetInwardHeading(p, end2);
                if (inward2 == outward)
                    candidates.Add((id, end2));
            }
        }

        if (candidates.Count == 0) {
            error = "No adjacent vehicle at the specified end.";
            return false;
        }

        if (candidates.Count > 1) {
            error = "Multiple adjacent vehicles found; coupling is ambiguous.";
            return false;
        }

        (otherVehicleId, otherEnd) = candidates[0];
        return true;
    }

    private static Direction GetOutwardHeading(VehiclePlacement placement, VehicleEnd end) {
        if (end == VehicleEnd.Front)
            return placement.FrontState.Heading;
        return placement.BackState.Heading.Opposite();
    }

    private static Direction GetInwardHeading(VehiclePlacement placement, VehicleEnd end) {
        if (end == VehicleEnd.Back)
            return placement.BackState.Heading;
        return placement.FrontState.Heading.Opposite();
    }

    private static bool TryValidateState(ShuntingPuzzle puzzle, PuzzleState state, out string? error) {
        error = null;

        // Validate turntable states early (avoid runtime exceptions when generating edges).
        foreach (var kvp in state.TurntableStates) {
            string turntableId = kvp.Key;
            int alignment = kvp.Value;

            var tt = puzzle.Track.Turntables.FirstOrDefault(t => string.Equals(t.Id, turntableId, StringComparison.Ordinal));
            if (tt is null) {
                error = $"Turntable state refers to unknown turntable '{turntableId}'.";
                return false;
            }

            if (alignment < 0 || alignment >= tt.Alignments.Count) {
                error = $"Turntable '{turntableId}' alignment index {alignment} is out of range (0..{tt.Alignments.Count - 1}).";
                return false;
            }
        }

        // Validate switch state indices.
        foreach (var kvp in state.SwitchStates) {
            var key = kvp.Key;
            int index = kvp.Value;

            var options = puzzle.Track.GetOutgoingEdges(key, state.TurntableStates);
            if (options.Count < 2) {
                error = $"Switch state stored for {key} but there is no switch there.";
                return false;
            }

            if (index < 0 || index >= options.Count) {
                error = $"Switch state index {index} is out of range at {key} (options={options.Count}).";
                return false;
            }
        }

        // Placements: known vehicles, valid paths, correct lengths, valid edges, no segment overlaps.
        foreach (var kvp in state.Placements) {
            int vehicleId = kvp.Key;
            var placement = kvp.Value;
            if (!puzzle.RollingStock.TryGetValue(vehicleId, out var spec)) {
                error = $"Unknown vehicle id {vehicleId}.";
                return false;
            }

            int unitCount = VehiclePlacement.CountUnitEdges(placement.Edges);
            if (unitCount != spec.Length) {
                error = $"Vehicle {vehicleId} has {unitCount} unit segments but requires {spec.Length}.";
                return false;
            }

            try {
                VehiclePlacementValidator.ValidateEdgeChain(placement.Edges);
            }
            catch (Exception ex) {
                error = $"Vehicle {vehicleId} has an invalid edge chain: {ex.Message}";
                return false;
            }

            foreach (var edge in placement.Edges) {
                var options = puzzle.Track.GetOutgoingEdges(edge.FromState, state.TurntableStates);
                if (!options.Contains(edge)) {
                    error = $"Vehicle {vehicleId} uses an invalid edge on segment '{edge.SegmentId}'.";
                    return false;
                }
            }
        }

        try {
            state.BuildSegmentOccupancy();
        }
        catch (Exception ex) {
            error = ex.Message;
            return false;
        }

        // Node blocking by long vehicles.
        var blockedBy = new Dictionary<GridPoint, int>();
        foreach (var kvp in state.Placements) {
            int vehicleId = kvp.Key;
            var placement = kvp.Value;
            var spec = puzzle.RollingStock[vehicleId];
            if (spec.Length <= 1)
                continue;
            for (int i = 0; i < placement.Edges.Count - 1; i++) {
                var node = placement.Edges[i].ToNode;
                if (!blockedBy.ContainsKey(node))
                    blockedBy.Add(node, vehicleId);
            }
        }

        foreach (var kvp in state.Placements) {
            int vehicleId = kvp.Key;
            var placement = kvp.Value;
            foreach (var edge in placement.Edges) {
                if (blockedBy.TryGetValue(edge.FromNode, out int blocker) && blocker != vehicleId) {
                    error = $"Vehicle {vehicleId} passes through node {edge.FromNode} blocked by vehicle {blocker}.";
                    return false;
                }
                if (blockedBy.TryGetValue(edge.ToNode, out int blocker2) && blocker2 != vehicleId) {
                    error = $"Vehicle {vehicleId} passes through node {edge.ToNode} blocked by vehicle {blocker2}.";
                    return false;
                }
            }
        }

        // Couplings: symmetric and adjacent.
        foreach (var kvp in state.Couplings) {
            int vehicleId = kvp.Key;
            var couplings = kvp.Value;

            foreach (var (end, coupling) in new[] { (VehicleEnd.Back, couplings.Back), (VehicleEnd.Front, couplings.Front) }) {
                if (!coupling.HasValue)
                    continue;

                if (!state.Placements.ContainsKey(vehicleId) || !state.Placements.ContainsKey(coupling.Value.OtherVehicleId)) {
                    error = "Coupling refers to a missing placement.";
                    return false;
                }

                if (!state.Couplings.TryGetValue(coupling.Value.OtherVehicleId, out var otherCouplings)) {
                    error = "Coupling is not symmetric.";
                    return false;
                }

                var otherRef = GetCoupling(otherCouplings, coupling.Value.OtherEnd);
                if (!otherRef.HasValue || otherRef.Value.OtherVehicleId != vehicleId || otherRef.Value.OtherEnd != end) {
                    error = "Coupling is not symmetric.";
                    return false;
                }

                // Check adjacency.
                var p1 = state.Placements[vehicleId];
                var p2 = state.Placements[coupling.Value.OtherVehicleId];

                if (p1.GetEndState(end).Node != p2.GetEndState(coupling.Value.OtherEnd).Node) {
                    error = "Coupled ends do not touch.";
                    return false;
                }

                if (GetOutwardHeading(p1, end) != GetInwardHeading(p2, coupling.Value.OtherEnd)) {
                    error = "Coupled ends are not aligned.";
                    return false;
                }
            }
        }

        return true;
    }

    private sealed class OrientedTrain {
        public OrientedTrain(List<int> vehicleIds, List<VehicleEnd> endsTowardHead) {
            VehicleIds = vehicleIds;
            EndsTowardHead = endsTowardHead;
        }

        /// <summary>
        /// Vehicle ids ordered from tail to head for the selected move direction.
        /// </summary>
        public List<int> VehicleIds { get; }

        /// <summary>
        /// For each entry in <see cref="VehicleIds"/>, the vehicle end that faces toward the head (move direction).
        /// </summary>
        public List<VehicleEnd> EndsTowardHead { get; }
    }

    private static bool TryBuildOrientedTrain(
        ShuntingPuzzle puzzle,
        PuzzleState state,
        int engineId,
        EngineMoveDirection moveDirection,
        out OrientedTrain orientedTrain,
        out string? error
    ) {
        orientedTrain = null!;
        error = null;

        // Build component.
        var component = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(engineId);
        component.Add(engineId);

        while (queue.Count > 0) {
            int v = queue.Dequeue();
            if (!state.Couplings.TryGetValue(v, out var c))
                continue;

            foreach (var link in new[] { c.Back, c.Front }) {
                if (!link.HasValue)
                    continue;

                int other = link.Value.OtherVehicleId;
                if (!state.Placements.ContainsKey(other)) {
                    error = $"Coupling references vehicle {other} which has no placement.";
                    return false;
                }

                if (component.Add(other))
                    queue.Enqueue(other);
            }
        }

        // Degree check to ensure a simple chain.
        int endpoints = 0;
        foreach (int v in component) {
            int degree = 0;
            if (state.Couplings.TryGetValue(v, out var c)) {
                if (c.Back.HasValue) degree++;
                if (c.Front.HasValue) degree++;
            }

            if (degree > 2) {
                error = "Train component is not linear (branching couplings).";
                return false;
            }

            if (degree <= 1)
                endpoints++;
        }

        if (component.Count > 1 && endpoints != 2) {
            error = "Train component is not a simple chain (cycle or disconnected coupling data).";
            return false;
        }

        // Determine engine headward end.
        VehicleEnd engineHeadward = moveDirection == EngineMoveDirection.Forward ? VehicleEnd.Front : VehicleEnd.Back;
        VehicleEnd engineTailward = engineHeadward.Opposite();

        // Walk from engine to tailmost, carrying headward end for each visited.
        var headwardByVehicle = new Dictionary<int, VehicleEnd> { [engineId] = engineHeadward };

        int current = engineId;
        VehicleEnd currentHeadward = engineHeadward;
        while (true) {
            VehicleEnd tailward = currentHeadward.Opposite();
            if (!TryGetCoupledNeighbor(state, current, tailward, out int next, out VehicleEnd nextEnd))
                break;

            // For the neighbor behind us, the connected end is headward.
            headwardByVehicle[next] = nextEnd;
            current = next;
            currentHeadward = nextEnd;
        }

        int tailmost = current;

        // Walk headward from tailmost to build full order.
        var tailToHeadVehicles = new List<int>();
        var tailToHeadEndsTowardHead = new List<VehicleEnd>();

        current = tailmost;
        currentHeadward = headwardByVehicle[current];
        while (true) {
            tailToHeadVehicles.Add(current);
            tailToHeadEndsTowardHead.Add(currentHeadward);

            if (!TryGetCoupledNeighbor(state, current, currentHeadward, out int next, out VehicleEnd nextEnd))
                break;

            // For the neighbor ahead, the connected end is tailward.
            current = next;
            currentHeadward = nextEnd.Opposite();
        }

        if (!tailToHeadVehicles.Contains(engineId)) {
            error = "Internal error: engine not found in constructed chain.";
            return false;
        }

        // Verify the chain covers the whole component.
        if (tailToHeadVehicles.Count != component.Count) {
            error = "Train component ordering failed; possible cycle.";
            return false;
        }

        orientedTrain = new OrientedTrain(tailToHeadVehicles, tailToHeadEndsTowardHead);
        return true;
    }

    private static bool TryGetCoupledNeighbor(
        PuzzleState state,
        int vehicleId,
        VehicleEnd viaEnd,
        out int otherId,
        out VehicleEnd otherEnd
    ) {
        otherId = -1;
        otherEnd = default;

        if (!state.Couplings.TryGetValue(vehicleId, out var c))
            return false;

        var link = viaEnd == VehicleEnd.Back ? c.Back : c.Front;
        if (!link.HasValue)
            return false;

        otherId = link.Value.OtherVehicleId;
        otherEnd = link.Value.OtherEnd;
        return true;
    }
}
