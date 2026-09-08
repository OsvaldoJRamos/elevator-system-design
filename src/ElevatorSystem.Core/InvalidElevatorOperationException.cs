namespace ElevatorSystem.Core;

/// <summary>
/// Thrown when an operation is requested that the elevator's current state does not permit,
/// such as opening the door while the car is travelling.
/// </summary>
/// <remarks>
/// This signals a defect in the caller rather than an expected outcome. Conditions a passenger
/// can legitimately produce, such as asking for a floor that does not exist, are reported as
/// results instead.
/// </remarks>
public sealed class InvalidElevatorOperationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidElevatorOperationException"/> class.
    /// </summary>
    public InvalidElevatorOperationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidElevatorOperationException"/> class.
    /// </summary>
    /// <param name="message">A description of the transition that was refused.</param>
    public InvalidElevatorOperationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidElevatorOperationException"/> class.
    /// </summary>
    /// <param name="message">A description of the transition that was refused.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public InvalidElevatorOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
