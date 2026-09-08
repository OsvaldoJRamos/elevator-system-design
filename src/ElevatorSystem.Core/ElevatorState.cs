namespace ElevatorSystem.Core;

/// <summary>
/// The states an elevator car can be in. Exactly one holds at any moment.
/// </summary>
public enum ElevatorState
{
    /// <summary>Stationary, doors closed, with nothing to serve right now.</summary>
    Idle,

    /// <summary>Travelling towards a higher floor.</summary>
    MovingUp,

    /// <summary>Travelling towards a lower floor.</summary>
    MovingDown,

    /// <summary>Stationary at a floor with the door open, letting passengers in or out.</summary>
    DoorOpen,
}
