namespace Trains.Puzzle;

public readonly record struct VehicleCoupling(int OtherVehicleId, VehicleEnd OtherEnd);

public sealed class VehicleCouplings {
    public VehicleCoupling? Back { get; set; }
    public VehicleCoupling? Front { get; set; }
}
