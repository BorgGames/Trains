using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Trains.Puzzle;

/// <summary>
/// JSON serialization helpers for <see cref="SolutionHistorySnapshot"/>.
/// </summary>
public static class SolutionHistoryJson {
    public static string Serialize(SolutionHistorySnapshot snapshot) {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

        var serializer = new DataContractJsonSerializer(typeof(SolutionHistorySnapshot));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, snapshot);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static SolutionHistorySnapshot Deserialize(string json) {
        if (json is null)
            throw new ArgumentNullException(nameof(json));

        var serializer = new DataContractJsonSerializer(typeof(SolutionHistorySnapshot));
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var obj = serializer.ReadObject(ms);
        if (obj is not SolutionHistorySnapshot snapshot)
            throw new InvalidOperationException("Invalid JSON payload for SolutionHistorySnapshot.");
        return snapshot;
    }
}

