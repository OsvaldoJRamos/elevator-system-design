using Microsoft.Extensions.Time.Testing;

namespace ElevatorSystem.Core.UnitTests.Support;

/// <summary>
/// An elevator wired to a clock the test controls, with round timings so that a test can
/// reason in whole ticks instead of in milliseconds.
/// </summary>
internal sealed class ElevatorHarness
{
    /// <summary>How much simulated time one call to <see cref="Tick"/> consumes.</summary>
    public static readonly TimeSpan TickDuration = TimeSpan.FromSeconds(1);

    private readonly FakeTimeProvider _time = new();

    public ElevatorHarness(
        FloorRange? floors = null,
        int startingFloor = 1,
        IElevatorSchedulingStrategy? schedulingStrategy = null)
    {
        Options = ElevatorOptions.Default with
        {
            Floors = floors ?? FloorRange.OneToTen,
            FloorTravelTime = TickDuration,
            DoorOpenDuration = TickDuration,
        };

        Elevator = new Elevator(
            Options,
            _time,
            schedulingStrategy ?? new FifoSchedulingStrategy(),
            startingFloor);
    }

    public Elevator Elevator { get; }

    public ElevatorOptions Options { get; }

    /// <summary>
    /// Advances the clock by one tick and lets the elevator react to it.
    /// </summary>
    public ElevatorHarness Tick(int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            _time.Advance(TickDuration);
            Elevator.Step();
        }

        return this;
    }

    /// <summary>
    /// Runs the elevator until it has nothing left to serve, returning the floors at which the
    /// doors opened, in order. The step budget stops a defective state machine from hanging the
    /// test run.
    /// </summary>
    public IReadOnlyList<int> RunAndRecordStops(int stepBudget = 500)
    {
        List<int> stops = [];
        ElevatorState previousState = Elevator.State;

        for (int step = 0; step < stepBudget; step++)
        {
            Elevator.Step();

            if (Elevator.State is ElevatorState.DoorOpen && previousState is not ElevatorState.DoorOpen)
            {
                stops.Add(Elevator.CurrentFloor);
            }

            previousState = Elevator.State;

            if (Elevator.State is ElevatorState.Idle && Elevator.TargetFloors.Count == 0)
            {
                return stops;
            }

            _time.Advance(TickDuration);
        }

        throw new InvalidOperationException(
            $"The elevator did not settle within {stepBudget} steps; it is likely stuck.");
    }
}
