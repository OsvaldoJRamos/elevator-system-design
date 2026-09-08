namespace ElevatorSystem.Core.UnitTests;

public sealed class ElevatorOptionsTests
{
    [Fact]
    public void Default_DescribesTheBuildingAndTimingsFromTheBrief()
    {
        ElevatorOptions options = ElevatorOptions.Default;

        options.Floors.Should().Be(FloorRange.OneToTen);
        options.FloorTravelTime.Should().BePositive();
        options.DoorOpenDuration.Should().BePositive();
    }

    [Fact]
    public void FloorTravelTime_RejectsNonPositiveDurations()
    {
        Action configure = () => _ = ElevatorOptions.Default with { FloorTravelTime = TimeSpan.Zero };

        configure.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DoorOpenDuration_RejectsNonPositiveDurations()
    {
        Action configure = () => _ = ElevatorOptions.Default with { DoorOpenDuration = TimeSpan.FromSeconds(-1) };

        configure.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Floors_RejectsNull()
    {
        Action configure = () => _ = ElevatorOptions.Default with { Floors = null! };

        configure.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Options_CanBeCustomisedWithoutMutatingTheDefault()
    {
        ElevatorOptions customised = ElevatorOptions.Default with
        {
            Floors = new FloorRange(1, 3),
            FloorTravelTime = TimeSpan.FromMilliseconds(500),
        };

        customised.Floors.Highest.Should().Be(3);
        ElevatorOptions.Default.Floors.Highest.Should().Be(10);
    }
}
