using System.Diagnostics;
using ElevatorSystem.Core.ConcurrencyTests.Support;
using Xunit.Abstractions;

namespace ElevatorSystem.Core.ConcurrencyTests;

/// <summary>
/// The brief requires elevator assignment in under 100 ms. This measures it rather than asserting
/// it in prose.
/// </summary>
/// <remarks>
/// The design meets the requirement by construction — admission validates and enqueues on a
/// lock-free queue and never waits for the car — so the measured figures are orders of magnitude
/// under the budget. The point of these tests is to fail if that property is ever quietly lost,
/// for instance by someone adding a lock to the admission path.
/// </remarks>
public sealed class AdmissionLatencyTests
{
    private const int Budget = 100;
    private const int SampleCount = 5_000;

    private readonly ITestOutputHelper _output;

    public AdmissionLatencyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void AdmissionLatency_StaysUnderTheBudgetOnAQuietSystem()
    {
        LoadHarness harness = new();
        double[] samples = new double[SampleCount];

        // Let the JIT settle so the first call is not measured as if it were representative.
        for (int i = 0; i < 100; i++)
        {
            harness.Controller.RequestDestination(1);
        }

        for (int i = 0; i < SampleCount; i++)
        {
            long startedAt = Stopwatch.GetTimestamp();
            harness.Controller.RequestDestination((i % 10) + 1);
            samples[i] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        }

        Report("quiet system", samples);
        Percentile(samples, 0.99).Should().BeLessThan(Budget);
    }

    [Fact]
    public async Task AdmissionLatency_StaysUnderTheBudgetWhileTheCarIsBeingDriven()
    {
        LoadHarness harness = new();
        double[] samples = new double[SampleCount];
        using CancellationTokenSource stopProcessing = new();

        Task processing = Task.Run(() =>
        {
            while (!stopProcessing.Token.IsCancellationRequested)
            {
                harness.Controller.ProcessRequests();
            }
        });

        try
        {
            for (int i = 0; i < SampleCount; i++)
            {
                long startedAt = Stopwatch.GetTimestamp();
                harness.Controller.RequestDestination((i % 10) + 1);
                samples[i] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            }
        }
        finally
        {
            await stopProcessing.CancelAsync();
            await processing;
        }

        Report("while processing", samples);
        Percentile(samples, 0.99).Should().BeLessThan(
            Budget,
            "a caller must not be made to wait for the car");
    }

    [Fact]
    public void AdmissionLatency_StaysUnderTheBudgetWithSixtyFourCallersContending()
    {
        LoadHarness harness = new();
        const int callerCount = 64;
        const int perCaller = 200;
        double[][] samplesPerCaller = new double[callerCount][];

        Parallel.For(0, callerCount, caller =>
        {
            double[] samples = new double[perCaller];
            for (int i = 0; i < perCaller; i++)
            {
                long startedAt = Stopwatch.GetTimestamp();
                harness.Controller.RequestDestination((i % 10) + 1);
                samples[i] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            }

            samplesPerCaller[caller] = samples;
        });

        double[] allSamples = [.. samplesPerCaller.SelectMany(samples => samples)];

        Report($"{callerCount} concurrent callers", allSamples);
        Percentile(allSamples, 0.99).Should().BeLessThan(Budget);
    }

    private void Report(string scenario, double[] samples)
    {
        double[] sorted = [.. samples.Order()];

        _output.WriteLine(
            $"{scenario}: n={sorted.Length}, " +
            $"median={Percentile(sorted, 0.50):F4} ms, " +
            $"p99={Percentile(sorted, 0.99):F4} ms, " +
            $"max={sorted[^1]:F4} ms, " +
            $"budget={Budget} ms");
    }

    private static double Percentile(double[] samples, double percentile)
    {
        double[] sorted = [.. samples.Order()];
        int index = (int)Math.Ceiling(percentile * sorted.Length) - 1;

        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}
