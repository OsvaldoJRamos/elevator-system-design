using ElevatorSystem.Core.UnitTests.Support;

namespace ElevatorSystem.Core.UnitTests;

/// <summary>
/// What the system does once the car stops making progress: report it, stop pretending it works,
/// and turn passengers away with a reason they can act on.
/// </summary>
public sealed class ElevatorOutOfServiceTests
{
    private static readonly TimeSpan StuckTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A car configured to take an hour per floor: it departs, and then never arrives.
    /// </summary>
    private static ElevatorOptions SeizedMotor => ElevatorOptions.Default with
    {
        FloorTravelTime = TimeSpan.FromHours(1),
        DoorOpenDuration = TimeSpan.FromSeconds(1),
        StuckTimeout = StuckTimeout,
    };

    [Fact]
    public void ACarThatStopsMakingProgress_IsReportedAsStalled()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(eventSink: sink, elevatorOptions: SeizedMotor);
        harness.Controller.RequestDestination(5);
        harness.Controller.ProcessRequests();

        harness.AdvanceClock(StuckTimeout).Controller.ProcessRequests();

        ElevatorStalled stalled = sink.OfType<ElevatorStalled>().Should().ContainSingle().Subject;
        stalled.Floor.Should().Be(1);
        stalled.State.Should().Be(ElevatorState.MovingUp);
        stalled.StalledFor.Should().BeGreaterThanOrEqualTo(StuckTimeout);
    }

    [Fact]
    public void AStalledCar_IsTakenOutOfService()
    {
        ControllerHarness harness = new(elevatorOptions: SeizedMotor);
        harness.Controller.RequestDestination(5);
        harness.Controller.ProcessRequests();

        harness.AdvanceClock(StuckTimeout).Controller.ProcessRequests();

        harness.Controller.GetSnapshot().IsOutOfService.Should().BeTrue();
    }

    [Fact]
    public void ACarOutOfService_TurnsNewRequestsAwayWithAReasonTheCallerCanActOn()
    {
        ControllerHarness harness = new(elevatorOptions: SeizedMotor);
        harness.Controller.RequestDestination(5);
        harness.Controller.ProcessRequests();
        harness.AdvanceClock(StuckTimeout).Controller.ProcessRequests();

        RequestResult result = harness.Controller.RequestElevator(3, Direction.Up);

        result.IsAccepted.Should().BeFalse();
        result.RejectionReason.Should().Be(RequestRejectionReason.ElevatorOutOfService);
    }

    [Fact]
    public void ACarOutOfService_IsNotAdvancedFurther()
    {
        ControllerHarness harness = new(elevatorOptions: SeizedMotor);
        harness.Controller.RequestDestination(5);
        harness.Controller.ProcessRequests();
        harness.AdvanceClock(StuckTimeout).Controller.ProcessRequests();

        harness.AdvanceClock(TimeSpan.FromHours(10)).Controller.ProcessRequests();

        harness.Controller.GetSnapshot().CurrentFloor
            .Should().Be(1, "a car known to be broken must not be driven");
    }

    [Fact]
    public void AStallIsReportedOnce_NotOnEveryStep()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(eventSink: sink, elevatorOptions: SeizedMotor);
        harness.Controller.RequestDestination(5);
        harness.Controller.ProcessRequests();

        for (int i = 0; i < 5; i++)
        {
            harness.AdvanceClock(StuckTimeout).Controller.ProcessRequests();
        }

        sink.OfType<ElevatorStalled>().Should().ContainSingle();
    }

    [Fact]
    public void AHealthyCar_IsNeverTakenOutOfService()
    {
        ControllerHarness harness = new();
        harness.Controller.RequestDestination(10);

        harness.RunAndRecordStops();

        harness.Controller.GetSnapshot().IsOutOfService.Should().BeFalse();
    }

    [Fact]
    public void AnIdleCarWithNothingToDo_IsNeverTakenOutOfService()
    {
        ControllerHarness harness = new(elevatorOptions: SeizedMotor);

        for (int i = 0; i < 10; i++)
        {
            harness.AdvanceClock(StuckTimeout).Controller.ProcessRequests();
        }

        harness.Controller.GetSnapshot().IsOutOfService
            .Should().BeFalse("an elevator nobody has called is not a broken elevator");
    }

    [Fact]
    public void ACarThatRefusesToDepart_IsAlsoCaught()
    {
        // Nothing is physically stuck here: the scheduler simply never chooses. The watchdog does
        // not care why, which is what lets it catch causes nobody anticipated.
        RecordingEventSink sink = new();
        ControllerHarness harness = new(
            eventSink: sink,
            schedulingStrategy: new AbstainingStrategy(),
            elevatorOptions: SeizedMotor);
        harness.Controller.RequestDestination(5);
        harness.Controller.ProcessRequests();

        harness.AdvanceClock(StuckTimeout).Controller.ProcessRequests();

        sink.OfType<ElevatorStalled>().Should().ContainSingle()
            .Which.State.Should().Be(ElevatorState.Idle);
    }

    [Fact]
    public void ReturningToService_IsReportedAndRestoresAdmission()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(eventSink: sink, elevatorOptions: SeizedMotor);
        harness.Controller.RequestDestination(5);
        harness.Controller.ProcessRequests();
        harness.AdvanceClock(StuckTimeout).Controller.ProcessRequests();

        harness.Controller.ReturnToService();

        sink.OfType<ElevatorReturnedToService>().Should().ContainSingle();
        harness.Controller.GetSnapshot().IsOutOfService.Should().BeFalse();
        harness.Controller.RequestDestination(3).IsAccepted.Should().BeTrue();
    }

    [Fact]
    public void ReturningACarThatWasNeverOutOfService_ChangesNothing()
    {
        RecordingEventSink sink = new();
        ControllerHarness harness = new(eventSink: sink);

        harness.Controller.ReturnToService();

        sink.OfType<ElevatorReturnedToService>().Should().BeEmpty();
    }

    [Fact]
    public void WorkAdmittedBeforeTheFault_SurvivesTheReturnToService()
    {
        ControllerHarness harness = new(elevatorOptions: SeizedMotor);
        harness.Controller.RequestDestination(5);
        harness.Controller.ProcessRequests();
        harness.AdvanceClock(StuckTimeout).Controller.ProcessRequests();

        harness.Controller.ReturnToService();

        harness.Controller.GetSnapshot().TargetFloors
            .Should().Equal(new[] { 5 }, "a passenger waiting before the fault is still waiting after it");
    }

    /// <summary>A strategy that never chooses anything, however much is waiting.</summary>
    private sealed class AbstainingStrategy : IElevatorSchedulingStrategy
    {
        public ElevatorRequest? SelectNext(
            IReadOnlyList<ElevatorRequest> pendingRequests,
            SchedulingContext context) => null;
    }
}
