namespace ElevatorSystem.Core;

/// <summary>
/// A passenger waiting on a landing, asking to be picked up and carried in a given direction.
/// </summary>
/// <param name="Floor">The floor the passenger is waiting on.</param>
/// <param name="Direction">The direction the passenger intends to travel.</param>
public sealed record PickupRequest(int Floor, Direction Direction) : ElevatorRequest(Floor);
