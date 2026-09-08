namespace ElevatorSystem.Core;

/// <summary>
/// The car's situation at the moment a scheduling decision is made.
/// </summary>
/// <remarks>
/// Passed to <see cref="IElevatorSchedulingStrategy"/> so that an algorithm can take the car's
/// position and direction into account. FIFO ignores both, but an algorithm that did not have
/// them could never be better than FIFO, which would leave the abstraction pointless.
/// </remarks>
/// <param name="CurrentFloor">The floor the car is at.</param>
/// <param name="State">What the car is doing.</param>
public readonly record struct SchedulingContext(int CurrentFloor, ElevatorState State);
