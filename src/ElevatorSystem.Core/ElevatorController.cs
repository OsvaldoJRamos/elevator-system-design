using System.Collections.Concurrent;

namespace ElevatorSystem.Core;

/// <summary>
/// The public boundary of the elevator system, and the only type in it that more than one thread
/// touches.
/// </summary>
/// <remarks>
/// <para>
/// The design is multi-producer, single-consumer. Any number of threads may call
/// <see cref="RequestElevator"/> and <see cref="RequestDestination"/>; those calls validate the
/// request and hand it to a lock-free queue, so a caller is never blocked by the car, by another
/// caller, or by an observer. Admission latency is therefore bounded by an allocation rather than
/// by contention.
/// </para>
/// <para>
/// <see cref="ProcessRequests"/> is the single consumer. It is serialised by a gate, so the
/// elevator's mutable state is only ever touched by one thread at a time even if a host is
/// careless enough to drive it from several. That is what allows <see cref="Elevator"/> itself to
/// hold no locks and read as ordinary sequential logic.
/// </para>
/// <para>
/// Observers never read the car's live fields. After each processing cycle the controller
/// publishes an immutable <see cref="ElevatorSnapshot"/>, and <see cref="GetSnapshot"/> reads that
/// reference without taking a lock at all — reference assignment is atomic, and
/// <see cref="Volatile"/> supplies the visibility. A reader can never observe a half-updated car.
/// </para>
/// </remarks>
public sealed class ElevatorController
{
    private readonly Elevator _elevator;
    private readonly ConcurrentQueue<ElevatorRequest> _admittedRequests = new();
    private readonly Lock _processingGate = new();

    private ElevatorSnapshot _publishedSnapshot;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElevatorController"/> class.
    /// </summary>
    /// <param name="elevator">The car this controller drives. It takes sole ownership of it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="elevator"/> is <see langword="null"/>.</exception>
    public ElevatorController(Elevator elevator)
    {
        ArgumentNullException.ThrowIfNull(elevator);

        _elevator = elevator;
        _publishedSnapshot = CaptureCurrentState();
    }

    /// <summary>
    /// Gets the floors this system serves.
    /// </summary>
    public FloorRange ServedFloors => _elevator.ServedFloors;

    /// <summary>
    /// Requests the elevator to a landing, for a passenger intending to travel in a given
    /// direction.
    /// </summary>
    /// <param name="floor">The floor the passenger is waiting on.</param>
    /// <param name="direction">The direction the passenger intends to travel.</param>
    /// <returns>Whether the request was admitted, and if not, why.</returns>
    public RequestResult RequestElevator(int floor, Direction direction)
    {
        if (!Enum.IsDefined(direction))
        {
            return RequestResult.Rejected($"'{direction}' is not a direction the elevator understands.");
        }

        return Admit(new PickupRequest(floor, direction));
    }

    /// <summary>
    /// Requests a destination floor on behalf of a passenger already inside the car.
    /// </summary>
    /// <param name="floor">The floor the passenger wants to reach.</param>
    /// <returns>Whether the request was admitted, and if not, why.</returns>
    public RequestResult RequestDestination(int floor) => Admit(new DestinationRequest(floor));

    /// <summary>
    /// Hands every admitted request to the car, advances it by one step, and publishes the
    /// resulting state for observers.
    /// </summary>
    /// <remarks>
    /// Safe to call from any thread and at any interval: calls are serialised, and the car
    /// performs at most one transition per step regardless of how often it is asked.
    /// </remarks>
    public void ProcessRequests()
    {
        lock (_processingGate)
        {
            while (_admittedRequests.TryDequeue(out ElevatorRequest? request))
            {
                _elevator.AddRequest(request);
            }

            _elevator.Step();

            Volatile.Write(ref _publishedSnapshot, CaptureCurrentState());
        }
    }

    /// <summary>
    /// Reads the state of the system as of the last processing cycle.
    /// </summary>
    /// <returns>An immutable description of the car and its outstanding work.</returns>
    /// <remarks>
    /// Never blocks, and never blocks the car. The pending count is read from the ingress queue
    /// at call time so that a request admitted since the last cycle is already visible to the
    /// caller who submitted it.
    /// </remarks>
    public ElevatorSnapshot GetSnapshot() =>
        Volatile.Read(ref _publishedSnapshot) with { PendingRequestCount = _admittedRequests.Count };

    private RequestResult Admit(ElevatorRequest request)
    {
        FloorRange servedFloors = _elevator.ServedFloors;

        if (!servedFloors.Contains(request.Floor))
        {
            return RequestResult.Rejected(
                $"Floor {request.Floor} does not exist in this building, which serves floors " +
                $"{servedFloors.Lowest} to {servedFloors.Highest}.");
        }

        _admittedRequests.Enqueue(request);
        return RequestResult.Accepted;
    }

    private ElevatorSnapshot CaptureCurrentState() => new(
        _elevator.CurrentFloor,
        _elevator.State,
        _elevator.TargetFloors,
        _admittedRequests.Count);
}
