namespace ElevatorSystem.Core;

/// <summary>
/// Receives what the elevator system reports.
/// </summary>
/// <remarks>
/// <para>
/// This is the domain's only outbound port. It says what happened, never how it should be
/// recorded, which is what keeps the core free of any logging framework — a constraint asserted
/// by a test rather than left to discipline.
/// </para>
/// <para>
/// Implementations are called on the thread that is driving the elevator and should return
/// promptly. A sink that needs to do I/O should buffer and hand off rather than block the car.
/// </para>
/// </remarks>
public interface IElevatorEventSink
{
    /// <summary>
    /// Reports one event.
    /// </summary>
    /// <param name="elevatorEvent">What happened.</param>
    /// <remarks>
    /// An implementation that throws will not break the elevator: the controller isolates sinks
    /// from the system they observe.
    /// </remarks>
    void Publish(ElevatorEvent elevatorEvent);
}
