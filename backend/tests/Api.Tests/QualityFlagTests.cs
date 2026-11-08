using System.Text.Json;
using NeighbourhoodExplorer.Api.Features.Metrics;

namespace NeighbourhoodExplorer.Api.Tests;

public class QualityFlagTests
{
    [Theory]
    [InlineData("ok", QualityFlag.Ok)]
    [InlineData("estimated", QualityFlag.Estimated)]
    [InlineData("stale", QualityFlag.Stale)]
    [InlineData("low_coverage", QualityFlag.LowCoverage)]
    [InlineData("inherited", QualityFlag.Inherited)]
    public void Parse_reads_every_wire_value(string raw, QualityFlag expected)
    {
        Assert.Equal(expected, QualityFlagValues.Parse(raw));
    }

    [Fact]
    public void Parse_rejects_an_unknown_value()
    {
        Assert.Throws<InvalidOperationException>(() => QualityFlagValues.Parse("unknown"));
    }

    [Fact]
    public void Serialization_uses_the_wire_value()
    {
        Assert.Equal("\"low_coverage\"", JsonSerializer.Serialize(QualityFlag.LowCoverage));
        Assert.Equal(
            QualityFlag.Inherited,
            JsonSerializer.Deserialize<QualityFlag>("\"inherited\""));
    }
}
