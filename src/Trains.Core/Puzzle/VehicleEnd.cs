namespace Trains.Puzzle;

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
