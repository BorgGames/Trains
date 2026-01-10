using Trains.Geometry;

namespace Trains.Track;

/// <summary>
/// A track layout: segments plus optional turntables, with derived routing data (switches).
/// </summary>
public sealed class TrackLayout {
    private readonly Dictionary<string, TrackSegment> _segmentsById;
    private readonly Dictionary<TrackState, List<DirectedTrackEdge>> _staticOutgoing;
    private readonly Dictionary<TrackState, IReadOnlyList<DirectedTrackEdge>> _staticSwitchOptions;

    private TrackLayout(
        Dictionary<string, TrackSegment> segmentsById,
        Dictionary<TrackState, List<DirectedTrackEdge>> staticOutgoing,
        Dictionary<TrackState, IReadOnlyList<DirectedTrackEdge>> staticSwitchOptions,
        IReadOnlyList<Turntable> turntables
    ) {
        _segmentsById = segmentsById;
        _staticOutgoing = staticOutgoing;
        _staticSwitchOptions = staticSwitchOptions;
        this.Turntables = turntables;
    }

    public IReadOnlyDictionary<string, TrackSegment> Segments => _segmentsById;

    /// <summary>
    /// A mapping for states that have multiple static outgoing options (i.e., classic switches).
    /// The options are in deterministic order.
    /// </summary>
    public IReadOnlyDictionary<TrackState, IReadOnlyList<DirectedTrackEdge>> StaticSwitchOptions => _staticSwitchOptions;

    public IReadOnlyList<Turntable> Turntables { get; }

    public static TrackLayout Create(IEnumerable<TrackSegment> segments, IEnumerable<Turntable>? turntables = null) {
        if (segments is null)
            throw new ArgumentNullException(nameof(segments));

        var segmentList = segments.ToList();
        var turntableList = (turntables ?? Array.Empty<Turntable>()).ToList();

        var segmentsById = new Dictionary<string, TrackSegment>(StringComparer.Ordinal);
        foreach (var segment in segmentList) {
            if (segmentsById.ContainsKey(segment.Id))
                throw new ArgumentException($"Duplicate segment id '{segment.Id}'.", nameof(segments));
            segmentsById.Add(segment.Id, segment);
        }

        // Basic validation for turntables.
        var turntablesById = new Dictionary<string, Turntable>(StringComparer.Ordinal);
        foreach (var tt in turntableList) {
            if (turntablesById.ContainsKey(tt.Id))
                throw new ArgumentException($"Duplicate turntable id '{tt.Id}'.", nameof(turntables));
            turntablesById.Add(tt.Id, tt);
        }

        foreach (var tt in turntableList) {
            foreach (var segment in segmentList) {
                if (tt.ContainsStrictly(segment.A) || tt.ContainsStrictly(segment.B))
                    throw new ArgumentException($"Segment '{segment.Id}' lies inside turntable '{tt.Id}'.", nameof(segments));
            }
        }

        // Build static outgoing edges.
        var staticOutgoing = new Dictionary<TrackState, List<DirectedTrackEdge>>();
        foreach (var segment in segmentList) {
            foreach (var edge in segment.GetDirectedEdges()) {
                ValidateEdge(edge);

                var key = edge.FromState;
                if (!staticOutgoing.TryGetValue(key, out var list)) {
                    list = new List<DirectedTrackEdge>();
                    staticOutgoing.Add(key, list);
                }

                list.Add(edge);
            }
        }

        // Deterministic ordering and switch identification.
        var staticSwitchOptions = new Dictionary<TrackState, IReadOnlyList<DirectedTrackEdge>>();
        foreach (var kvp in staticOutgoing) {
            var state = kvp.Key;
            var list = kvp.Value;

            list.Sort(EdgeComparer.Instance);
            if (list.Count > 1) {
                staticSwitchOptions.Add(state, list.ToArray());
                if (list.Count > 3)
                    throw new ArgumentException($"Switch at {state} has too many options ({list.Count}).");
            }
        }

        return new TrackLayout(segmentsById, staticOutgoing, staticSwitchOptions, turntableList);
    }

    /// <summary>
    /// Returns outgoing edges from the specified state, including turntable edges (based on current turntable alignment).
    /// </summary>
    public IReadOnlyList<DirectedTrackEdge> GetOutgoingEdges(
        TrackState state,
        IReadOnlyDictionary<string, int> turntableStates
    ) {
        if (turntableStates is null)
            throw new ArgumentNullException(nameof(turntableStates));

        var result = new List<DirectedTrackEdge>();

        if (_staticOutgoing.TryGetValue(state, out var statics))
            result.AddRange(statics);

        foreach (var tt in this.Turntables) {
            if (!turntableStates.TryGetValue(tt.Id, out int alignment))
                alignment = 0;

            foreach (var edge in tt.GetDirectedEdgesForAlignment(alignment)) {
                if (edge.FromState.Equals(state))
                    result.Add(edge);
            }
        }

        result.Sort(EdgeComparer.Instance);
        return result;
    }

    public bool IsKnownSegment(string segmentId) => _segmentsById.ContainsKey(segmentId) || segmentId.StartsWith("Turntable:", StringComparison.Ordinal);

    private static void ValidateEdge(DirectedTrackEdge edge) {
        if (string.IsNullOrWhiteSpace(edge.SegmentId))
            throw new ArgumentException("Edge segment id must be non-empty.");
        if (edge.FromNode == edge.ToNode)
            throw new ArgumentException($"Edge '{edge.SegmentId}' must connect distinct nodes.");
    }

    internal sealed class EdgeComparer : IComparer<DirectedTrackEdge> {
        internal static readonly EdgeComparer Instance = new();

        public int Compare(DirectedTrackEdge x, DirectedTrackEdge y) {
            // Prefer straight edges (preserve heading) first to make switch indices stable across common layouts,
            // then by exit heading, then by segment id.
            bool xStraight = x.EntryHeading == x.ExitHeading;
            bool yStraight = y.EntryHeading == y.ExitHeading;
            if (xStraight != yStraight)
                return yStraight.CompareTo(xStraight);

            int heading = x.ExitHeading.CompareTo(y.ExitHeading);
            if (heading != 0)
                return heading;

            return string.CompareOrdinal(x.SegmentId, y.SegmentId);
        }
    }
}
