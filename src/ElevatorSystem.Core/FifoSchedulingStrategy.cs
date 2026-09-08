namespace ElevatorSystem.Core;

/// <summary>
/// Serves requests strictly in the order they arrived.
/// </summary>
/// <remarks>
/// <para>
/// This is the algorithm the brief asks for, and it is deliberately not an efficient one: a car
/// travelling from floor 1 to floor 8 will pass a passenger waiting on floor 3 without stopping,
/// because that request arrived later. The position and direction in
/// <see cref="SchedulingContext"/> are therefore ignored, which is the whole of the trade-off.
/// </para>
/// <para>
/// See <c>docs/adr/0002-serve-requests-first-in-first-out.md</c> for why this is the right
/// starting point and what a lift-industry algorithm would do instead.
/// </para>
/// </remarks>
public sealed class FifoSchedulingStrategy : IElevatorSchedulingStrategy
{
    /// <inheritdoc />
    public ElevatorRequest? SelectNext(IReadOnlyList<ElevatorRequest> pendingRequests, SchedulingContext context)
    {
        ArgumentNullException.ThrowIfNull(pendingRequests);

        return pendingRequests.Count == 0 ? null : pendingRequests[0];
    }
}
