using System.Collections;

namespace Trains.Puzzle;

/// <summary>
/// In-memory solution edit history implemented as snapshots (versions).
/// </summary>
public sealed class InMemorySolutionHistory : ISolutionHistory {
    private readonly List<Solution> _history;

    public InMemorySolutionHistory(Solution startingSolution) {
        if (startingSolution is null)
            throw new ArgumentNullException(nameof(startingSolution));

        _history = new List<Solution>(capacity: 1) { startingSolution.Clone() };
    }

    public int CurrentVersion { get; private set; }

    public int LatestVersion => _history.Count - 1;

    public Solution CurrentSolution => _history[this.CurrentVersion].Clone();

    public void Add(Solution version) {
        if (version is null)
            throw new ArgumentNullException(nameof(version));

        Trim();
        _history.Add(version.Clone());
        CurrentVersion++;
    }

    public Solution Undo() {
        if (this.CurrentVersion == 0)
            throw new InvalidOperationException();

        this.CurrentVersion--;
        return this.CurrentSolution;
    }

    public Solution Redo() {
        if (this.CurrentVersion == this.LatestVersion)
            throw new InvalidOperationException();

        this.CurrentVersion++;
        return this.CurrentSolution;
    }

    public SolutionHistorySnapshot ToSnapshot() {
        return new SolutionHistorySnapshot {
            SchemaVersion = SolutionHistorySnapshot.CurrentSchemaVersion,
            CurrentVersion = this.CurrentVersion,
            History = _history.Select(s => SolutionSnapshot.FromSolution(s)).ToList(),
        };
    }

    public static InMemorySolutionHistory FromSnapshot(SolutionHistorySnapshot snapshot) {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.SchemaVersion != SolutionHistorySnapshot.CurrentSchemaVersion)
            throw new NotSupportedException($"Unsupported solution history schema version {snapshot.SchemaVersion}.");
        if (snapshot.History is null)
            throw new ArgumentException("Snapshot history cannot be null.", nameof(snapshot));
        if (snapshot.History.Count == 0)
            throw new ArgumentException("Snapshot must contain at least one version.", nameof(snapshot));
        if (snapshot.CurrentVersion < 0 || snapshot.CurrentVersion >= snapshot.History.Count)
            throw new ArgumentOutOfRangeException(nameof(snapshot.CurrentVersion), snapshot.CurrentVersion, "CurrentVersion is out of range.");

        var solutions = snapshot.History.Select(h => h.ToSolution()).ToList();

        var history = new InMemorySolutionHistory(solutions[0]);
        for (int i = 1; i < solutions.Count; i++)
            history._history.Add(solutions[i].Clone());
        history.CurrentVersion = snapshot.CurrentVersion;
        return history;
    }

    public IEnumerator<Solution> GetEnumerator() {
        for (int version = 0; version <= this.CurrentVersion; version++)
            yield return _history[version].Clone();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Trim() {
        if (this.CurrentVersion < this.LatestVersion) {
            _history.RemoveRange(this.CurrentVersion + 1, this.LatestVersion - this.CurrentVersion);
        }
    }
}
