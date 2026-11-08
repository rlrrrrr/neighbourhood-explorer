using NeighbourhoodExplorer.Etl.WorldPop;

namespace NeighbourhoodExplorer.Etl.Tests;

public class GdalInfoParserTests
{
    private const string GdalJson = """
        {
          "size": [2, 3],
          "geoTransform": [665000, 100, 0, 4563000, 0, -100],
          "coordinateSystem": {
            "wkt": "PROJCRS[\"WGS 84 / UTM zone 35N\",ID[\"EPSG\",32635]]"
          },
          "bands": [
            {
              "noDataValue": -99999,
              "minimum": 0,
              "maximum": 10,
              "mean": 5,
              "metadata": {
                "": {
                  "STATISTICS_VALID_PERCENT": "50"
                }
              }
            }
          ]
        }
        """;

    [Fact]
    public void Parse_reads_size_transform_band_stats_and_wkt()
    {
        var info = GdalInfoParser.Parse(GdalJson);

        Assert.Equal(2, info.Width);
        Assert.Equal(3, info.Height);
        Assert.Equal(100, info.PixelSizeX);
        Assert.Equal(-100, info.PixelSizeY);
        Assert.Equal(-99999, info.NoDataValue);
        Assert.Equal(0, info.Minimum);
        Assert.Equal(10, info.Maximum);
        Assert.Equal(5, info.Mean);
        Assert.Equal(50, info.ValidPercent);
        Assert.Contains("ID[\"EPSG\",32635]", info.Wkt, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_invalid_json()
    {
        Assert.Throws<EtlFailureException>(() => GdalInfoParser.Parse("{"));
    }

    [Fact]
    public void Parse_rejects_a_missing_field()
    {
        Assert.Throws<EtlFailureException>(() => GdalInfoParser.Parse("""{"size":[1,1]}"""));
    }
}
