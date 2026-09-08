namespace ElevatorSystem.Core.UnitTests;

public sealed class FloorRangeTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(0, false)]
    [InlineData(11, false)]
    [InlineData(-3, false)]
    public void Contains_ReturnsWhetherTheFloorIsWithinTheBuilding(int floor, bool expected)
    {
        FloorRange range = new(lowest: 1, highest: 10);

        range.Contains(floor).Should().Be(expected);
    }

    [Fact]
    public void Constructor_ExposesTheBoundsItWasGiven()
    {
        FloorRange range = new(lowest: -2, highest: 30);

        range.Lowest.Should().Be(-2);
        range.Highest.Should().Be(30);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(5, 4)]
    [InlineData(0, -1)]
    public void Constructor_RejectsARangeWithoutAtLeastTwoFloors(int lowest, int highest)
    {
        Action construct = () => _ = new FloorRange(lowest, highest);

        construct.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(highest));
    }

    [Fact]
    public void OneToTen_IsTheBuildingDescribedByTheBrief()
    {
        FloorRange.OneToTen.Lowest.Should().Be(1);
        FloorRange.OneToTen.Highest.Should().Be(10);
    }
}
