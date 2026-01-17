using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Trains.Puzzle.Serialization;

/// <summary>
/// JSON serialization helpers for <see cref="PuzzleStateSnapshot"/>.
/// </summary>
public static class PuzzleStateJson {
    public static string Serialize(PuzzleStateSnapshot snapshot) {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

        var serializer = new DataContractJsonSerializer(typeof(PuzzleStateSnapshot));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, snapshot);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static PuzzleStateSnapshot Deserialize(string json) {
        if (json is null)
            throw new ArgumentNullException(nameof(json));

        var serializer = new DataContractJsonSerializer(typeof(PuzzleStateSnapshot));
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var obj = serializer.ReadObject(ms);
        if (obj is not PuzzleStateSnapshot snapshot)
            throw new InvalidOperationException("Invalid JSON payload for PuzzleStateSnapshot.");
        return snapshot;
    }
}

