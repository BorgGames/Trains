using System;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Trains.Puzzle.Serialization;

namespace Trains.Web.Services;

public sealed record PlayPayload(PuzzleStateSnapshot State, int MoveCount);

public sealed class PlayPayloadProtector {
    private readonly IDataProtector _protector;
    private readonly ILogger<PlayPayloadProtector> _log;

    public PlayPayloadProtector(IDataProtectionProvider provider, ILogger<PlayPayloadProtector> log) {
        if (provider is null)
            throw new ArgumentNullException(nameof(provider));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        _protector = provider.CreateProtector("Trains.Web.PlayPayload.v1");
    }

    public string Protect(PlayPayload payload) {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));
        if (payload.MoveCount < 0)
            throw new ArgumentOutOfRangeException(nameof(payload.MoveCount), payload.MoveCount, "Move count must be non-negative.");

        string json = JsonSerializer.Serialize(payload);
        return _protector.Protect(json);
    }

    public bool TryUnprotect(string? protectedPayload, out PlayPayload? payload) {
        payload = null;
        if (string.IsNullOrWhiteSpace(protectedPayload))
            return false;

        try {
            string json = _protector.Unprotect(protectedPayload);
            payload = JsonSerializer.Deserialize<PlayPayload>(json);
            return payload is not null && payload.MoveCount >= 0 && payload.State is not null;
        }
        catch {
            _log.LogDebug("Failed to unprotect play payload.");
            return false;
        }
    }
}
