using ElevatorSystem.Core.UnitTests.Support;

namespace ElevatorSystem.Core.UnitTests;

public sealed class ElevatorMovementTests
{
    [Fact]
    public void Step_WithNothingQueued_LeavesTheElevatorIdle()
    {
        ElevatorHarness harness = new();

        harness.Tick(5);

        harness.Elevator.State.Should().Be(ElevatorState.Idle);
        harness.Elevator.CurrentFloor.Should().Be(1);
    }

    [Fact]
    public void Step_WithARequestAbove_StartsMovingUp()
    {
        ElevatorHarness harness = new(startingFloor: 2);
        harness.Elevator.AddRequest(new DestinationRequest(6));

        harness.Elevator.Step();

        harness.Elevator.State.Should().Be(ElevatorState.MovingUp);
        harness.Elevator.CurrentFloor.Should().Be(2, "departing does not itself move the car");
    }

    [Fact]
    public void Step_WithARequestBelow_StartsMovingDown()
    {
        ElevatorHarness harness = new(startingFloor: 7);
        harness.Elevator.AddRequest(new PickupRequest(3, Direction.Up));

        harness.Elevator.Step();

        harness.Elevator.State.Should().Be(ElevatorState.MovingDown);
    }

    [Fact]
    public void Step_WithARequestForTheCurrentFloor_OpensTheDoorWithoutMoving()
    {
        ElevatorHarness harness = new(startingFloor: 4);
        harness.Elevator.AddRequest(new PickupRequest(4, Direction.Up));

        harness.Elevator.Step();

        harness.Elevator.State.Should().Be(ElevatorState.DoorOpen);
        harness.Elevator.CurrentFloor.Should().Be(4);
    }

    [Fact]
    public void Step_BeforeTheTravelTimeHasElapsed_DoesNotChangeFloor()
    {
        ElevatorHarness harness = new();
        harness.Elevator.AddRequest(new DestinationRequest(3));
        harness.Elevator.Step();

        harness.Elevator.Step();

        harness.Elevator.CurrentFloor.Should().Be(1);
        harness.Elevator.State.Should().Be(ElevatorState.MovingUp);
    }

    [Fact]
    public void Step_AdvancesOneFloorPerTravelInterval()
    {
        ElevatorHarness harness = new();
        harness.Elevator.AddRequest(new DestinationRequest(4));
        harness.Elevator.Step();

        harness.Tick();
        harness.Elevator.CurrentFloor.Should().Be(2);

        harness.Tick();
        harness.Elevator.CurrentFloor.Should().Be(3);
    }

    [Fact]
    public void Step_OnReachingTheTargetFloor_OpensTheDoor()
    {
        ElevatorHarness harness = new();
        harness.Elevator.AddRequest(new DestinationRequest(3));
        harness.Elevator.Step();

        harness.Tick(2);

        harness.Elevator.CurrentFloor.Should().Be(3);
        harness.Elevator.State.Should().Be(ElevatorState.DoorOpen);
    }

    [Fact]
    public void Step_AfterTheDoorHasBeenOpenLongEnough_ClosesItAndGoesIdle()
    {
        ElevatorHarness harness = new(startingFloor: 2);
        harness.Elevator.AddRequest(new DestinationRequest(2));
        harness.Elevator.Step();

        harness.Tick();

        harness.Elevator.State.Should().Be(ElevatorState.Idle);
        harness.Elevator.TargetFloors.Should().BeEmpty();
    }

    [Fact]
    public void Step_ServesTheWholeQueueAndReturnsToIdle()
    {
        ElevatorHarness harness = new();
        harness.Elevator.AddRequest(new DestinationRequest(3));
        harness.Elevator.AddRequest(new DestinationRequest(5));

        IReadOnlyList<int> stops = harness.RunAndRecordStops();

        stops.Should().Equal(3, 5);
        harness.Elevator.State.Should().Be(ElevatorState.Idle);
        harness.Elevator.CurrentFloor.Should().Be(5);
    }

    [Fact]
    public void Step_ServesRequestsInArrivalOrderEvenWhenThatMeansPassingAFloorTwice()
    {
        // The brief asks for FIFO, which is deliberately not the shortest route: the car
        // travels 1 -> 8, passing floor 3, and only then comes back down for it.
        ElevatorHarness harness = new();
        harness.Elevator.AddRequest(new DestinationRequest(8));
        harness.Elevator.AddRequest(new PickupRequest(3, Direction.Down));

        IReadOnlyList<int> stops = harness.RunAndRecordStops();

        stops.Should().Equal(8, 3);
    }

    [Fact]
    public void Step_DoesNotStopAtAFloorItMerelyPassesThrough()
    {
        ElevatorHarness harness = new();
        harness.Elevator.AddRequest(new DestinationRequest(4));
        harness.Elevator.Step();

        harness.Tick(2);

        harness.Elevator.CurrentFloor.Should().Be(3);
        harness.Elevator.State.Should().Be(ElevatorState.MovingUp);
    }

    [Fact]
    public void Step_IgnoresADuplicateRequestForAFloorAlreadyQueued()
    {
        ElevatorHarness harness = new();
        harness.Elevator.AddRequest(new DestinationRequest(6));
        harness.Elevator.AddRequest(new PickupRequest(6, Direction.Up));

        IReadOnlyList<int> stops = harness.RunAndRecordStops();

        stops.Should().Equal(new[] { 6 }, "a floor already scheduled does not earn a second stop");
    }

    [Fact]
    public void Step_AcceptsARequestArrivingWhileTheCarIsMoving()
    {
        ElevatorHarness harness = new();
        harness.Elevator.AddRequest(new DestinationRequest(3));
        harness.Elevator.Step();
        harness.Tick();

        harness.Elevator.AddRequest(new DestinationRequest(1));
        IReadOnlyList<int> stops = harness.RunAndRecordStops();

        stops.Should().Equal(3, 1);
    }
}
