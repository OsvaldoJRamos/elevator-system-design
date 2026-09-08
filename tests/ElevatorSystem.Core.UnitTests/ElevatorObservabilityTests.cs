using ElevatorSystem.Core.UnitTests.Support;

namespace ElevatorSystem.Core.UnitTests;

/// <summary>
/// What the system reports is part of its contract: the brief asks for a log of elevator actions,
/// and a log nobody asserts on is a log that quietly goes wrong.
/// </summary>
public sealed class ElevatorObservabilityTests
{
    [Fact]
    public void AnAdmittedRequest_IsReported()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(eventSink: sink);

        harness.Controller.RequestDestination(5);

        sink.OfType<RequestAdmitted>().Should().ContainSingle()
            .Which.Request.Should().Be(new DestinationRequest(5));
    }

    [Fact]
    public void ARejectedRequest_IsReportedWithItsReason()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(eventSink: sink);

        harness.Controller.RequestDestination(99);

        RequestRejected rejected = sink.OfType<RequestRejected>().Should().ContainSingle().Subject;
        rejected.Request.Floor.Should().Be(99);
        rejected.Reason.Should().Be(RequestRejectionReason.FloorOutOfRange);
        rejected.Detail.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AnInvalidDirection_IsReportedAsARejection()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(eventSink: sink);

        harness.Controller.RequestElevator(5, (Direction)42);

        sink.OfType<RequestRejected>().Should().ContainSingle();
        sink.OfType<RequestAdmitted>().Should().BeEmpty();
    }

    [Fact]
    public void DepartingIsReportedOnceRatherThanOnEveryStep()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(eventSink: sink);
        harness.Controller.RequestDestination(4);

        harness.RunAndRecordStops();

        sink.OfType<ElevatorDeparted>().Should().ContainSingle()
            .Which.Should().Be(new ElevatorDeparted(FromFloor: 1, Direction.Up));
    }

    [Fact]
    public void EveryFloorTheCarPassesIsReported()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(eventSink: sink);
        harness.Controller.RequestDestination(4);

        harness.RunAndRecordStops();

        sink.OfType<ElevatorMoved>().Should().Equal(
            new ElevatorMoved(1, 2),
            new ElevatorMoved(2, 3),
            new ElevatorMoved(3, 4));
    }

    [Fact]
    public void TheDoorOpeningAndClosingIsReported()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(eventSink: sink);
        harness.Controller.RequestDestination(2);

        harness.RunAndRecordStops();

        sink.OfType<DoorOpened>().Should().Equal(new DoorOpened(2));
        sink.OfType<DoorClosed>().Should().Equal(new DoorClosed(2));
    }

    [Fact]
    public void AFullJourneyIsReportedAsAReadableSequenceOfActions()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(startingFloor: 3, eventSink: sink);
        harness.Controller.RequestElevator(1, Direction.Up);

        harness.RunAndRecordStops();

        sink.Events.Should().Equal(
            new RequestAdmitted(new PickupRequest(1, Direction.Up)),
            new ElevatorDeparted(3, Direction.Down),
            new ElevatorMoved(3, 2),
            new ElevatorMoved(2, 1),
            new DoorOpened(1),
            new DoorClosed(1));
    }

    [Fact]
    public void ADefectiveSink_CannotBreakTheElevator()
    {
        FaultyEventSink sink = new();
        ControllerHarness harness = new(eventSink: sink);

        harness.Controller.RequestDestination(3);
        Action run = () => harness.RunAndRecordStops();

        run.Should().NotThrow("observation must never break the thing it observes");
        sink.PublishAttempts.Should().BeGreaterThan(0);
        harness.Controller.GetSnapshot().CurrentFloor.Should().Be(3);
    }

    [Fact]
    public void AFailureWhileAdvancingTheCar_IsReportedRatherThanThrown()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(
            eventSink: sink,
            schedulingStrategy: new FabricatingStrategy());
        harness.Controller.RequestDestination(4);

        Action process = harness.Controller.ProcessRequests;

        process.Should().NotThrow();
        sink.OfType<ElevatorStepFailed>().Should().ContainSingle()
            .Which.Failure.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void AFailureWhileAdvancingTheCar_LeavesTheSystemObservable()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(
            eventSink: sink,
            schedulingStrategy: new FabricatingStrategy());
        harness.Controller.RequestDestination(4);

        harness.Controller.ProcessRequests();

        ElevatorSnapshot snapshot = harness.Controller.GetSnapshot();
        snapshot.CurrentFloor.Should().Be(1);
        snapshot.PendingRequestCount.Should().Be(0, "the request was drained before the car failed");
    }

    [Fact]
    public void WithNoSinkSupplied_TheSystemStillRuns()
    {
        ControllerHarness harness = new();

        harness.Controller.RequestDestination(5);
        Action run = () => harness.RunAndRecordStops();

        run.Should().NotThrow();
    }

    /// <summary>A strategy that chooses a request it was never offered, so that the car fails.</summary>
    private sealed class FabricatingStrategy : IElevatorSchedulingStrategy
    {
        public ElevatorRequest? SelectNext(
            IReadOnlyList<ElevatorRequest> pendingRequests,
            SchedulingContext context) => new DestinationRequest(9);
    }
}
