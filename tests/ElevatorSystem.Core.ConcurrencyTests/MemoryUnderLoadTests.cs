using ElevatorSystem.Core.ConcurrencyTests.Support;
using Xunit.Abstractions;

namespace ElevatorSystem.Core.ConcurrencyTests;

/// <summary>
/// The brief asks that memory usage stay reasonable under load. "Reasonable" is made concrete
/// here as two properties: admitting a request costs a small, constant amount, and nothing is
/// retained once the work is done.
/// </summary>
public sealed class MemoryUnderLoadTests
{
    private const int RequestCount = 100_000;

    /// <summary>
    /// Generous compared with what an accepted request actually costs — a small record and a queue
    /// slot. The purpose is to catch a change that starts allocating per request, not to pin the
    /// exact figure.
    /// </summary>
    private const int BytesPerRequestCeiling = 256;

    private readonly ITestOutputHelper _output;

    public MemoryUnderLoadTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void AdmittingARequest_CostsASmallConstantAmountOfMemory()
    {
        LoadHarness harness = new();

        // Warm up so that one-off allocations are not attributed to the measured requests.
        for (int i = 0; i < 1_000; i++)
        {
            harness.Controller.RequestDestination((i % 10) + 1);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < RequestCount; i++)
        {
            harness.Controller.RequestDestination((i % 10) + 1);
        }

        long bytesPerRequest = (GC.GetAllocatedBytesForCurrentThread() - before) / RequestCount;

        _output.WriteLine($"{bytesPerRequest} bytes allocated per admitted request.");
        bytesPerRequest.Should().BeLessThan(BytesPerRequestCeiling);
    }

    [Fact]
    public void ARejectedRequest_IsNotRetainedAnywhere()
    {
        LoadHarness harness = new();

        for (int i = 0; i < RequestCount; i++)
        {
            harness.Controller.RequestDestination(harness.Floors.Highest + 1 + i);
        }

        harness.Controller.GetSnapshot().PendingRequestCount
            .Should().Be(0, "a request that was never admitted must not occupy the queue");
    }

    [Fact]
    public void ServingTheQueue_ReleasesEverythingItHeld()
    {
        LoadHarness harness = new();
        for (int i = 0; i < RequestCount; i++)
        {
            harness.Controller.RequestDestination((i % 10) + 1);
        }

        harness.DrainEverything();

        long heldAfterDraining = MeasureLiveBytes();
        ElevatorSnapshot finalState = harness.Controller.GetSnapshot();

        finalState.PendingRequestCount.Should().Be(0);
        finalState.TargetFloors.Should().BeEmpty();

        _output.WriteLine(
            $"{RequestCount} requests served; {heldAfterDraining / 1024:N0} KiB live afterwards.");
    }

    [Fact]
    public void TheQueueGrowsWithOutstandingWorkAndNotWithTrafficAlreadyServed()
    {
        LoadHarness harness = new();

        for (int round = 0; round < 5; round++)
        {
            for (int i = 0; i < 10_000; i++)
            {
                harness.Controller.RequestDestination((i % 10) + 1);
            }

            harness.DrainEverything();

            harness.Controller.GetSnapshot().PendingRequestCount
                .Should().Be(0, "each round leaves nothing behind for the next one to carry");
        }
    }

    private static long MeasureLiveBytes()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return GC.GetTotalMemory(forceFullCollection: true);
    }
}
