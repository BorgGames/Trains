using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Trains.Puzzle.Serialization;

/// <summary>
/// JSON serialization helpers for <see cref="PuzzleSnapshot"/>.
/// </summary>
public static class PuzzleJson {
    public static string Serialize(PuzzleSnapshot snapshot) {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

        var serializer = new DataContractJsonSerializer(typeof(PuzzleSnapshot));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, snapshot);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static PuzzleSnapshot Deserialize(string json) {
        if (json is null)
            throw new ArgumentNullException(nameof(json));

        var serializer = new DataContractJsonSerializer(typeof(PuzzleSnapshot));
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var obj = serializer.ReadObject(ms);
        if (obj is not PuzzleSnapshot snapshot)
            throw new InvalidOperationException("Invalid JSON payload for PuzzleSnapshot.");
        return snapshot;
    }
}

