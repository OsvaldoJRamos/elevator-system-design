using ElevatorSystem.Core.ConcurrencyTests.Support;
using Xunit.Abstractions;

namespace ElevatorSystem.Core.ConcurrencyTests;

/// <summary>
/// A running system allocates on every processing cycle whether or not anything is happening, and
/// a host drives it many times a second for as long as the building is open. These tests bound
/// that steady-state cost so it cannot creep back up unnoticed.
/// </summary>
public sealed class SteadyStateAllocationTests
{
    private const int Cycles = 10_000;

    /// <summary>
    /// Comfortably above the measured cost of a snapshot and its floor list, and comfortably
    /// below the figure this cost before the LINQ enumerators and the redundant list build were
    /// removed from the cycle.
    /// </summary>
    private const int IdleBytesPerCycleCeiling = 160;

    private readonly ITestOutputHelper _output;

    public SteadyStateAllocationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void AnIdleCar_AllocatesLittleOnEachProcessingCycle()
    {
        LoadHarness harness = new();
        WarmUp(harness);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Cycles; i++)
        {
            harness.Controller.ProcessRequests();
        }

        long bytesPerCycle = (GC.GetAllocatedBytesForCurrentThread() - before) / Cycles;

        _output.WriteLine($"idle cycle: {bytesPerCycle} bytes");
        bytesPerCycle.Should().BeLessThan(
            IdleBytesPerCycleCeiling,
            "a car doing nothing should not generate rubbish for the collector to sweep up");
    }

    [Fact]
    public void ReadingWhetherWorkIsOutstanding_CostsNothing()
    {
        LoadHarness harness = new();
        for (int i = 0; i < 10; i++)
        {
            harness.Controller.RequestDestination(i + 1);
        }

        harness.Controller.ProcessRequests();
        for (int i = 0; i < 100; i++)
        {
            _ = harness.Controller.GetSnapshot();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Cycles; i++)
        {
            harness.Controller.ProcessRequests();
        }

        long bytesPerCycle = (GC.GetAllocatedBytesForCurrentThread() - before) / Cycles;

        _output.WriteLine($"busy cycle: {bytesPerCycle} bytes");
        bytesPerCycle.Should().BeLessThan(IdleBytesPerCycleCeiling * 2);
    }

    private static void WarmUp(LoadHarness harness)
    {
        for (int i = 0; i < 500; i++)
        {
            harness.Controller.ProcessRequests();
        }
    }
}
