using ElevatorSystem.Core;

namespace ElevatorSystem.Simulator;

/// <summary>
/// Drives the elevator by calling <see cref="ElevatorController.ProcessRequests"/> on a timer.
/// </summary>
/// <remarks>
/// The core deliberately owns no threads: nothing in it makes progress unless something asks it
/// to. This is the something. Keeping it here rather than in the domain is what lets the same
/// controller be driven by a console loop, a background service, or a test calling it by hand.
/// </remarks>
public sealed class ElevatorRunner
{
    private readonly ElevatorController _controller;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _interval;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElevatorRunner"/> class.
    /// </summary>
    /// <param name="controller">The controller to drive.</param>
    /// <param name="timeProvider">The clock the timer runs on.</param>
    /// <param name="options">How often to advance the elevator.</param>
    /// <exception cref="ArgumentNullException">A required collaborator was not supplied.</exception>
    public ElevatorRunner(ElevatorController controller, TimeProvider timeProvider, SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _controller = controller;
        _timeProvider = timeProvider;
        _interval = options.ProcessingInterval;
    }

    /// <summary>
    /// Advances the elevator until cancelled.
    /// </summary>
    /// <param name="cancellationToken">Stops the loop.</param>
    /// <returns>A task that completes once the loop has stopped.</returns>
    /// <remarks>
    /// Cancellation is how this method is meant to end, so it returns normally rather than
    /// throwing: a shutdown is not a failure.
    /// </remarks>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(_interval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                _controller.ProcessRequests();
            }
        }
        catch (OperationCanceledException)
        {
            // Asked to stop. That is not a failure.
        }
    }
}
