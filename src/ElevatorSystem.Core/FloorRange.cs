namespace ElevatorSystem.Core;

/// <summary>
/// The contiguous set of floors an elevator is allowed to serve.
/// </summary>
/// <remarks>
/// This is the single place that knows which floors exist. Everything that needs to decide
/// whether a floor is real asks this type rather than repeating the bounds check.
/// </remarks>
public sealed record FloorRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FloorRange"/> class.
    /// </summary>
    /// <param name="lowest">The lowest floor served.</param>
    /// <param name="highest">The highest floor served. Must be above <paramref name="lowest"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The range would contain fewer than two floors.
    /// </exception>
    public FloorRange(int lowest, int highest)
    {
        if (highest <= lowest)
        {
            throw new ArgumentOutOfRangeException(
                nameof(highest),
                highest,
                $"A building needs at least two floors, so the highest floor must be above the lowest ({lowest}).");
        }

        Lowest = lowest;
        Highest = highest;
    }

    /// <summary>
    /// Gets the building described by the brief: floors 1 through 10.
    /// </summary>
    public static FloorRange OneToTen { get; } = new(lowest: 1, highest: 10);

    /// <summary>
    /// Gets the lowest floor served.
    /// </summary>
    public int Lowest { get; }

    /// <summary>
    /// Gets the highest floor served.
    /// </summary>
    public int Highest { get; }

    /// <summary>
    /// Determines whether the given floor exists in this building.
    /// </summary>
    /// <param name="floor">The floor to check.</param>
    /// <returns><see langword="true"/> if the floor is served; otherwise <see langword="false"/>.</returns>
    public bool Contains(int floor) => floor >= Lowest && floor <= Highest;
}
