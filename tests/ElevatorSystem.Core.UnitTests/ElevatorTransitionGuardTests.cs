using ElevatorSystem.Core.UnitTests.Support;

namespace ElevatorSystem.Core.UnitTests;

/// <summary>
/// The movement and door primitives are part of the public contract, so they have to defend
/// themselves: an illegal transition is a bug in the caller and must not be absorbed silently.
/// </summary>
public sealed class ElevatorTransitionGuardTests
{
    [Fact]
    public void MoveUp_AtTheTopFloor_IsRejected()
    {
        ElevatorHarness harness = new(startingFloor: 10);

        Action moveUp = harness.Elevator.MoveUp;

        moveUp.Should().Throw<InvalidElevatorOperationException>()
            .WithMessage("*top floor*");
    }

    [Fact]
    public void MoveDown_AtTheBottomFloor_IsRejected()
    {
        ElevatorHarness harness = new(startingFloor: 1);

        Action moveDown = harness.Elevator.MoveDown;

        moveDown.Should().Throw<InvalidElevatorOperationException>()
            .WithMessage("*bottom floor*");
    }

    [Fact]
    public void MoveUp_WithTheDoorOpen_IsRejected()
    {
        ElevatorHarness harness = new(startingFloor: 5);
        harness.Elevator.OpenDoor();

        Action moveUp = harness.Elevator.MoveUp;

        moveUp.Should().Throw<InvalidElevatorOperationException>()
            .WithMessage("*door*");
    }

    [Fact]
    public void MoveDown_WithTheDoorOpen_IsRejected()
    {
        ElevatorHarness harness = new(startingFloor: 5);
        harness.Elevator.OpenDoor();

        Action moveDown = harness.Elevator.MoveDown;

        moveDown.Should().Throw<InvalidElevatorOperationException>()
            .WithMessage("*door*");
    }

    [Theory]
    [InlineData(ElevatorState.MovingUp)]
    [InlineData(ElevatorState.MovingDown)]
    public void OpenDoor_WhileTheCarIsMoving_IsRejected(ElevatorState movingState)
    {
        ElevatorHarness harness = new(startingFloor: 5);
        if (movingState is ElevatorState.MovingUp)
        {
            harness.Elevator.MoveUp();
        }
        else
        {
            harness.Elevator.MoveDown();
        }

        Action openDoor = harness.Elevator.OpenDoor;

        openDoor.Should().Throw<InvalidElevatorOperationException>()
            .WithMessage("*moving*");
    }

    [Fact]
    public void CloseDoor_WhenTheDoorIsNotOpen_IsRejected()
    {
        ElevatorHarness harness = new();

        Action closeDoor = harness.Elevator.CloseDoor;

        closeDoor.Should().Throw<InvalidElevatorOperationException>();
    }

    [Fact]
    public void OpenDoor_WhenAlreadyOpen_IsIdempotent()
    {
        ElevatorHarness harness = new();
        harness.Elevator.OpenDoor();

        Action openAgain = harness.Elevator.OpenDoor;

        openAgain.Should().NotThrow();
        harness.Elevator.State.Should().Be(ElevatorState.DoorOpen);
    }

    [Fact]
    public void MoveUp_FromIdle_MovesOneFloorAndReportsTheDirection()
    {
        ElevatorHarness harness = new(startingFloor: 5);

        harness.Elevator.MoveUp();

        harness.Elevator.CurrentFloor.Should().Be(6);
        harness.Elevator.State.Should().Be(ElevatorState.MovingUp);
    }

    [Fact]
    public void MoveDown_FromIdle_MovesOneFloorAndReportsTheDirection()
    {
        ElevatorHarness harness = new(startingFloor: 5);

        harness.Elevator.MoveDown();

        harness.Elevator.CurrentFloor.Should().Be(4);
        harness.Elevator.State.Should().Be(ElevatorState.MovingDown);
    }

    [Fact]
    public void CloseDoor_FromDoorOpen_ReturnsTheCarToIdle()
    {
        ElevatorHarness harness = new();
        harness.Elevator.OpenDoor();

        harness.Elevator.CloseDoor();

        harness.Elevator.State.Should().Be(ElevatorState.Idle);
    }
}
