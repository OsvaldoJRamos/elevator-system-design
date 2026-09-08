namespace ElevatorSystem.Core.UnitTests.Architecture;

/// <summary>
/// Guards the boundary declared in the design document: the domain assembly must stay
/// free of third-party and framework-integration dependencies, so that it can be reasoned
/// about and tested without any infrastructure at all.
/// </summary>
public sealed class CoreDependencyTests
{
    private static readonly string[] AllowedAssemblyPrefixes =
    [
        "System",
        "netstandard",
        "mscorlib",
    ];

    [Fact]
    public void Core_ReferencesNothingOutsideTheBaseClassLibrary()
    {
        IEnumerable<string> referencedAssemblies = ElevatorSystemCore.Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name!)
            .Where(name => !IsBaseClassLibrary(name));

        referencedAssemblies.Should().BeEmpty(
            "the domain model must not depend on logging, DI or any third-party package");
    }

    private static bool IsBaseClassLibrary(string assemblyName) =>
        AllowedAssemblyPrefixes.Any(prefix =>
            assemblyName.Equals(prefix, StringComparison.Ordinal) ||
            assemblyName.StartsWith(prefix + ".", StringComparison.Ordinal));
}
