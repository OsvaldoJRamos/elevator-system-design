using System.Diagnostics;
using ElevatorSystem.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Simulator;

/// <summary>
/// Plays a scenario against a running elevator, then shuts the application down.
/// </summary>
public sealed partial class SimulationHostedService : BackgroundService
{
    private readonly ElevatorController _controller;
    private readonly ElevatorRunner _runner;
    private readonly SimulationScenario _scenario;
    private readonly SimulationOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<SimulationHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationHostedService"/> class.
    /// </summary>
    /// <param name="controller">The elevator system under simulation.</param>
    /// <param name="runner">Advances the elevator while the scenario plays.</param>
    /// <param name="scenario">The passengers to submit.</param>
    /// <param name="options">Timings of the simulation itself.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="lifetime">Used to stop the application once the scenario is done.</param>
    /// <param name="logger">Where to narrate the run.</param>
    public SimulationHostedService(
        ElevatorController controller,
        ElevatorRunner runner,
        SimulationScenario scenario,
        SimulationOptions options,
        TimeProvider timeProvider,
        IHostApplicationLifetime lifetime,
        ILogger<SimulationHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(logger);

        _controller = controller;
        _runner = runner;
        _scenario = scenario;
        _options = options;
        _timeProvider = timeProvider;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogScenarioStarting(_scenario.Name, _scenario.Steps.Count, _controller.ServedFloors.Lowest, _controller.ServedFloors.Highest);

        using CancellationTokenSource stopTheRunner =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        Task running = _runner.RunAsync(stopTheRunner.Token);
        bool settled;

        try
        {
            await SubmitScenarioAsync(stoppingToken).ConfigureAwait(false);
            settled = await WaitUntilSettledAsync(stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            await stopTheRunner.CancelAsync().ConfigureAwait(false);
            await running.ConfigureAwait(false);
        }

        ElevatorSnapshot finalState = _controller.GetSnapshot();
        SimulationOutcome outcome = SimulationOutcomes.Determine(finalState, settled);

        ReportOutcome(outcome, finalState);
        Environment.ExitCode = outcome.ToExitCode();

        _lifetime.StopApplication();
    }

    private async Task SubmitScenarioAsync(CancellationToken cancellationToken)
    {
        foreach (ScheduledRequest step in _scenario.Steps)
        {
            if (step.After > TimeSpan.Zero)
            {
                await Task.Delay(step.After, _timeProvider, cancellationToken).ConfigureAwait(false);
            }

            Submit(step.Request);
        }
    }

    private void Submit(ElevatorRequest request) => _ = request switch
    {
        PickupRequest pickup => _controller.RequestElevator(pickup.Floor, pickup.Direction),
        DestinationRequest destination => _controller.RequestDestination(destination.Floor),
        _ => throw new UnreachableException($"Unhandled request type '{request.GetType().Name}'."),
    };

    /// <summary>
    /// Waits until the car has nothing left to do, so that the run ends on a complete picture
    /// rather than mid-journey.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the car came to rest; <see langword="false"/> if the wait timed
    /// out. The distinction matters: only one of the two is a successful run.
    /// </returns>
    private async Task<bool> WaitUntilSettledAsync(CancellationToken cancellationToken)
    {
        long startedAt = _timeProvider.GetTimestamp();

        while (!HasSettled())
        {
            if (_timeProvider.GetElapsedTime(startedAt) > _options.SettleTimeout)
            {
                return false;
            }

            await Task.Delay(_options.ProcessingInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private void ReportOutcome(SimulationOutcome outcome, ElevatorSnapshot finalState)
    {
        switch (outcome)
        {
            case SimulationOutcome.Completed:
                LogScenarioFinished(finalState.CurrentFloor);
                break;

            case SimulationOutcome.ElevatorWithdrawn:
                LogElevatorWithdrawn(finalState.CurrentFloor, finalState.TargetFloors.Count);
                break;

            case SimulationOutcome.DidNotSettle:
                LogGaveUpWaiting(_options.SettleTimeout);
                break;

            default:
                throw new UnreachableException($"Unhandled simulation outcome '{outcome}'.");
        }
    }

    private bool HasSettled()
    {
        ElevatorSnapshot snapshot = _controller.GetSnapshot();

        return snapshot.IsOutOfService
            || (snapshot.State is ElevatorState.Idle
                && snapshot.TargetFloors.Count == 0
                && snapshot.PendingRequestCount == 0);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Starting scenario '{Scenario}' with {StepCount} requests, floors {Lowest} to {Highest}.")]
    private partial void LogScenarioStarting(string scenario, int stepCount, int lowest, int highest);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scenario complete. The car is parked on floor {Floor}.")]
    private partial void LogScenarioFinished(int floor);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The car had not settled after {Timeout}; stopping anyway.")]
    private partial void LogGaveUpWaiting(TimeSpan timeout);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Scenario abandoned: the elevator was withdrawn from service on floor {Floor} with {Outstanding} floors still owed.")]
    private partial void LogElevatorWithdrawn(int floor, int outstanding);
}
