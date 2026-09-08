using ElevatorSystem.Core;

namespace ElevatorSystem.Simulator.Tests;

/// <summary>
/// The first version of the simulator treated "the car stopped moving" as success regardless of
/// why, so a withdrawn elevator was announced as a completed scenario and the process exited
/// zero. These tests hold that line.
/// </summary>
public sealed class SimulationOutcomesTests
{
    private static ElevatorSnapshot Snapshot(bool outOfService, params int[] outstanding) =>
        new(CurrentFloor: 4, ElevatorState.Idle, outstanding, PendingRequestCount: 0, outOfService);

    [Fact]
    public void ACarThatServedEverything_IsAComplete​Run()
    {
        SimulationOutcomes.Determine(Snapshot(outOfService: false), settled: true)
            .Should().Be(SimulationOutcome.Completed);
    }

    [Fact]
    public void AWithdrawnCar_IsNotReportedAsSuccessEvenThoughItHasStopped()
    {
        SimulationOutcomes.Determine(Snapshot(outOfService: true), settled: true)
            .Should().Be(SimulationOutcome.ElevatorWithdrawn);
    }

    [Fact]
    public void AWithdrawnCarWithWorkOutstanding_IsStillAWithdrawal()
    {
        SimulationOutcomes.Determine(Snapshot(outOfService: true, 5, 9), settled: false)
            .Should().Be(SimulationOutcome.ElevatorWithdrawn);
    }

    [Fact]
    public void ACarThatNeverCameToRest_IsReportedAsNotSettling()
    {
        SimulationOutcomes.Determine(Snapshot(outOfService: false, 7), settled: false)
            .Should().Be(SimulationOutcome.DidNotSettle);
    }

    [Fact]
    public void Determine_RejectsAMissingSnapshot()
    {
        Action determine = () => SimulationOutcomes.Determine(null!, settled: true);

        determine.Should().Throw<ArgumentNullException>().WithParameterName("finalState");
    }

    [Theory]
    [InlineData(SimulationOutcome.Completed, 0)]
    [InlineData(SimulationOutcome.ElevatorWithdrawn, 1)]
    [InlineData(SimulationOutcome.DidNotSettle, 1)]
    public void OnlyACompletedRun_ExitsZero(SimulationOutcome outcome, int expectedExitCode)
    {
        outcome.ToExitCode().Should().Be(expectedExitCode);
    }
}
