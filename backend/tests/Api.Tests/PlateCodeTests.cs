using NeighbourhoodExplorer.Api.Features.Geography;

namespace NeighbourhoodExplorer.Api.Tests;

public class PlateCodeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(34)]
    [InlineData(81)]
    public void TryCreate_accepts_a_valid_plate_code(int value)
    {
        Assert.True(PlateCode.TryCreate(value, out var plateCode));
        Assert.Equal(value, plateCode.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(82)]
    [InlineData(-1)]
    public void TryCreate_rejects_an_out_of_range_plate_code(int value)
    {
        Assert.False(PlateCode.TryCreate(value, out _));
    }

    [Theory]
    [InlineData("TR-06", 6)]
    [InlineData("TR-34", 34)]
    [InlineData("TR-81", 81)]
    public void TryParseOfficialCode_reads_the_official_code(string officialCode, int expected)
    {
        Assert.True(PlateCode.TryParseOfficialCode(officialCode, out var plateCode));
        Assert.Equal(expected, plateCode.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("TR-00")]
    [InlineData("TR-82")]
    [InlineData("TR-034")]
    [InlineData("34")]
    [InlineData("TR-AB")]
    public void TryParseOfficialCode_rejects_an_invalid_code(string? officialCode)
    {
        Assert.False(PlateCode.TryParseOfficialCode(officialCode, out _));
    }

    [Fact]
    public void ToOfficialCode_uses_two_digits()
    {
        Assert.True(PlateCode.TryCreate(6, out var plateCode));
        Assert.Equal("TR-06", plateCode.ToOfficialCode());
    }
}
