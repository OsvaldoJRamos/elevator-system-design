namespace ElevatorSystem.Core.UnitTests.Support;

/// <summary>
/// A scheduling strategy that always serves whichever pending floor is closest to the car.
/// </summary>
/// <remarks>
/// This exists only in the test suite, and only to prove that <see cref="Elevator"/> genuinely
/// delegates its scheduling decision. If the elevator had FIFO baked in, a test using this
/// strategy would still produce FIFO ordering and fail — which is exactly what makes
/// <c>IElevatorSchedulingStrategy</c> a real seam rather than a decorative one.
/// </remarks>
internal sealed class NearestFloorFirstStrategy : IElevatorSchedulingStrategy
{
    public ElevatorRequest? SelectNext(IReadOnlyList<ElevatorRequest> pendingRequests, SchedulingContext context)
    {
        ArgumentNullException.ThrowIfNull(pendingRequests);

        return pendingRequests
            .OrderBy(request => Math.Abs(request.Floor - context.CurrentFloor))
            .FirstOrDefault();
    }
}
