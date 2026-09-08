using System.Reflection;

namespace ElevatorSystem.Core;

/// <summary>
/// Marks the assembly that contains the elevator domain model.
/// </summary>
/// <remarks>
/// Exists so that tests and composition roots can refer to this assembly without
/// depending on any particular domain type.
/// </remarks>
public static class ElevatorSystemCore
{
    /// <summary>
    /// Gets the assembly that contains the elevator domain model.
    /// </summary>
    public static Assembly Assembly => typeof(ElevatorSystemCore).Assembly;
}
