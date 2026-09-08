namespace ElevatorSystem.Core;

/// <summary>
/// Something the elevator system did, reported for observation.
/// </summary>
/// <remarks>
/// <para>
/// The hierarchy is closed and lives in a single file on purpose: the set of things the system
/// can report is small and finite, and reading it in one place is worth more than the convention
/// of one type per file. A consumer switches over these cases exhaustively.
/// </para>
/// <para>
/// Events carry no timestamp. The moment something is recorded belongs to whoever records it —
/// a logger already stamps its entries, and a simulator running on an accelerated clock would
/// stamp them differently. Baking one in here would force a choice on both.
/// </para>
/// </remarks>
public abstract record ElevatorEvent;

/// <summary>A request passed validation and was queued for scheduling.</summary>
/// <param name="Request">The request that was admitted.</param>
public sealed record RequestAdmitted(ElevatorRequest Request) : ElevatorEvent;

/// <summary>A request was turned away at the boundary.</summary>
/// <param name="Request">The request that was refused.</param>
/// <param name="Reason">The category of refusal.</param>
/// <param name="Detail">A human-readable account of why.</param>
public sealed record RequestRejected(
    ElevatorRequest Request,
    RequestRejectionReason Reason,
    string Detail) : ElevatorEvent;

/// <summary>The car began travelling.</summary>
/// <param name="FromFloor">The floor it set off from.</param>
/// <param name="Direction">The direction it set off in.</param>
public sealed record ElevatorDeparted(int FromFloor, Direction Direction) : ElevatorEvent;

/// <summary>The car travelled one floor.</summary>
/// <param name="FromFloor">The floor it left.</param>
/// <param name="ToFloor">The floor it reached.</param>
public sealed record ElevatorMoved(int FromFloor, int ToFloor) : ElevatorEvent;

/// <summary>The door opened.</summary>
/// <param name="Floor">The floor the car was at.</param>
public sealed record DoorOpened(int Floor) : ElevatorEvent;

/// <summary>The door closed.</summary>
/// <param name="Floor">The floor the car was at.</param>
public sealed record DoorClosed(int Floor) : ElevatorEvent;

/// <summary>Handing a request to the car failed. The remaining requests were still processed.</summary>
/// <param name="Request">The request that could not be scheduled.</param>
/// <param name="Failure">What went wrong.</param>
public sealed record RequestSchedulingFailed(ElevatorRequest Request, Exception Failure) : ElevatorEvent;

/// <summary>Advancing the car failed. The system stayed up so the fault can be observed.</summary>
/// <param name="Failure">What went wrong.</param>
public sealed record ElevatorStepFailed(Exception Failure) : ElevatorEvent;

/// <summary>
/// The car went too long without making progress and has been withdrawn from service.
/// </summary>
/// <param name="Floor">Where it was when it stopped progressing.</param>
/// <param name="State">What it was doing when it stopped progressing.</param>
/// <param name="StalledFor">How long it had been without progress.</param>
public sealed record ElevatorStalled(
    int Floor,
    ElevatorState State,
    TimeSpan StalledFor) : ElevatorEvent;

/// <summary>The car was put back into service after having been withdrawn.</summary>
public sealed record ElevatorReturnedToService : ElevatorEvent;
