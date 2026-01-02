namespace Trains.Puzzle;

/// <summary>
/// Definition of a single piece of rolling stock.
/// </summary>
public abstract class RollingStockSpec {
    protected RollingStockSpec(int id, int length, int weight) {
        if (id < 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "Id must be non-negative.");
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be positive.");
        if (weight < 0)
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weight must be non-negative.");

        this.Id = id;
        this.Length = length;
        this.Weight = weight;
    }

    public int Id { get; }

    /// <summary>
    /// The length of the vehicle in "unit segments" (segments with distance 1).
    /// </summary>
    public int Length { get; }

    public int Weight { get; }

    public virtual int ForwardPower => 0;
    public virtual int BackwardPower => 0;

    public bool IsEngine => this.ForwardPower != 0 || this.BackwardPower != 0;
}

public sealed class EngineSpec : RollingStockSpec {
    public EngineSpec(int id, int length, int weight, int forwardPower, int backwardPower) : base(id, length, weight) {
        if (forwardPower < 0)
            throw new ArgumentOutOfRangeException(nameof(forwardPower), forwardPower, "Forward power must be non-negative.");
        if (backwardPower < 0)
            throw new ArgumentOutOfRangeException(nameof(backwardPower), backwardPower, "Backward power must be non-negative.");

        this.ForwardPowerValue = forwardPower;
        this.BackwardPowerValue = backwardPower;
    }

    private int ForwardPowerValue { get; }
    private int BackwardPowerValue { get; }

    public override int ForwardPower => this.ForwardPowerValue;
    public override int BackwardPower => this.BackwardPowerValue;
}

public sealed class CarSpec : RollingStockSpec {
    public CarSpec(int id, int length, int weight) : base(id, length, weight) { }
}
