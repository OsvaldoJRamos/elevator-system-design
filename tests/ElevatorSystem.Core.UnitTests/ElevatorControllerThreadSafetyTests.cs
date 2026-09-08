using System.Collections.Concurrent;
using ElevatorSystem.Core.UnitTests.Support;

namespace ElevatorSystem.Core.UnitTests;

/// <summary>
/// The controller is the only type in the system that more than one thread touches. These tests
/// exercise that contract directly: many callers submitting at once, and callers submitting while
/// the elevator is being driven.
/// </summary>
/// <remarks>
/// Load and latency belong to the dedicated concurrency suite; what is asserted here is
/// correctness — nothing lost, nothing duplicated, nothing corrupted.
/// </remarks>
public sealed class ElevatorControllerThreadSafetyTests
{
    [Fact]
    public void RequestsSubmittedFromManyThreads_AreAllAdmittedAndNoneLost()
    {
        const int threadCount = 50;
        const int requestsPerThread = 20;
        ControllerHarness harness = new();

        Parallel.For(0, threadCount, _ =>
        {
            for (int i = 0; i < requestsPerThread; i++)
            {
                RequestResult result = harness.Controller.RequestDestination((i % 10) + 1);
                result.IsAccepted.Should().BeTrue();
            }
        });

        harness.Controller.GetSnapshot().PendingRequestCount
            .Should().Be(threadCount * requestsPerThread);
    }

    [Fact]
    public async Task SubmittingWhileProcessing_LosesNothingAndThrowsNothing()
    {
        const int producerCount = 8;
        const int requestsPerProducer = 100;
        ControllerHarness harness = new();
        ConcurrentBag<Exception> failures = [];

        using CancellationTokenSource processingIsDone = new();

        Task[] consumers = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            try
            {
                while (!processingIsDone.Token.IsCancellationRequested)
                {
                    harness.Controller.ProcessRequests();
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        })).ToArray();

        Parallel.For(0, producerCount, producer =>
        {
            for (int i = 0; i < requestsPerProducer; i++)
            {
                harness.Controller.RequestElevator((i % 10) + 1, producer % 2 == 0 ? Direction.Up : Direction.Down);
            }
        });

        await processingIsDone.CancelAsync();
        await Task.WhenAll(consumers);
        harness.Controller.ProcessRequests();

        failures.Should().BeEmpty("concurrent processing must not corrupt the elevator");
        harness.Controller.GetSnapshot().PendingRequestCount
            .Should().Be(0, "every admitted request must reach the elevator");
    }

    [Fact]
    public async Task SnapshotsTakenWhileTheCarMoves_AreInternallyConsistent()
    {
        ControllerHarness harness = new();
        harness.Controller.RequestDestination(10);
        ConcurrentBag<ElevatorSnapshot> observed = [];

        using CancellationTokenSource observationIsDone = new();

        // Waiting on this before driving the car removes a race in the test itself: on a busy
        // thread pool the observer could otherwise be cancelled before running even once, and
        // a test about concurrency must not itself depend on timing.
        TaskCompletionSource observerIsRunning = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task observer = Task.Run(() =>
        {
            while (!observationIsDone.Token.IsCancellationRequested)
            {
                observed.Add(harness.Controller.GetSnapshot());
                observerIsRunning.TrySetResult();
            }
        });

        await observerIsRunning.Task;
        harness.Tick(20);
        await observationIsDone.CancelAsync();
        await observer;

        observed.Should().NotBeEmpty();
        observed.Should().AllSatisfy(snapshot =>
        {
            snapshot.CurrentFloor.Should().BeInRange(1, 10);
            snapshot.PendingRequestCount.Should().BeGreaterThanOrEqualTo(0);

            // An empty queue is a legitimate observation, both before the car departs and
            // after it has finished; what must never appear is a floor that does not exist.
            snapshot.TargetFloors.Where(floor => floor is < 1 or > 10)
                .Should().BeEmpty("a snapshot must never expose a floor outside the building");
        });
    }
}
