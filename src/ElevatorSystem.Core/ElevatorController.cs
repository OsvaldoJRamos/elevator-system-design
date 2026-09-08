using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

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
/// A car that stops making progress is withdrawn from service rather than driven blindly. The
/// withdrawal is reported, new requests are refused with a reason a caller can act on, and
/// <see cref="ReturnToService"/> is the deliberate human act that undoes it.
/// </para>
/// <para>
/// Observers never read the car's live fields. After each processing cycle the controller
/// publishes an immutable <see cref="ElevatorSnapshot"/>, and <see cref="GetSnapshot"/> reads that
/// reference without taking a lock at all — reference assignment is atomic, and
/// <see cref="Volatile"/> supplies the visibility. A reader can never observe a half-updated state.
/// </para>
/// </remarks>
public sealed class ElevatorController
{
    private readonly Elevator _elevator;
    private readonly StuckElevatorWatchdog _watchdog;
    private readonly IElevatorEventSink _eventSink;
    private readonly ConcurrentQueue<ElevatorRequest> _admittedRequests = new();
    private readonly Lock _processingGate = new();

    private ElevatorSnapshot _publishedSnapshot;

    // Read by admitting threads outside the gate, so the write has to be visible to them.
    private volatile bool _isOutOfService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElevatorController"/> class.
    /// </summary>
    /// <param name="elevator">The car this controller drives. It takes sole ownership of it.</param>
    /// <param name="watchdog">Detects a car that has stopped making progress.</param>
    /// <param name="eventSink">
    /// Where to report what the system does. Defaults to discarding everything.
    /// </param>
    /// <exception cref="ArgumentNullException">A required collaborator was not supplied.</exception>
    public ElevatorController(
        Elevator elevator,
        StuckElevatorWatchdog watchdog,
        IElevatorEventSink? eventSink = null)
    {
        ArgumentNullException.ThrowIfNull(elevator);
        ArgumentNullException.ThrowIfNull(watchdog);

        _elevator = elevator;
        _watchdog = watchdog;
        _eventSink = eventSink ?? NullElevatorEventSink.Instance;
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
        PickupRequest request = new(floor, direction);

        return Enum.IsDefined(direction)
            ? Admit(request)
            : Refuse(
                request,
                RequestRejectionReason.UnknownDirection,
                $"'{direction}' is not a direction the elevator understands.");
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
    /// performs at most one transition per step regardless of how often it is asked. A failure
    /// while scheduling one request or while advancing the car is reported rather than thrown, so
    /// that a single fault cannot take the system down or discard the rest of the queue.
    /// </remarks>
    public void ProcessRequests()
    {
        lock (_processingGate)
        {
            if (_isOutOfService)
            {
                return;
            }

            DrainAdmittedRequests();
            AdvanceCar();
            WithdrawFromServiceIfStalled();

            Volatile.Write(ref _publishedSnapshot, CaptureCurrentState());
        }
    }

    /// <summary>
    /// Puts a withdrawn car back into service.
    /// </summary>
    /// <remarks>
    /// This is the engineer's act, not the system's: nothing returns a car to service
    /// automatically, because nothing here can know whether the fault was fixed. Work admitted
    /// before the fault is kept — a passenger who was waiting before it broke is still waiting
    /// afterwards. Calling this on a car that is already in service does nothing.
    /// </remarks>
    public void ReturnToService()
    {
        lock (_processingGate)
        {
            if (!_isOutOfService)
            {
                return;
            }

            _isOutOfService = false;
            _watchdog.Reset();

            Report(new ElevatorReturnedToService());
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
        if (_isOutOfService)
        {
            return Refuse(
                request,
                RequestRejectionReason.ElevatorOutOfService,
                "The elevator has been withdrawn from service and is not accepting requests.");
        }

        FloorRange servedFloors = _elevator.ServedFloors;

        if (!servedFloors.Contains(request.Floor))
        {
            return Refuse(
                request,
                RequestRejectionReason.FloorOutOfRange,
                $"Floor {request.Floor} does not exist in this building, which serves floors " +
                $"{servedFloors.Lowest} to {servedFloors.Highest}.");
        }

        _admittedRequests.Enqueue(request);
        Report(new RequestAdmitted(request));
        return RequestResult.Accepted;
    }

    private RequestResult Refuse(ElevatorRequest request, RequestRejectionReason reason, string detail)
    {
        Report(new RequestRejected(request, reason, detail));
        return RequestResult.Rejected(reason, detail);
    }

    private void WithdrawFromServiceIfStalled()
    {
        bool hasWorkPending = _elevator.TargetFloors.Count > 0 || !_admittedRequests.IsEmpty;

        if (_watchdog.Observe(_elevator.CurrentFloor, _elevator.State, hasWorkPending)
            is not TimeSpan stalledFor)
        {
            return;
        }

        _isOutOfService = true;
        Report(new ElevatorStalled(_elevator.CurrentFloor, _elevator.State, stalledFor));
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "One malformed request must not discard the rest of the queue. The failure " +
                        "is reported through the event sink rather than swallowed.")]
    private void DrainAdmittedRequests()
    {
        while (_admittedRequests.TryDequeue(out ElevatorRequest? request))
        {
            try
            {
                _elevator.AddRequest(request);
            }
            catch (Exception failure)
            {
                Report(new RequestSchedulingFailed(request, failure));
            }
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A fault in the car is reported and leaves the system observable, so that a " +
                        "watchdog and an operator can see it, rather than tearing down the host.")]
    private void AdvanceCar()
    {
        int floorBefore = _elevator.CurrentFloor;
        ElevatorState stateBefore = _elevator.State;

        try
        {
            _elevator.Step();
        }
        catch (Exception failure)
        {
            Report(new ElevatorStepFailed(failure));
            return;
        }

        ReportWhatChanged(floorBefore, stateBefore);
    }

    private void ReportWhatChanged(int floorBefore, ElevatorState stateBefore)
    {
        int floorAfter = _elevator.CurrentFloor;
        ElevatorState stateAfter = _elevator.State;

        bool wasTravelling = stateBefore is ElevatorState.MovingUp or ElevatorState.MovingDown;
        bool isTravelling = stateAfter is ElevatorState.MovingUp or ElevatorState.MovingDown;

        if (!wasTravelling && isTravelling)
        {
            Direction direction = stateAfter is ElevatorState.MovingUp ? Direction.Up : Direction.Down;
            Report(new ElevatorDeparted(floorBefore, direction));
        }

        if (floorAfter != floorBefore)
        {
            Report(new ElevatorMoved(floorBefore, floorAfter));
        }

        if (stateBefore is not ElevatorState.DoorOpen && stateAfter is ElevatorState.DoorOpen)
        {
            Report(new DoorOpened(floorAfter));
        }
        else if (stateBefore is ElevatorState.DoorOpen && stateAfter is not ElevatorState.DoorOpen)
        {
            Report(new DoorClosed(floorAfter));
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Observation must never break the thing it observes. A sink that throws is " +
                        "a defect in the sink, and there is by definition nowhere left to report it.")]
    private void Report(ElevatorEvent elevatorEvent)
    {
        try
        {
            _eventSink.Publish(elevatorEvent);
        }
        catch
        {
            // Deliberately ignored: see the justification above.
        }
    }

    private ElevatorSnapshot CaptureCurrentState() => new(
        _elevator.CurrentFloor,
        _elevator.State,
        _elevator.TargetFloors,
        _admittedRequests.Count,
        _isOutOfService);
}
