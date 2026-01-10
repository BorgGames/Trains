namespace Trains.Puzzle;

/// <summary>
/// One of the two ends of a vehicle, relative to the vehicle's own placement orientation.
/// "Front" and "Back" are not compass directions; a vehicle's front can point East, West, etc. depending on how it is placed.
/// </summary>
public enum VehicleEnd {
    Back = 0,
    Front = 1,
}

public static class VehicleEndExtensions {
    public static VehicleEnd Opposite(this VehicleEnd end) =>
        end switch {
            VehicleEnd.Back => VehicleEnd.Front,
            VehicleEnd.Front => VehicleEnd.Back,
            _ => throw new ArgumentOutOfRangeException(nameof(end), end, "Unknown end."),
        };
}
