namespace ElevatorSystem.Simulator;

/// <summary>
/// How the simulation itself is run, as distinct from how the elevator behaves.
/// </summary>
public sealed record SimulationOptions
{
    private readonly TimeSpan _processingInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets how often the runner advances the elevator.
    /// </summary>
    /// <remarks>
    /// A scheduling detail, not a correctness one: the car performs at most one transition per
    /// step regardless of how often it is asked, so polling faster only makes the simulation
    /// smoother.
    /// </remarks>
    public TimeSpan ProcessingInterval
    {
        get => _processingInterval;
        init => _processingInterval = value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The interval must be greater than zero.");
    }

    /// <summary>
    /// Gets how long to wait for the car to finish its work before giving up and shutting down.
    /// </summary>
    public TimeSpan SettleTimeout { get; init; } = TimeSpan.FromMinutes(1);
}
