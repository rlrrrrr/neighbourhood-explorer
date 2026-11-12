using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Manifests;

namespace NeighbourhoodExplorer.Etl.Tests;

public class MetricVocabularyTests
{
    [Fact]
    public void Maps_the_vocabulary_to_the_database_wire_values()
    {
        Assert.Equal("province", MetricNativeLevel.Province.ToWireValue());
        Assert.Equal("district", MetricNativeLevel.District.ToWireValue());
        Assert.Equal("neighborhood", MetricNativeLevel.Neighborhood.ToWireValue());
        Assert.Equal("higher_is_better", MetricPolarity.HigherIsBetter.ToWireValue());
        Assert.Equal("lower_is_better", MetricPolarity.LowerIsBetter.ToWireValue());
        Assert.Equal("neutral", MetricPolarity.Neutral.ToWireValue());
        Assert.Equal("percentile", MetricNormalizationMethod.Percentile.ToWireValue());
        Assert.Equal("ok", MetricQualityFlag.Ok.ToWireValue());
        Assert.Equal("estimated", MetricQualityFlag.Estimated.ToWireValue());
        Assert.Equal("low_coverage", MetricQualityFlag.LowCoverage.ToWireValue());
        Assert.Equal("inherited", MetricQualityFlag.Inherited.ToWireValue());
    }

    [Theory]
    [InlineData("district", ExpectedCountField.Districts)]
    [InlineData("districts", ExpectedCountField.Districts)]
    [InlineData("neighborhood", ExpectedCountField.Neighborhoods)]
    [InlineData("neighborhoods", ExpectedCountField.Neighborhoods)]
    [InlineData("province_population", ExpectedCountField.ProvincePopulation)]
    [InlineData("bus_stops", ExpectedCountField.BusStops)]
    public void Reads_expected_count_keys_and_their_aliases(
        string key,
        ExpectedCountField expected)
    {
        Assert.True(ExpectedCountFieldNames.TryParse(key, out var field));
        Assert.Equal(expected, field);
    }

    [Fact]
    public void Rejects_an_unknown_expected_count_key()
    {
        Assert.False(ExpectedCountFieldNames.TryParse("unknown", out _));
    }
}
