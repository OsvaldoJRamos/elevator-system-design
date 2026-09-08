using ElevatorSystem.Core.UnitTests.Support;

namespace ElevatorSystem.Core.UnitTests;

/// <summary>
/// The elevator decides <em>how</em> to travel; the strategy decides <em>where next</em>. These
/// tests hold that line: swapping the strategy must change the order of stops without the
/// elevator knowing anything about the algorithm.
/// </summary>
public sealed class ElevatorSchedulingTests
{
    [Fact]
    public void Constructor_RejectsAMissingSchedulingStrategy()
    {
        Action construct = () => _ = new Elevator(ElevatorOptions.Default, TimeProvider.System, null!);

        construct.Should().Throw<ArgumentNullException>()
            .WithParameterName("schedulingStrategy");
    }

    [Fact]
    public void Elevator_ServesFloorsInTheOrderItsStrategyChooses()
    {
        ElevatorHarness fifo = new();
        ElevatorHarness nearestFirst = new(schedulingStrategy: new NearestFloorFirstStrategy());

        foreach (ElevatorHarness harness in new[] { fifo, nearestFirst })
        {
            harness.Elevator.AddRequest(new DestinationRequest(8));
            harness.Elevator.AddRequest(new DestinationRequest(3));
        }

        fifo.RunAndRecordStops().Should().Equal(new[] { 8, 3 }, "first in, first out");
        nearestFirst.RunAndRecordStops().Should().Equal(new[] { 3, 8 }, "the nearer floor is served first");
    }

    [Fact]
    public void Elevator_RemovesTheChosenRequestFromItsQueue()
    {
        ElevatorHarness harness = new();
        harness.Elevator.AddRequest(new DestinationRequest(4));
        harness.Elevator.AddRequest(new DestinationRequest(7));

        harness.Elevator.Step();

        harness.Elevator.TargetFloors.Should().Equal(new[] { 4, 7 },
            "the chosen floor moves from the queue to being served, and stays at the front");
    }

    [Fact]
    public void Elevator_RejectsAStrategyThatReturnsARequestItWasNotOffered()
    {
        ElevatorHarness harness = new(schedulingStrategy: new FabricatingStrategy());
        harness.Elevator.AddRequest(new DestinationRequest(4));

        Action step = harness.Elevator.Step;

        step.Should().Throw<InvalidOperationException>()
            .WithMessage("*not among the pending requests*");
    }

    [Fact]
    public void Elevator_StaysIdleWhenTheStrategyDeclinesToChoose()
    {
        ElevatorHarness harness = new(schedulingStrategy: new AbstainingStrategy());
        harness.Elevator.AddRequest(new DestinationRequest(4));

        harness.Tick(3);

        harness.Elevator.State.Should().Be(ElevatorState.Idle);
        harness.Elevator.CurrentFloor.Should().Be(1);
    }

    /// <summary>A strategy that invents a request instead of choosing one of the offered ones.</summary>
    private sealed class FabricatingStrategy : IElevatorSchedulingStrategy
    {
        public ElevatorRequest? SelectNext(
            IReadOnlyList<ElevatorRequest> pendingRequests,
            SchedulingContext context) => new DestinationRequest(9);
    }

    /// <summary>A strategy that never chooses anything, however much is waiting.</summary>
    private sealed class AbstainingStrategy : IElevatorSchedulingStrategy
    {
        public ElevatorRequest? SelectNext(
            IReadOnlyList<ElevatorRequest> pendingRequests,
            SchedulingContext context) => null;
    }
}
