using ElevatorSystem.Core.UnitTests.Support;

namespace ElevatorSystem.Core.UnitTests;

public sealed class ElevatorControllerTests
{
    [Fact]
    public void Constructor_RejectsMissingCollaborators()
    {
        StuckElevatorWatchdog watchdog = new(TimeSpan.FromSeconds(30), TimeProvider.System);
        Elevator elevator = new(ElevatorOptions.Default, TimeProvider.System, new FifoSchedulingStrategy());

        Action withoutElevator = () => _ = new ElevatorController(null!, watchdog);
        Action withoutWatchdog = () => _ = new ElevatorController(elevator, null!);

        withoutElevator.Should().Throw<ArgumentNullException>().WithParameterName("elevator");
        withoutWatchdog.Should().Throw<ArgumentNullException>().WithParameterName("watchdog");
    }

    [Fact]
    public void RequestElevator_AcceptsAFloorInTheBuilding()
    {
        ControllerHarness harness = new();

        RequestResult result = harness.Controller.RequestElevator(7, Direction.Down);

        result.IsAccepted.Should().BeTrue();
        result.RejectionReason.Should().BeNull();
        result.RejectionDetail.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-4)]
    [InlineData(int.MaxValue)]
    public void RequestElevator_RejectsAFloorOutsideTheBuildingInsteadOfThrowing(int floor)
    {
        ControllerHarness harness = new();

        RequestResult result = harness.Controller.RequestElevator(floor, Direction.Up);

        result.IsAccepted.Should().Be(false);
        result.RejectionReason.Should().Be(RequestRejectionReason.FloorOutOfRange);
        result.RejectionDetail.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void RequestDestination_RejectsAFloorOutsideTheBuilding(int floor)
    {
        ControllerHarness harness = new();

        RequestResult result = harness.Controller.RequestDestination(floor);

        result.IsAccepted.Should().BeFalse();
    }

    [Fact]
    public void RequestElevator_RejectsAnUndefinedDirection()
    {
        ControllerHarness harness = new();

        RequestResult result = harness.Controller.RequestElevator(5, (Direction)42);

        result.IsAccepted.Should().BeFalse();
    }

    [Fact]
    public void ARejectedRequest_IsNotQueued()
    {
        ControllerHarness harness = new();

        harness.Controller.RequestDestination(99);
        harness.Controller.ProcessRequests();

        harness.Controller.GetSnapshot().TargetFloors.Should().BeEmpty();
        harness.Controller.GetSnapshot().PendingRequestCount.Should().Be(0);
    }

    [Fact]
    public void AnAcceptedRequest_IsNotHandedToTheElevatorUntilRequestsAreProcessed()
    {
        ControllerHarness harness = new();

        harness.Controller.RequestDestination(5);

        ElevatorSnapshot beforeProcessing = harness.Controller.GetSnapshot();
        beforeProcessing.PendingRequestCount.Should().Be(1, "the request is admitted but not yet scheduled");
        beforeProcessing.TargetFloors.Should().BeEmpty();
    }

    [Fact]
    public void ProcessRequests_HandsQueuedRequestsToTheElevatorAndAdvancesIt()
    {
        ControllerHarness harness = new();
        harness.Controller.RequestDestination(4);

        harness.Controller.ProcessRequests();

        ElevatorSnapshot snapshot = harness.Controller.GetSnapshot();
        snapshot.PendingRequestCount.Should().Be(0);
        snapshot.TargetFloors.Should().Equal(4);
        snapshot.State.Should().Be(ElevatorState.MovingUp);
    }

    [Fact]
    public void ProcessRequests_WithNothingToDo_IsHarmless()
    {
        ControllerHarness harness = new();

        Action process = () => harness.Tick(5);

        process.Should().NotThrow();
        harness.Controller.GetSnapshot().State.Should().Be(ElevatorState.Idle);
    }

    [Fact]
    public void TheControllerServesRequestsEndToEnd()
    {
        ControllerHarness harness = new();
        harness.Controller.RequestElevator(6, Direction.Down);
        harness.Controller.RequestDestination(2);

        IReadOnlyList<int> stops = harness.RunAndRecordStops();

        stops.Should().Equal(6, 2);
    }

    [Fact]
    public void GetSnapshot_ReportsTheCarsPositionAndState()
    {
        ControllerHarness harness = new(startingFloor: 3);

        ElevatorSnapshot snapshot = harness.Controller.GetSnapshot();

        snapshot.CurrentFloor.Should().Be(3);
        snapshot.State.Should().Be(ElevatorState.Idle);
        snapshot.TargetFloors.Should().BeEmpty();
        snapshot.PendingRequestCount.Should().Be(0);
    }

    [Fact]
    public void GetSnapshot_ReturnsAValueThatDoesNotChangeUnderTheCaller()
    {
        ControllerHarness harness = new();
        harness.Controller.RequestDestination(5);
        harness.Controller.ProcessRequests();

        ElevatorSnapshot taken = harness.Controller.GetSnapshot();
        harness.Tick(4);

        taken.CurrentFloor.Should().Be(1, "a snapshot is a value, not a window onto live state");
        taken.TargetFloors.Should().Equal(5);
        harness.Controller.GetSnapshot().CurrentFloor.Should().NotBe(1);
    }

    [Fact]
    public void ServedFloors_ExposesTheBuildingTheControllerValidatesAgainst()
    {
        ControllerHarness harness = new(floors: new FloorRange(1, 3));

        harness.Controller.ServedFloors.Should().Be(new FloorRange(1, 3));
        harness.Controller.RequestDestination(4).IsAccepted.Should().BeFalse();
    }
}
