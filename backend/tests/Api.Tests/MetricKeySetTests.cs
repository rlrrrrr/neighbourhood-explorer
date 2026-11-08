using NeighbourhoodExplorer.Api.Features.Metrics;

namespace NeighbourhoodExplorer.Api.Tests;

public class MetricKeySetTests
{
    [Fact]
    public void Parse_trims_deduplicates_and_sorts()
    {
        var keys = MetricKeySet.Parse("population_density, cafes,population_density, ,noise");

        Assert.Equal(["cafes", "noise", "population_density"], keys.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",,,")]
    public void Parse_returns_empty_for_blank_input(string? raw)
    {
        Assert.True(MetricKeySet.Parse(raw).IsEmpty);
    }
}
