namespace ElevatorSystem.Core;

/// <summary>
/// The outcome of offering a request to the elevator system.
/// </summary>
/// <remarks>
/// A passenger asking for a floor that does not exist is ordinary traffic, not an exceptional
/// condition, so it is reported as a value rather than thrown. Exceptions in this system are
/// reserved for defects in the calling code.
/// </remarks>
public sealed record RequestResult
{
    private RequestResult(bool isAccepted, string? rejectionReason)
    {
        IsAccepted = isAccepted;
        RejectionReason = rejectionReason;
    }

    /// <summary>
    /// Gets the result returned when a request has been admitted for scheduling.
    /// </summary>
    /// <remarks>
    /// A single shared instance: admission happens on the hot path, and an accepted result
    /// carries no per-request information worth allocating for.
    /// </remarks>
    public static RequestResult Accepted { get; } = new(isAccepted: true, rejectionReason: null);

    /// <summary>
    /// Gets a value indicating whether the request was admitted.
    /// </summary>
    public bool IsAccepted { get; }

    /// <summary>
    /// Gets why the request was turned away, or <see langword="null"/> if it was accepted.
    /// </summary>
    public string? RejectionReason { get; }

    /// <summary>
    /// Creates a result describing a request that was turned away.
    /// </summary>
    /// <param name="reason">A description of why, suitable for a log or an error message.</param>
    /// <returns>A rejected result.</returns>
    public static RequestResult Rejected(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new RequestResult(isAccepted: false, rejectionReason: reason);
    }
}
