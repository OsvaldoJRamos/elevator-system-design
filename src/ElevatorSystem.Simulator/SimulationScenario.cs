using ElevatorSystem.Core;

namespace ElevatorSystem.Simulator;

/// <summary>
/// A request that arrives some time after the one before it.
/// </summary>
/// <param name="After">How long to wait before submitting it.</param>
/// <param name="Request">What to submit.</param>
public sealed record ScheduledRequest(TimeSpan After, ElevatorRequest Request);

/// <summary>
/// A scripted sequence of passengers, so that a run of the simulator is reproducible and can be
/// read against the log it produces.
/// </summary>
/// <param name="Name">What to call this scenario in the log.</param>
/// <param name="StartingFloor">Where the car is parked when the scenario begins.</param>
/// <param name="Steps">The passengers, in the order they arrive.</param>
public sealed record SimulationScenario(
    string Name,
    int StartingFloor,
    IReadOnlyList<ScheduledRequest> Steps)
{
    /// <summary>
    /// Gets the scenario the simulator runs by default.
    /// </summary>
    /// <remarks>
    /// Chosen to show the cost of FIFO rather than to flatter it. The car is called to floor 9,
    /// and while it is on its way someone on floor 3 asks to go up — a floor it passes without
    /// stopping, because that request arrived second. A LOOK scheduler would have collected them
    /// on the way; see ADR 0002.
    /// </remarks>
    public static SimulationScenario MorningRush { get; } = new(
        Name: "Morning rush",
        StartingFloor: 1,
        Steps:
        [
            new ScheduledRequest(TimeSpan.Zero, new PickupRequest(9, Direction.Down)),
            new ScheduledRequest(TimeSpan.FromMilliseconds(300), new PickupRequest(3, Direction.Up)),
            new ScheduledRequest(TimeSpan.FromMilliseconds(200), new DestinationRequest(6)),
            new ScheduledRequest(TimeSpan.FromMilliseconds(500), new DestinationRequest(42)),
            new ScheduledRequest(TimeSpan.FromMilliseconds(100), new PickupRequest(2, Direction.Up)),
        ]);
}
