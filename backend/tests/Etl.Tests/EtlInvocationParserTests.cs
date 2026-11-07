using NeighbourhoodExplorer.Etl;

namespace NeighbourhoodExplorer.Etl.Tests;

public class EtlInvocationParserTests
{
    [Fact]
    public void Parses_source_and_province_with_equals()
    {
        var invocation = EtlInvocationParser.Parse(["osm-geofabrik-istanbul", "--province=TR-34"]);

        Assert.Equal("osm-geofabrik-istanbul", invocation.SourceId);
        Assert.Equal("TR-34", invocation.ProvinceCode);
    }

    [Fact]
    public void Parses_source_and_province_with_separate_value()
    {
        var invocation = EtlInvocationParser.Parse(["--province", "TR-34", "osm-geofabrik-istanbul"]);

        Assert.Equal("osm-geofabrik-istanbul", invocation.SourceId);
        Assert.Equal("TR-34", invocation.ProvinceCode);
    }

    [Fact]
    public void Rejects_unknown_option()
    {
        Assert.Throws<EtlUsageException>(() => EtlInvocationParser.Parse(["source", "--unknown=1"]));
    }

    [Fact]
    public void Rejects_missing_source()
    {
        Assert.Throws<EtlUsageException>(() => EtlInvocationParser.Parse(["--province=TR-34"]));
    }
}
