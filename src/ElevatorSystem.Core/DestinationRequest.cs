namespace ElevatorSystem.Core;

/// <summary>
/// A passenger already inside the car, asking to be taken to a floor.
/// </summary>
/// <param name="Floor">The floor the passenger wants to reach.</param>
public sealed record DestinationRequest(int Floor) : ElevatorRequest(Floor);
