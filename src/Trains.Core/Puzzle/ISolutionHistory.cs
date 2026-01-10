namespace Trains.Puzzle;

/// <summary>
/// Represents puzzle solution version history (RoboZZle-style snapshot history).
/// </summary>
public interface ISolutionHistory : IEnumerable<Solution> {
    /// <summary>
    /// Enumerates solution versions from the start through <see cref="CurrentVersion"/> (inclusive).
    /// Versions after <see cref="CurrentVersion"/> (i.e. redo-able versions) are not enumerated.
    /// </summary>

    /// <summary>
    /// Index of current solution version.
    /// </summary>
    int CurrentVersion { get; }

    /// <summary>
    /// Index of the latest solution version.
    /// </summary>
    int LatestVersion { get; }

    /// <summary>
    /// Current solution snapshot.
    /// </summary>
    Solution CurrentSolution { get; }

    /// <summary>
    /// Adds a solution version to the history. Any versions after the current one are discarded.
    /// </summary>
    void Add(Solution version);

    /// <summary>
    /// Undoes the last edit operation and returns the previous solution version.
    /// </summary>
    Solution Undo();

    /// <summary>
    /// Redoes the last undo operation and returns the restored solution version.
    /// </summary>
    Solution Redo();
}
