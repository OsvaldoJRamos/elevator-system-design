namespace ElevatorSystem.Core;

/// <summary>
/// Why a request was not admitted.
/// </summary>
/// <remarks>
/// Typed rather than free text because the distinction is actionable: a floor that does not exist
/// will never exist, whereas a car out of service will come back. A caller can retry one and must
/// not retry the other.
/// </remarks>
public enum RequestRejectionReason
{
    /// <summary>The requested floor is not served by this building.</summary>
    FloorOutOfRange,

    /// <summary>The direction supplied is not one the elevator understands.</summary>
    UnknownDirection,

    /// <summary>The elevator has been taken out of service and is accepting nothing.</summary>
    ElevatorOutOfService,
}
