namespace ElevatorSystem.Core;

/// <summary>
/// What the elevator system looked like at one instant.
/// </summary>
/// <remarks>
/// This is a value, not a window onto live state. A caller holding a snapshot can read every
/// field knowing they describe the same moment, which is what makes observation safe while the
/// car is being driven on another thread.
/// </remarks>
/// <param name="CurrentFloor">The floor the car was at.</param>
/// <param name="State">What the car was doing.</param>
/// <param name="TargetFloors">The floors it still owed a visit, in service order.</param>
/// <param name="PendingRequestCount">
/// How many admitted requests had not yet been handed to the car.
/// </param>
public sealed record ElevatorSnapshot(
    int CurrentFloor,
    ElevatorState State,
    IReadOnlyList<int> TargetFloors,
    int PendingRequestCount);
