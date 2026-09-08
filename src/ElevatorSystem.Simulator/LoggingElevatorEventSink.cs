using System.Diagnostics;
using ElevatorSystem.Core;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Simulator;

/// <summary>
/// Writes what the elevator does to the application log.
/// </summary>
/// <remarks>
/// <para>
/// This adapter lives in the simulator rather than in the core, which is the whole point of
/// <see cref="IElevatorEventSink"/>: the domain says what happened, and only this side of the
/// boundary decides that "happened" means a line of structured log output.
/// </para>
/// <para>
/// Every message is a source-generated <see cref="LoggerMessageAttribute"/> method taking the
/// event's fields directly. Nothing is formatted, concatenated or boxed unless the message is
/// actually going to be written, which matters for a sink called on every step of every car.
/// </para>
/// </remarks>
public sealed partial class LoggingElevatorEventSink : IElevatorEventSink
{
    private readonly ILogger<LoggingElevatorEventSink> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingElevatorEventSink"/> class.
    /// </summary>
    /// <param name="logger">Where to write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public LoggingElevatorEventSink(ILogger<LoggingElevatorEventSink> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public void Publish(ElevatorEvent elevatorEvent)
    {
        ArgumentNullException.ThrowIfNull(elevatorEvent);

        switch (elevatorEvent)
        {
            case RequestAdmitted { Request: PickupRequest pickup }:
                LogPickupAccepted(pickup.Floor, pickup.Direction);
                break;

            case RequestAdmitted { Request: DestinationRequest destination }:
                LogDestinationAccepted(destination.Floor);
                break;

            case RequestRejected rejected:
                LogRequestRefused(rejected.Request.Floor, rejected.Reason, rejected.Detail);
                break;

            case ElevatorDeparted departed:
                LogDeparted(departed.FromFloor, departed.Direction);
                break;

            case ElevatorMoved moved:
                LogMoved(moved.FromFloor, moved.ToFloor);
                break;

            case DoorOpened opened:
                LogDoorOpened(opened.Floor);
                break;

            case DoorClosed closed:
                LogDoorClosed(closed.Floor);
                break;

            case ElevatorStalled stalled:
                LogStalled(stalled.Floor, stalled.State, stalled.StalledFor);
                break;

            case ElevatorReturnedToService:
                LogReturnedToService();
                break;

            case RequestSchedulingFailed failed:
                LogSchedulingFailed(failed.Failure, failed.Request.Floor);
                break;

            case ElevatorStepFailed failed:
                LogStepFailed(failed.Failure);
                break;

            default:
                throw new UnreachableException($"Unhandled elevator event '{elevatorEvent.GetType().Name}'.");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Accepted a pickup on floor {Floor} going {Direction}.")]
    private partial void LogPickupAccepted(int floor, Direction direction);

    [LoggerMessage(Level = LogLevel.Information, Message = "Accepted a destination of floor {Floor}.")]
    private partial void LogDestinationAccepted(int floor);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused a request for floor {Floor} ({Reason}): {Detail}")]
    private partial void LogRequestRefused(int floor, RequestRejectionReason reason, string detail);

    [LoggerMessage(Level = LogLevel.Information, Message = "Departing floor {Floor} going {Direction}.")]
    private partial void LogDeparted(int floor, Direction direction);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Passing floor {FromFloor} towards {ToFloor}.")]
    private partial void LogMoved(int fromFloor, int toFloor);

    [LoggerMessage(Level = LogLevel.Information, Message = "Doors open on floor {Floor}.")]
    private partial void LogDoorOpened(int floor);

    [LoggerMessage(Level = LogLevel.Information, Message = "Doors closed on floor {Floor}.")]
    private partial void LogDoorClosed(int floor);

    [LoggerMessage(
        Level = LogLevel.Critical,
        Message = "Stuck on floor {Floor} in state {State} for {StalledFor}. Withdrawing from service.")]
    private partial void LogStalled(int floor, ElevatorState state, TimeSpan stalledFor);

    [LoggerMessage(Level = LogLevel.Information, Message = "Returned to service.")]
    private partial void LogReturnedToService();

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not schedule a request for floor {Floor}.")]
    private partial void LogSchedulingFailed(Exception failure, int floor);

    [LoggerMessage(Level = LogLevel.Error, Message = "The elevator failed while being advanced.")]
    private partial void LogStepFailed(Exception failure);
}
