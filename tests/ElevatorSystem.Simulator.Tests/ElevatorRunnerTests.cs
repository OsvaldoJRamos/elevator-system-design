using ElevatorSystem.Core;
using Microsoft.Extensions.Time.Testing;

namespace ElevatorSystem.Simulator.Tests;

/// <summary>
/// The runner is the only thing in the system that owns a thread. What matters about it is that
/// it advances the elevator while it is running, and that stopping it is clean rather than an
/// exception the host has to catch.
/// </summary>
public sealed class ElevatorRunnerTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    private readonly FakeTimeProvider _time = new();
    private readonly SignallingEventSink _sink = new();
    private readonly ElevatorController _controller;
    private readonly ElevatorRunner _runner;

    public ElevatorRunnerTests()
    {
        ElevatorOptions options = ElevatorOptions.Default with
        {
            FloorTravelTime = Interval,
            DoorOpenDuration = Interval,
        };

        Elevator elevator = new(options, _time, new FifoSchedulingStrategy());
        _controller = new ElevatorController(
            elevator,
            new StuckElevatorWatchdog(options.StuckTimeout, _time),
            _sink);

        _runner = new ElevatorRunner(
            _controller,
            _time,
            new SimulationOptions { ProcessingInterval = Interval });
    }

    [Fact]
    public void Constructor_RejectsMissingCollaborators()
    {
        SimulationOptions options = new();

        Action withoutController = () => _ = new ElevatorRunner(null!, _time, options);
        Action withoutClock = () => _ = new ElevatorRunner(_controller, null!, options);
        Action withoutOptions = () => _ = new ElevatorRunner(_controller, _time, null!);

        withoutController.Should().Throw<ArgumentNullException>().WithParameterName("controller");
        withoutClock.Should().Throw<ArgumentNullException>().WithParameterName("timeProvider");
        withoutOptions.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public async Task RunAsync_AdvancesTheElevatorWithoutTheCallerTouchingIt()
    {
        using CancellationTokenSource stop = new();
        _controller.RequestDestination(3);

        Task running = _runner.RunAsync(stop.Token);
        for (int tick = 0; tick < 6; tick++)
        {
            _time.Advance(Interval);
        }

        // The signal is what makes this deterministic; the timeout is only a safety net so that a
        // broken runner fails the test rather than hanging the suite.
        await _sink.DoorsHaveOpened.WaitAsync(TimeSpan.FromSeconds(10));
        await stop.CancelAsync();
        await running;

        _controller.GetSnapshot().CurrentFloor.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_StopsCleanlyWhenCancelled()
    {
        using CancellationTokenSource stop = new();

        Task running = _runner.RunAsync(stop.Token);
        await stop.CancelAsync();

        Func<Task> awaiting = async () => await running;

        await awaiting.Should().NotThrowAsync("a shutdown is not a failure");
        running.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_CancelledBeforeItEverTicks_StillStopsCleanly()
    {
        using CancellationTokenSource stop = new();
        await stop.CancelAsync();

        Func<Task> run = () => _runner.RunAsync(stop.Token);

        await run.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RunAsync_DoesNothingOnceStopped()
    {
        using CancellationTokenSource stop = new();
        Task running = _runner.RunAsync(stop.Token);
        await stop.CancelAsync();
        await running;

        _controller.RequestDestination(8);
        for (int tick = 0; tick < 20; tick++)
        {
            _time.Advance(Interval);
        }

        _controller.GetSnapshot().CurrentFloor.Should().Be(1, "a stopped runner drives nothing");
    }

    /// <summary>Completes a task the first time the doors open, so a test can await progress.</summary>
    private sealed class SignallingEventSink : IElevatorEventSink
    {
        private readonly TaskCompletionSource _doorsHaveOpened =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task DoorsHaveOpened => _doorsHaveOpened.Task;

        public void Publish(ElevatorEvent elevatorEvent)
        {
            if (elevatorEvent is DoorOpened)
            {
                _doorsHaveOpened.TrySetResult();
            }
        }
    }
}
