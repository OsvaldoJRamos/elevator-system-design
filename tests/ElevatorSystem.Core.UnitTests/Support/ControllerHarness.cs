using Microsoft.Extensions.Time.Testing;

namespace ElevatorSystem.Core.UnitTests.Support;

/// <summary>
/// A controller wired to a clock the test controls, driven through the same public surface a
/// real caller would use.
/// </summary>
internal sealed class ControllerHarness
{
    /// <summary>How much simulated time one call to <see cref="Tick"/> consumes.</summary>
    public static readonly TimeSpan TickDuration = TimeSpan.FromSeconds(1);

    private readonly FakeTimeProvider _time = new();

    public ControllerHarness(
        int startingFloor = 1,
        FloorRange? floors = null,
        IElevatorEventSink? eventSink = null,
        IElevatorSchedulingStrategy? schedulingStrategy = null,
        ElevatorOptions? elevatorOptions = null)
    {
        ElevatorOptions options = elevatorOptions ?? ElevatorOptions.Default with
        {
            Floors = floors ?? FloorRange.OneToTen,
            FloorTravelTime = TickDuration,
            DoorOpenDuration = TickDuration,
        };

        Options = options;

        Elevator elevator = new(
            options,
            _time,
            schedulingStrategy ?? new FifoSchedulingStrategy(),
            startingFloor);

        Controller = new ElevatorController(
            elevator,
            new StuckElevatorWatchdog(options.StuckTimeout, _time),
            eventSink);
    }

    public ElevatorController Controller { get; }

    public ElevatorOptions Options { get; }

    /// <summary>Advances the clock without letting the controller react to it.</summary>
    public ControllerHarness AdvanceClock(TimeSpan duration)
    {
        _time.Advance(duration);
        return this;
    }

    /// <summary>
    /// Advances the clock by one tick and lets the controller process whatever became due.
    /// </summary>
    public ControllerHarness Tick(int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            _time.Advance(TickDuration);
            Controller.ProcessRequests();
        }

        return this;
    }

    /// <summary>
    /// Runs until nothing is left to serve, returning the floors at which the doors opened.
    /// </summary>
    public IReadOnlyList<int> RunAndRecordStops(int stepBudget = 500)
    {
        List<int> stops = [];
        ElevatorState previousState = Controller.GetSnapshot().State;

        for (int step = 0; step < stepBudget; step++)
        {
            Controller.ProcessRequests();
            ElevatorSnapshot snapshot = Controller.GetSnapshot();

            if (snapshot.State is ElevatorState.DoorOpen && previousState is not ElevatorState.DoorOpen)
            {
                stops.Add(snapshot.CurrentFloor);
            }

            previousState = snapshot.State;

            if (snapshot.State is ElevatorState.Idle && snapshot.TargetFloors.Count == 0)
            {
                return stops;
            }

            _time.Advance(TickDuration);
        }

        throw new InvalidOperationException(
            $"The elevator did not settle within {stepBudget} steps; it is likely stuck.");
    }
}
