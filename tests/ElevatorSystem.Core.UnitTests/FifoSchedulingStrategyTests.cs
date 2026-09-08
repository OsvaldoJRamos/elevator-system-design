namespace ElevatorSystem.Core.UnitTests;

public sealed class FifoSchedulingStrategyTests
{
    private static readonly SchedulingContext AnyContext = new(CurrentFloor: 5, State: ElevatorState.Idle);

    private readonly FifoSchedulingStrategy _strategy = new();

    [Fact]
    public void SelectNext_WithNothingPending_ReturnsNull()
    {
        ElevatorRequest? selected = _strategy.SelectNext([], AnyContext);

        selected.Should().BeNull();
    }

    [Fact]
    public void SelectNext_ReturnsTheRequestThatArrivedFirst()
    {
        DestinationRequest first = new(9);
        List<ElevatorRequest> pending = [first, new PickupRequest(2, Direction.Up)];

        ElevatorRequest? selected = _strategy.SelectNext(pending, AnyContext);

        selected.Should().BeSameAs(first);
    }

    [Fact]
    public void SelectNext_IgnoresHowNearTheCarAlreadyIs()
    {
        // The car is sitting on floor 5 and floor 6 is one step away, but floor 9 asked first.
        // This is the defining property of FIFO, and the reason it is not an efficient algorithm.
        List<ElevatorRequest> pending = [new DestinationRequest(9), new DestinationRequest(6)];

        ElevatorRequest? selected = _strategy.SelectNext(pending, new SchedulingContext(5, ElevatorState.Idle));

        selected!.Floor.Should().Be(9);
    }

    [Fact]
    public void SelectNext_IgnoresTheDirectionTheCarIsTravellingIn()
    {
        List<ElevatorRequest> pending = [new DestinationRequest(1), new DestinationRequest(10)];

        ElevatorRequest? selected = _strategy.SelectNext(pending, new SchedulingContext(5, ElevatorState.MovingUp));

        selected!.Floor.Should().Be(1, "arrival order outranks the direction of travel");
    }

    [Fact]
    public void SelectNext_LeavesTheQueueItWasGivenUntouched()
    {
        List<ElevatorRequest> pending = [new DestinationRequest(3), new DestinationRequest(7)];

        _strategy.SelectNext(pending, AnyContext);

        pending.Should().HaveCount(2, "choosing is the strategy's job; removing is the elevator's");
    }

    [Fact]
    public void SelectNext_RejectsAMissingQueue()
    {
        Action selectNext = () => _strategy.SelectNext(null!, AnyContext);

        selectNext.Should().Throw<ArgumentNullException>().WithParameterName("pendingRequests");
    }
}
