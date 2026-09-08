using Microsoft.Extensions.Time.Testing;

namespace ElevatorSystem.Core.ConcurrencyTests.Support;

/// <summary>
/// A complete elevator system wired to a controllable clock, built for driving hard rather than
/// for asserting on one transition at a time.
/// </summary>
internal sealed class LoadHarness
{
    private static readonly TimeSpan TickDuration = TimeSpan.FromMilliseconds(100);

    private readonly FakeTimeProvider _time = new();

    public LoadHarness()
    {
        ElevatorOptions options = ElevatorOptions.Default with
        {
            FloorTravelTime = TickDuration,
            DoorOpenDuration = TickDuration,
            StuckTimeout = TimeSpan.FromHours(1),
        };

        Elevator elevator = new(options, _time, new FifoSchedulingStrategy());
        Controller = new ElevatorController(elevator, new StuckElevatorWatchdog(options.StuckTimeout, _time));
    }

    public ElevatorController Controller { get; }

    /// <summary>
    /// Gets the floors this system serves, so a load generator can stay inside them.
    /// </summary>
    public FloorRange Floors => Controller.ServedFloors;

    /// <summary>
    /// Drives the system until it has served everything, and reports how many steps that took.
    /// </summary>
    /// <param name="stepBudget">A ceiling, so a defect stops the test rather than the machine.</param>
    /// <returns>The number of steps taken.</returns>
    public int DrainEverything(int stepBudget = 100_000)
    {
        for (int step = 1; step <= stepBudget; step++)
        {
            Controller.ProcessRequests();

            ElevatorSnapshot snapshot = Controller.GetSnapshot();
            if (snapshot.State is ElevatorState.Idle
                && snapshot.TargetFloors.Count == 0
                && snapshot.PendingRequestCount == 0)
            {
                return step;
            }

            _time.Advance(TickDuration);
        }

        throw new InvalidOperationException(
            $"The system had not settled after {stepBudget} steps.");
    }
}
