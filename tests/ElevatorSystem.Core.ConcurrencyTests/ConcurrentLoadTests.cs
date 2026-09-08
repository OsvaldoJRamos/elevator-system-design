using System.Collections.Concurrent;
using ElevatorSystem.Core.ConcurrencyTests.Support;
using Xunit.Abstractions;

namespace ElevatorSystem.Core.ConcurrencyTests;

/// <summary>
/// The brief asks the system to handle 100+ concurrent requests efficiently. These tests put a
/// multiple of that through it and check that nothing is lost, duplicated or corrupted, and that
/// everything admitted is eventually served.
/// </summary>
public sealed class ConcurrentLoadTests
{
    private const int ProducerCount = 64;
    private const int RequestsPerProducer = 50;
    private const int TotalRequests = ProducerCount * RequestsPerProducer;

    private readonly ITestOutputHelper _output;

    public ConcurrentLoadTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ThreeThousandRequestsFromSixtyFourThreads_AreAllAdmitted()
    {
        LoadHarness harness = new();

        Parallel.For(0, ProducerCount, producer =>
        {
            for (int i = 0; i < RequestsPerProducer; i++)
            {
                int floor = (i % 10) + 1;
                RequestResult result = producer % 2 == 0
                    ? harness.Controller.RequestDestination(floor)
                    : harness.Controller.RequestElevator(floor, i % 2 == 0 ? Direction.Up : Direction.Down);

                result.IsAccepted.Should().BeTrue();
            }
        });

        harness.Controller.GetSnapshot().PendingRequestCount.Should().Be(TotalRequests);
        _output.WriteLine($"{TotalRequests} requests admitted from {ProducerCount} threads, none lost.");
    }

    [Fact]
    public void EverythingAdmittedUnderLoad_IsEventuallyServed()
    {
        LoadHarness harness = new();

        Parallel.For(0, ProducerCount, _ =>
        {
            for (int i = 0; i < RequestsPerProducer; i++)
            {
                harness.Controller.RequestDestination((i % 10) + 1);
            }
        });

        int steps = harness.DrainEverything();

        ElevatorSnapshot finalState = harness.Controller.GetSnapshot();
        finalState.PendingRequestCount.Should().Be(0);
        finalState.TargetFloors.Should().BeEmpty();
        finalState.State.Should().Be(ElevatorState.Idle);
        finalState.IsOutOfService.Should().BeFalse();

        _output.WriteLine($"{TotalRequests} requests drained in {steps} steps.");
    }

    [Fact]
    public async Task RequestsArrivingWhileTheCarRuns_AreNeitherLostNorDuplicated()
    {
        LoadHarness harness = new();
        ConcurrentBag<Exception> failures = [];
        using CancellationTokenSource stopProcessing = new();

        Task[] consumers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            try
            {
                while (!stopProcessing.Token.IsCancellationRequested)
                {
                    harness.Controller.ProcessRequests();
                }
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        })).ToArray();

        int admitted = 0;
        Parallel.For(0, ProducerCount, _ =>
        {
            for (int i = 0; i < RequestsPerProducer; i++)
            {
                if (harness.Controller.RequestDestination((i % 10) + 1).IsAccepted)
                {
                    Interlocked.Increment(ref admitted);
                }
            }
        });

        await stopProcessing.CancelAsync();
        await Task.WhenAll(consumers);

        failures.Should().BeEmpty("four threads processing at once must not corrupt the car");
        admitted.Should().Be(TotalRequests);

        harness.DrainEverything();
        harness.Controller.GetSnapshot().PendingRequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ObserversUnderLoad_NeverSeeAnImpossibleState()
    {
        LoadHarness harness = new();
        ConcurrentBag<ElevatorSnapshot> observations = [];
        using CancellationTokenSource stopObserving = new();
        TaskCompletionSource observerIsRunning = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task observer = Task.Run(() =>
        {
            while (!stopObserving.Token.IsCancellationRequested)
            {
                observations.Add(harness.Controller.GetSnapshot());
                observerIsRunning.TrySetResult();
            }
        });

        await observerIsRunning.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Parallel.For(0, ProducerCount, _ =>
        {
            for (int i = 0; i < RequestsPerProducer; i++)
            {
                harness.Controller.RequestDestination((i % 10) + 1);
            }
        });
        harness.DrainEverything();

        await stopObserving.CancelAsync();
        await observer;

        observations.Should().NotBeEmpty();
        observations.Should().AllSatisfy(snapshot =>
        {
            snapshot.CurrentFloor.Should().BeInRange(harness.Floors.Lowest, harness.Floors.Highest);
            snapshot.PendingRequestCount.Should().BeGreaterThanOrEqualTo(0);
            snapshot.TargetFloors
                .Where(floor => floor < harness.Floors.Lowest || floor > harness.Floors.Highest)
                .Should().BeEmpty();
        });

        _output.WriteLine($"{observations.Count} snapshots observed under load, all internally consistent.");
    }
}
