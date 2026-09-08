using System.Runtime.CompilerServices;

namespace ElevatorSystem.Core;

/// <summary>
/// The physical characteristics of an elevator: which floors it serves and how long its
/// movements take.
/// </summary>
/// <remarks>
/// Every duration in the system is configured here rather than hard-coded, which is what lets
/// tests drive the elevator through a whole day of traffic in a few microseconds.
/// </remarks>
public sealed record ElevatorOptions
{
    private readonly FloorRange _floors = FloorRange.OneToTen;
    private readonly TimeSpan _floorTravelTime = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _doorOpenDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets the configuration described by the brief: floors 1 to 10, two seconds per floor and
    /// a three second door dwell.
    /// </summary>
    public static ElevatorOptions Default { get; } = new();

    /// <summary>
    /// Gets the floors this elevator serves.
    /// </summary>
    public FloorRange Floors
    {
        get => _floors;
        init => _floors = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets how long the car takes to travel between two adjacent floors.
    /// </summary>
    public TimeSpan FloorTravelTime
    {
        get => _floorTravelTime;
        init => _floorTravelTime = RequirePositive(value);
    }

    /// <summary>
    /// Gets how long the door stays open once the car has arrived.
    /// </summary>
    public TimeSpan DoorOpenDuration
    {
        get => _doorOpenDuration;
        init => _doorOpenDuration = RequirePositive(value);
    }

    private static TimeSpan RequirePositive(TimeSpan value, [CallerMemberName] string? propertyName = null) =>
        value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(propertyName, value, "The duration must be greater than zero.");
}
