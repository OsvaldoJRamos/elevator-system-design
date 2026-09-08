namespace ElevatorSystem.Core;

/// <summary>
/// A sink that discards everything it is given.
/// </summary>
/// <remarks>
/// The default for callers that do not care about observation, so that the controller never has
/// to check for a missing sink and no code path is conditional on whether anyone is watching.
/// </remarks>
public sealed class NullElevatorEventSink : IElevatorEventSink
{
    private NullElevatorEventSink()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static NullElevatorEventSink Instance { get; } = new();

    /// <inheritdoc />
    public void Publish(ElevatorEvent elevatorEvent)
    {
        // Nobody is listening.
    }
}
