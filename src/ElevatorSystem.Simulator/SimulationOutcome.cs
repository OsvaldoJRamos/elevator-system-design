using ElevatorSystem.Core;

namespace ElevatorSystem.Simulator;

/// <summary>
/// How a simulation run ended.
/// </summary>
public enum SimulationOutcome
{
    /// <summary>Every request was served and the car came to rest.</summary>
    Completed,

    /// <summary>The car was withdrawn from service before it could finish.</summary>
    ElevatorWithdrawn,

    /// <summary>The car never came to rest, and the run gave up waiting for it.</summary>
    DidNotSettle,
}

/// <summary>
/// Decides what a finished run should be reported as.
/// </summary>
/// <remarks>
/// Extracted from the host so the decision can be tested directly. It exists because the first
/// version of the simulator treated "the car stopped moving" as success regardless of why, and
/// therefore announced a completed scenario and exited zero after withdrawing a broken elevator
/// from service.
/// </remarks>
public static class SimulationOutcomes
{
    /// <summary>
    /// Determines the outcome of a run from the state the system finished in.
    /// </summary>
    /// <param name="finalState">The system as it was when the run stopped.</param>
    /// <param name="settled">
    /// Whether the run stopped because the car had nothing left to do, rather than because it
    /// gave up waiting.
    /// </param>
    /// <returns>What to report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="finalState"/> is <see langword="null"/>.</exception>
    public static SimulationOutcome Determine(ElevatorSnapshot finalState, bool settled)
    {
        ArgumentNullException.ThrowIfNull(finalState);

        // Checked before anything else: a withdrawn car has stopped having work to do, so every
        // other test for "finished" would otherwise report it as a success.
        if (finalState.IsOutOfService)
        {
            return SimulationOutcome.ElevatorWithdrawn;
        }

        return settled ? SimulationOutcome.Completed : SimulationOutcome.DidNotSettle;
    }

    /// <summary>
    /// Gets the process exit code an outcome should produce.
    /// </summary>
    /// <param name="outcome">The outcome of the run.</param>
    /// <returns>Zero if the run succeeded; otherwise a non-zero code.</returns>
    public static int ToExitCode(this SimulationOutcome outcome) =>
        outcome is SimulationOutcome.Completed ? 0 : 1;
}
