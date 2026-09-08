using System.Diagnostics;
using ElevatorSystem.Core;
using ElevatorSystem.Simulator.Tests.Support;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Simulator.Tests;

public sealed class LoggingElevatorEventSinkTests
{
    private readonly CapturingLogger<LoggingElevatorEventSink> _logger = new();
    private readonly LoggingElevatorEventSink _sink;

    public LoggingElevatorEventSinkTests() => _sink = new LoggingElevatorEventSink(_logger);

    [Fact]
    public void Constructor_RejectsAMissingLogger()
    {
        Action construct = () => _ = new LoggingElevatorEventSink(null!);

        construct.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Publish_RejectsANullEvent()
    {
        Action publish = () => _sink.Publish(null!);

        publish.Should().Throw<ArgumentNullException>();
    }

    public static TheoryData<ElevatorEvent, LogLevel> EveryKindOfEvent => new()
    {
        { new RequestAdmitted(new PickupRequest(3, Direction.Up)), LogLevel.Information },
        { new RequestAdmitted(new DestinationRequest(7)), LogLevel.Information },
        {
            new RequestRejected(new DestinationRequest(42), RequestRejectionReason.FloorOutOfRange, "no such floor"),
            LogLevel.Warning
        },
        { new ElevatorDeparted(1, Direction.Up), LogLevel.Information },
        { new ElevatorMoved(1, 2), LogLevel.Debug },
        { new DoorOpened(4), LogLevel.Information },
        { new DoorClosed(4), LogLevel.Information },
        { new ElevatorStalled(5, ElevatorState.MovingUp, TimeSpan.FromSeconds(30)), LogLevel.Critical },
        { new ElevatorReturnedToService(), LogLevel.Information },
        {
            new RequestSchedulingFailed(new DestinationRequest(2), new InvalidOperationException("boom")),
            LogLevel.Error
        },
        { new ElevatorStepFailed(new InvalidOperationException("boom")), LogLevel.Error },
    };

    [Theory]
    [MemberData(nameof(EveryKindOfEvent))]
    public void EveryEvent_ProducesExactlyOneLineAtTheRightSeverity(ElevatorEvent elevatorEvent, LogLevel expected)
    {
        _sink.Publish(elevatorEvent);

        _logger.Lines.Should().ContainSingle()
            .Which.Level.Should().Be(expected);
    }

    [Fact]
    public void ARejection_LogsTheFloorTheReasonAndTheDetail()
    {
        _sink.Publish(new RequestRejected(
            new DestinationRequest(42),
            RequestRejectionReason.FloorOutOfRange,
            "Floor 42 does not exist."));

        string message = _logger.Lines.Should().ContainSingle().Subject.Message;
        message.Should().Contain("42")
            .And.Contain(nameof(RequestRejectionReason.FloorOutOfRange))
            .And.Contain("Floor 42 does not exist.");
    }

    [Fact]
    public void AFailure_KeepsTheExceptionRatherThanOnlyItsMessage()
    {
        InvalidOperationException failure = new("the motor is jammed");

        _sink.Publish(new ElevatorStepFailed(failure));

        _logger.Lines.Should().ContainSingle().Which.Exception.Should().BeSameAs(failure);
    }

    [Fact]
    public void AnEventTheSinkDoesNotKnowAbout_FailsLoudlyRatherThanSilently()
    {
        Action publish = () => _sink.Publish(new UnknownEvent());

        publish.Should().Throw<UnreachableException>(
            "a new event that nobody taught the log about must not vanish");
    }

    private sealed record UnknownEvent : ElevatorEvent;
}
