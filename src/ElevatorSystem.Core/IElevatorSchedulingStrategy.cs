namespace ElevatorSystem.Core;

/// <summary>
/// Decides which of the waiting requests the elevator should serve next.
/// </summary>
/// <remarks>
/// This is the one decision in the system that a building operator might reasonably want to
/// change. Isolating it here means a different algorithm is a new class and a different line in
/// the composition root, rather than an edit to the state machine.
/// </remarks>
public interface IElevatorSchedulingStrategy
{
    /// <summary>
    /// Chooses the next request to serve.
    /// </summary>
    /// <param name="pendingRequests">
    /// The requests waiting to be served, oldest first. Implementations must treat this as
    /// read-only: choosing is the strategy's job, removing is the elevator's.
    /// </param>
    /// <param name="context">Where the car is and what it is doing.</param>
    /// <returns>
    /// One of the requests in <paramref name="pendingRequests"/>, or <see langword="null"/> to
    /// leave the car idle. Returning a request that was not offered is a programming error and
    /// the elevator will reject it.
    /// </returns>
    ElevatorRequest? SelectNext(IReadOnlyList<ElevatorRequest> pendingRequests, SchedulingContext context);
}
