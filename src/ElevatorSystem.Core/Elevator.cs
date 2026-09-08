using System.Diagnostics;

namespace ElevatorSystem.Core;

/// <summary>
/// A single elevator car: where it is, what it is doing, and which floors it still owes a visit.
/// </summary>
/// <remarks>
/// <para>
/// This type is deliberately not thread-safe and holds no locks. It is driven by exactly one
/// thread — see <c>ElevatorController</c>, which owns the concurrency — and keeping the state
/// machine single-threaded is what allows it to be read as plain sequential logic.
/// </para>
/// <para>
/// Time enters only through the injected <see cref="TimeProvider"/>. The car makes no progress
/// on its own: a caller advances it by calling <see cref="Step"/>, and each call decides what,
/// if anything, has become due since the current phase began.
/// </para>
/// </remarks>
public sealed class Elevator
{
    private readonly ElevatorOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Queue<ElevatorRequest> _pendingRequests = new();

    private long _phaseStartedAt;
    private int? _floorBeingServed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Elevator"/> class, parked and idle.
    /// </summary>
    /// <param name="options">The floors served and the timings of the car's movements.</param>
    /// <param name="timeProvider">The clock the car reads to decide when a movement is due.</param>
    /// <param name="startingFloor">
    /// The floor the car is parked at. Defaults to the lowest floor of the building.
    /// </param>
    /// <exception cref="ArgumentNullException">A required collaborator was not supplied.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="startingFloor"/> is not a floor of this building.
    /// </exception>
    public Elevator(ElevatorOptions options, TimeProvider timeProvider, int? startingFloor = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        int parkedAt = startingFloor ?? options.Floors.Lowest;
        if (!options.Floors.Contains(parkedAt))
        {
            throw new ArgumentOutOfRangeException(
                nameof(startingFloor),
                parkedAt,
                $"Floor {parkedAt} does not exist in a building of floors {options.Floors.Lowest} to {options.Floors.Highest}.");
        }

        _options = options;
        _timeProvider = timeProvider;
        _phaseStartedAt = timeProvider.GetTimestamp();

        CurrentFloor = parkedAt;
        State = ElevatorState.Idle;
    }

    /// <summary>
    /// Gets the floor the car is currently at.
    /// </summary>
    public int CurrentFloor { get; private set; }

    /// <summary>
    /// Gets what the car is doing right now.
    /// </summary>
    public ElevatorState State { get; private set; }

    /// <summary>
    /// Gets the floors still owed a visit, in the order they will be served: the floor currently
    /// being travelled to first, then everything queued behind it.
    /// </summary>
    /// <remarks>
    /// Each read returns a fresh snapshot, so a caller holding the result is never surprised by
    /// a later request appearing in a list it already inspected.
    /// </remarks>
    public IReadOnlyList<int> TargetFloors
    {
        get
        {
            List<int> floors = new(_pendingRequests.Count + 1);

            if (_floorBeingServed is int servedFloor)
            {
                floors.Add(servedFloor);
            }

            floors.AddRange(_pendingRequests.Select(request => request.Floor));
            return floors;
        }
    }

    /// <summary>
    /// Queues a floor for the car to visit.
    /// </summary>
    /// <param name="request">The pickup or destination request to serve.</param>
    /// <remarks>
    /// A floor already scheduled is not queued twice: two passengers wanting the same floor is
    /// one stop, not two.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The requested floor does not exist in this building. Callers at the system boundary are
    /// expected to have rejected it already.
    /// </exception>
    public void AddRequest(ElevatorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Floors.Contains(request.Floor))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Floor,
                $"Floor {request.Floor} does not exist in a building of floors {_options.Floors.Lowest} to {_options.Floors.Highest}.");
        }

        if (IsAlreadyScheduled(request.Floor))
        {
            return;
        }

        _pendingRequests.Enqueue(request);
    }

    /// <summary>
    /// Moves the car up one floor.
    /// </summary>
    /// <exception cref="InvalidElevatorOperationException">
    /// The car is at the top floor, or its door is open.
    /// </exception>
    public void MoveUp()
    {
        EnsureDoorIsClosed();

        if (CurrentFloor >= _options.Floors.Highest)
        {
            throw new InvalidElevatorOperationException(
                $"The elevator is already at the top floor ({_options.Floors.Highest}) and cannot move up.");
        }

        CurrentFloor++;
        EnterPhase(ElevatorState.MovingUp);
    }

    /// <summary>
    /// Moves the car down one floor.
    /// </summary>
    /// <exception cref="InvalidElevatorOperationException">
    /// The car is at the bottom floor, or its door is open.
    /// </exception>
    public void MoveDown()
    {
        EnsureDoorIsClosed();

        if (CurrentFloor <= _options.Floors.Lowest)
        {
            throw new InvalidElevatorOperationException(
                $"The elevator is already at the bottom floor ({_options.Floors.Lowest}) and cannot move down.");
        }

        CurrentFloor--;
        EnterPhase(ElevatorState.MovingDown);
    }

    /// <summary>
    /// Opens the door. Opening an already open door restarts nothing and is not an error.
    /// </summary>
    /// <exception cref="InvalidElevatorOperationException">The car is moving.</exception>
    public void OpenDoor()
    {
        if (State is ElevatorState.MovingUp or ElevatorState.MovingDown)
        {
            throw new InvalidElevatorOperationException(
                "The door cannot be opened while the elevator is moving.");
        }

        if (State is ElevatorState.DoorOpen)
        {
            return;
        }

        EnterPhase(ElevatorState.DoorOpen);
    }

    /// <summary>
    /// Closes the door, leaving the car idle.
    /// </summary>
    /// <exception cref="InvalidElevatorOperationException">The door is not open.</exception>
    public void CloseDoor()
    {
        if (State is not ElevatorState.DoorOpen)
        {
            throw new InvalidElevatorOperationException(
                $"The door cannot be closed because it is not open; the elevator is {State}.");
        }

        EnterPhase(ElevatorState.Idle);
    }

    /// <summary>
    /// Advances the car by one step, applying whatever the clock says has become due: departing
    /// for the next floor, arriving, or closing a door that has been open long enough.
    /// </summary>
    /// <remarks>
    /// One call performs at most one transition. Calling it more often than the configured
    /// timings simply does nothing, which is what makes the caller's polling interval a
    /// scheduling detail rather than a correctness concern.
    /// </remarks>
    public void Step()
    {
        switch (State)
        {
            case ElevatorState.Idle:
                DepartIfWorkIsWaiting();
                break;

            case ElevatorState.MovingUp:
            case ElevatorState.MovingDown:
                ContinueTravelling();
                break;

            case ElevatorState.DoorOpen:
                CloseDoorOnceItHasBeenOpenLongEnough();
                break;

            default:
                throw new UnreachableException($"Unhandled elevator state '{State}'.");
        }
    }

    private void DepartIfWorkIsWaiting()
    {
        if (_floorBeingServed is null)
        {
            if (_pendingRequests.Count == 0)
            {
                return;
            }

            _floorBeingServed = SelectNextFloorToServe();
        }

        if (_floorBeingServed == CurrentFloor)
        {
            ArriveAtFloorBeingServed();
            return;
        }

        EnterPhase(_floorBeingServed > CurrentFloor ? ElevatorState.MovingUp : ElevatorState.MovingDown);
    }

    private void ContinueTravelling()
    {
        if (ElapsedInCurrentPhase < _options.FloorTravelTime)
        {
            return;
        }

        if (State is ElevatorState.MovingUp)
        {
            MoveUp();
        }
        else
        {
            MoveDown();
        }

        if (CurrentFloor == _floorBeingServed)
        {
            ArriveAtFloorBeingServed();
        }
    }

    private void CloseDoorOnceItHasBeenOpenLongEnough()
    {
        if (ElapsedInCurrentPhase < _options.DoorOpenDuration)
        {
            return;
        }

        CloseDoor();
    }

    private void ArriveAtFloorBeingServed()
    {
        _floorBeingServed = null;

        // The car comes to a stop before its door is allowed to open. Assigning the state
        // directly rather than going through a phase transition keeps arrival a single event:
        // the door dwell is timed from the moment the door opens, not from the moment the car
        // stopped.
        State = ElevatorState.Idle;
        OpenDoor();
    }

    /// <summary>
    /// Chooses which queued floor to serve next.
    /// </summary>
    /// <remarks>
    /// The brief asks for first-in, first-out, so the oldest request wins even when a nearer one
    /// is waiting. Step 3 of the delivery plan lifts this decision out behind an interface, at
    /// which point the trade-off gets its own decision record.
    /// </remarks>
    private int SelectNextFloorToServe() => _pendingRequests.Dequeue().Floor;

    private bool IsAlreadyScheduled(int floor) =>
        _floorBeingServed == floor || _pendingRequests.Any(request => request.Floor == floor);

    private void EnsureDoorIsClosed()
    {
        if (State is ElevatorState.DoorOpen)
        {
            throw new InvalidElevatorOperationException(
                "The elevator cannot move while the door is open.");
        }
    }

    private void EnterPhase(ElevatorState state)
    {
        State = state;
        _phaseStartedAt = _timeProvider.GetTimestamp();
    }

    private TimeSpan ElapsedInCurrentPhase => _timeProvider.GetElapsedTime(_phaseStartedAt);
}
