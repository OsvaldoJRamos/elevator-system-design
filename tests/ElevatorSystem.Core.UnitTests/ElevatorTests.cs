using ElevatorSystem.Core.UnitTests.Support;

namespace ElevatorSystem.Core.UnitTests;

public sealed class ElevatorTests
{
    [Fact]
    public void NewElevator_IsIdleAtItsStartingFloor()
    {
        ElevatorHarness harness = new(startingFloor: 4);

        harness.Elevator.CurrentFloor.Should().Be(4);
        harness.Elevator.State.Should().Be(ElevatorState.Idle);
        harness.Elevator.TargetFloors.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_RejectsAStartingFloorOutsideTheBuilding()
    {
        Action construct = () => _ = new Elevator(ElevatorOptions.Default, TimeProvider.System, startingFloor: 11);

        construct.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("startingFloor");
    }

    [Fact]
    public void Constructor_RejectsMissingCollaborators()
    {
        Action withoutOptions = () => _ = new Elevator(null!, TimeProvider.System);
        Action withoutClock = () => _ = new Elevator(ElevatorOptions.Default, null!);

        withoutOptions.Should().Throw<ArgumentNullException>().WithParameterName("options");
        withoutClock.Should().Throw<ArgumentNullException>().WithParameterName("timeProvider");
    }

    [Fact]
    public void AddRequest_RejectsNull()
    {
        ElevatorHarness harness = new();

        Action add = () => harness.Elevator.AddRequest(null!);

        add.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void AddRequest_RejectsAFloorOutsideTheBuilding(int floor)
    {
        ElevatorHarness harness = new();

        Action add = () => harness.Elevator.AddRequest(new DestinationRequest(floor));

        add.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddRequest_QueuesTheRequestedFloor()
    {
        ElevatorHarness harness = new();

        harness.Elevator.AddRequest(new DestinationRequest(7));

        harness.Elevator.TargetFloors.Should().Equal(7);
    }

    [Fact]
    public void TargetFloors_PreservesArrivalOrder()
    {
        ElevatorHarness harness = new();

        harness.Elevator.AddRequest(new DestinationRequest(9));
        harness.Elevator.AddRequest(new PickupRequest(3, Direction.Down));
        harness.Elevator.AddRequest(new DestinationRequest(6));

        harness.Elevator.TargetFloors.Should().Equal(9, 3, 6);
    }

    [Fact]
    public void TargetFloors_KeepsTheFloorBeingServedAtTheFront()
    {
        ElevatorHarness harness = new();
        harness.Elevator.AddRequest(new DestinationRequest(3));
        harness.Elevator.AddRequest(new DestinationRequest(8));

        harness.Elevator.Step();

        harness.Elevator.TargetFloors.Should().Equal(3, 8);
    }

    [Fact]
    public void TargetFloors_IsASnapshotAndNotALiveView()
    {
        ElevatorHarness harness = new();
        harness.Elevator.AddRequest(new DestinationRequest(5));

        IReadOnlyList<int> before = harness.Elevator.TargetFloors;
        harness.Elevator.AddRequest(new DestinationRequest(2));

        before.Should().Equal(5);
    }

    [Fact]
    public void AddRequest_AcceptsTheCurrentFloor()
    {
        ElevatorHarness harness = new(startingFloor: 5);

        harness.Elevator.AddRequest(new PickupRequest(5, Direction.Up));

        harness.Elevator.TargetFloors.Should().Equal(5);
    }
}
