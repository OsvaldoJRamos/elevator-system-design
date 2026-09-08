namespace ElevatorSystem.Core;

/// <summary>
/// A request for the elevator to visit a floor.
/// </summary>
/// <remarks>
/// The hierarchy is closed: a request is either a <see cref="PickupRequest"/> raised from a
/// landing or a <see cref="DestinationRequest"/> raised from inside the car. Both name a floor,
/// which is all the scheduler needs; the distinction is preserved because it carries intent that
/// a smarter scheduling algorithm would use.
/// </remarks>
/// <param name="Floor">The floor the elevator is asked to visit.</param>
public abstract record ElevatorRequest(int Floor);
