using System.Collections.Concurrent;

namespace ElevatorSystem.Core.UnitTests.Support;

/// <summary>
/// Collects everything the system reports, so a test can assert on the sequence of actions
/// rather than only on the final state.
/// </summary>
internal sealed class RecordingEventSink : IElevatorEventSink
{
    private readonly ConcurrentQueue<ElevatorEvent> _events = new();

    public IReadOnlyList<ElevatorEvent> Events => [.. _events];

    public IReadOnlyList<TEvent> OfType<TEvent>()
        where TEvent : ElevatorEvent => [.. _events.OfType<TEvent>()];

    public void Publish(ElevatorEvent elevatorEvent) => _events.Enqueue(elevatorEvent);
}

/// <summary>A sink that fails on every event, to prove observation cannot break the elevator.</summary>
internal sealed class FaultyEventSink : IElevatorEventSink
{
    public int PublishAttempts { get; private set; }

    public void Publish(ElevatorEvent elevatorEvent)
    {
        PublishAttempts++;
        throw new InvalidOperationException("This sink is broken.");
    }
}
