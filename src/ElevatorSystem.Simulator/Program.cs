using ElevatorSystem.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Simulator;

/// <summary>
/// Entry point and composition root of the elevator simulator.
/// </summary>
/// <remarks>
/// Everything the system is made of is chosen here and nowhere else. Swapping FIFO for another
/// algorithm, or the console log for a different sink, is a change to this file alone — which is
/// the practical payoff of the abstractions the core defines.
/// </remarks>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(console =>
        {
            console.SingleLine = true;
            console.TimestampFormat = "HH:mm:ss.fff ";
        });

        ConfigureElevatorSystem(builder.Services);

        await builder.Build().RunAsync().ConfigureAwait(false);
    }

    private static void ConfigureElevatorSystem(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // Accelerated relative to a real installation so that a demonstration run finishes in
        // seconds. The behaviour under test is identical; only the constants differ.
        services.AddSingleton(ElevatorOptions.Default with
        {
            Floors = FloorRange.OneToTen,
            FloorTravelTime = TimeSpan.FromMilliseconds(250),
            DoorOpenDuration = TimeSpan.FromMilliseconds(400),
            StuckTimeout = TimeSpan.FromSeconds(5),
        });

        services.AddSingleton(new SimulationOptions
        {
            ProcessingInterval = TimeSpan.FromMilliseconds(25),
        });

        services.AddSingleton(SimulationScenario.MorningRush);

        services.AddSingleton<IElevatorSchedulingStrategy, FifoSchedulingStrategy>();
        services.AddSingleton<IElevatorEventSink, LoggingElevatorEventSink>();

        services.AddSingleton(provider => new Elevator(
            provider.GetRequiredService<ElevatorOptions>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IElevatorSchedulingStrategy>(),
            provider.GetRequiredService<SimulationScenario>().StartingFloor));

        services.AddSingleton(provider => new StuckElevatorWatchdog(
            provider.GetRequiredService<ElevatorOptions>().StuckTimeout,
            provider.GetRequiredService<TimeProvider>()));

        services.AddSingleton(provider => new ElevatorController(
            provider.GetRequiredService<Elevator>(),
            provider.GetRequiredService<StuckElevatorWatchdog>(),
            provider.GetRequiredService<IElevatorEventSink>()));

        services.AddSingleton<ElevatorRunner>();
        services.AddHostedService<SimulationHostedService>();
    }
}
