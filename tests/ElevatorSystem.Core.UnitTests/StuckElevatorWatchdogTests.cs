using Microsoft.Extensions.Time.Testing;

namespace ElevatorSystem.Core.UnitTests;

/// <summary>
/// The watchdog answers one question: has the car stopped making progress for longer than it
/// should have? It is deliberately ignorant of why, which is what lets it catch causes nobody
/// anticipated.
/// </summary>
public sealed class StuckElevatorWatchdogTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly FakeTimeProvider _time = new();
    private readonly StuckElevatorWatchdog _watchdog;

    public StuckElevatorWatchdogTests() => _watchdog = new StuckElevatorWatchdog(Timeout, _time);

    [Fact]
    public void Constructor_RejectsANonPositiveTimeout()
    {
        Action construct = () => _ = new StuckElevatorWatchdog(TimeSpan.Zero, _time);

        construct.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_RejectsAMissingClock()
    {
        Action construct = () => _ = new StuckElevatorWatchdog(Timeout, null!);

        construct.Should().Throw<ArgumentNullException>().WithParameterName("timeProvider");
    }

    [Fact]
    public void ACarThatKeepsMoving_IsNeverReportedAsStalled()
    {
        for (int floor = 1; floor <= 9; floor++)
        {
            _time.Advance(Timeout);
            _watchdog.Observe(floor, ElevatorState.MovingUp, hasWorkPending: true).Should().BeNull();
        }
    }

    [Fact]
    public void AnIdleCarWithNothingToDo_IsNotStalledHoweverLongItSitsThere()
    {
        _watchdog.Observe(1, ElevatorState.Idle, hasWorkPending: false);

        _time.Advance(Timeout * 100);

        _watchdog.Observe(1, ElevatorState.Idle, hasWorkPending: false).Should().BeNull();
    }

    [Fact]
    public void ACarStoppedMidTravel_IsReportedOnceTheTimeoutPasses()
    {
        _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true);

        _time.Advance(Timeout);

        TimeSpan? stalledFor = _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true);

        stalledFor.Should().Be(Timeout);
    }

    [Fact]
    public void ACarStoppedMidTravel_IsNotReportedBeforeTheTimeoutPasses()
    {
        _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true);

        _time.Advance(Timeout - TimeSpan.FromMilliseconds(1));

        _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true).Should().BeNull();
    }

    [Fact]
    public void AnIdleCarWithWorkWaiting_IsStalled()
    {
        // A car that will not depart is as broken as one that will not arrive.
        _watchdog.Observe(1, ElevatorState.Idle, hasWorkPending: true);

        _time.Advance(Timeout);

        _watchdog.Observe(1, ElevatorState.Idle, hasWorkPending: true).Should().NotBeNull();
    }

    [Fact]
    public void ADoorThatNeverCloses_IsStalled()
    {
        _watchdog.Observe(6, ElevatorState.DoorOpen, hasWorkPending: false);

        _time.Advance(Timeout);

        _watchdog.Observe(6, ElevatorState.DoorOpen, hasWorkPending: false).Should().NotBeNull();
    }

    [Fact]
    public void AStallIsReportedOnlyOnce()
    {
        _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true);
        _time.Advance(Timeout);
        _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true).Should().NotBeNull();

        _time.Advance(Timeout);

        _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true)
            .Should().BeNull("a stall is an event, not a condition to be re-reported every step");
    }

    [Fact]
    public void ProgressAfterAStall_ArmsTheWatchdogAgain()
    {
        _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true);
        _time.Advance(Timeout);
        _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true).Should().NotBeNull();

        _watchdog.Observe(5, ElevatorState.MovingUp, hasWorkPending: true).Should().BeNull();
        _time.Advance(Timeout);

        _watchdog.Observe(5, ElevatorState.MovingUp, hasWorkPending: true).Should().NotBeNull();
    }

    [Fact]
    public void AStateChangeCountsAsProgressEvenWithoutMovement()
    {
        _watchdog.Observe(6, ElevatorState.MovingUp, hasWorkPending: true);
        _time.Advance(Timeout - TimeSpan.FromSeconds(1));

        _watchdog.Observe(6, ElevatorState.DoorOpen, hasWorkPending: false);
        _time.Advance(TimeSpan.FromSeconds(2));

        _watchdog.Observe(6, ElevatorState.DoorOpen, hasWorkPending: false).Should().BeNull();
    }

    [Fact]
    public void Reset_ArmsTheWatchdogFromScratch()
    {
        _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true);
        _time.Advance(Timeout);
        _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true).Should().NotBeNull();

        _watchdog.Reset();
        _time.Advance(Timeout - TimeSpan.FromSeconds(1));

        _watchdog.Observe(4, ElevatorState.MovingUp, hasWorkPending: true).Should().BeNull();
    }
}
