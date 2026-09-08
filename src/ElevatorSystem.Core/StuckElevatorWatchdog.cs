namespace ElevatorSystem.Core;

/// <summary>
/// Watches for a car that has stopped making progress.
/// </summary>
/// <remarks>
/// <para>
/// The watchdog answers one question — has anything changed recently enough? — and is
/// deliberately ignorant of why it might not have. A seized motor, a door that will not close and
/// a scheduler that refuses to choose all look identical from here, which is precisely what lets
/// it catch causes nobody anticipated.
/// </para>
/// <para>
/// Progress means the car changed floor or changed state. An idle car with nothing to do is not
/// stalled, however long it sits there; an idle car with passengers waiting is.
/// </para>
/// <para>
/// Not thread-safe. It is owned and called by the controller from inside its processing gate.
/// </para>
/// </remarks>
public sealed class StuckElevatorWatchdog
{
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _timeProvider;

    private long _lastProgressAt;
    private int? _lastFloor;
    private ElevatorState _lastState;
    private bool _stallAlreadyReported;

    /// <summary>
    /// Initializes a new instance of the <see cref="StuckElevatorWatchdog"/> class.
    /// </summary>
    /// <param name="timeout">How long without progress is too long.</param>
    /// <param name="timeProvider">The clock used to measure that.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public StuckElevatorWatchdog(TimeSpan timeout, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        _timeout = timeout;
        _timeProvider = timeProvider;
        _lastProgressAt = timeProvider.GetTimestamp();
    }

    /// <summary>
    /// Records what the car looks like now and reports whether it has stalled.
    /// </summary>
    /// <param name="currentFloor">The floor the car is at.</param>
    /// <param name="state">What the car is doing.</param>
    /// <param name="hasWorkPending">Whether anything is waiting to be served.</param>
    /// <returns>
    /// How long the car has been without progress, the first time that exceeds the timeout;
    /// otherwise <see langword="null"/>. A stall is reported once and not repeated until the car
    /// makes progress again, because it is an event rather than a condition.
    /// </returns>
    public TimeSpan? Observe(int currentFloor, ElevatorState state, bool hasWorkPending)
    {
        bool hasMadeProgress = currentFloor != _lastFloor || state != _lastState;
        bool isIdleWithNothingToDo = state is ElevatorState.Idle && !hasWorkPending;

        if (hasMadeProgress || isIdleWithNothingToDo)
        {
            _lastFloor = currentFloor;
            _lastState = state;
            _lastProgressAt = _timeProvider.GetTimestamp();
            _stallAlreadyReported = false;
            return null;
        }

        if (_stallAlreadyReported)
        {
            return null;
        }

        TimeSpan withoutProgress = _timeProvider.GetElapsedTime(_lastProgressAt);
        if (withoutProgress < _timeout)
        {
            return null;
        }

        _stallAlreadyReported = true;
        return withoutProgress;
    }

    /// <summary>
    /// Forgets everything observed so far and starts measuring again from now.
    /// </summary>
    /// <remarks>
    /// Used when a car is returned to service, so that the fault it was withdrawn for does not
    /// immediately withdraw it again.
    /// </remarks>
    public void Reset()
    {
        _lastFloor = null;
        _lastState = default;
        _lastProgressAt = _timeProvider.GetTimestamp();
        _stallAlreadyReported = false;
    }
}
