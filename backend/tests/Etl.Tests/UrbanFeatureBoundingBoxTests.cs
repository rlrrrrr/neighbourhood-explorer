using NeighbourhoodExplorer.Etl.Osm;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class UrbanFeatureBoundingBoxTests
{
    [Fact]
    public void Expands_the_province_extent_by_the_margin()
    {
        var bounds = new UrbanFeatureBoundingBox(28, 40, 30, 42).Expand(0.02);

        Assert.Equal(27.98, bounds.MinLongitude, 10);
        Assert.Equal(39.98, bounds.MinLatitude, 10);
        Assert.Equal(30.02, bounds.MaxLongitude, 10);
        Assert.Equal(42.02, bounds.MaxLatitude, 10);
    }

    [Fact]
    public void Clamps_the_expanded_extent_to_the_coordinate_system()
    {
        var bounds = new UrbanFeatureBoundingBox(-179.99, -89.99, 179.99, 89.99).Expand(0.02);

        Assert.Equal(-180, bounds.MinLongitude, 10);
        Assert.Equal(-90, bounds.MinLatitude, 10);
        Assert.Equal(180, bounds.MaxLongitude, 10);
        Assert.Equal(90, bounds.MaxLatitude, 10);
    }

    [Fact]
    public void Formats_the_osm2pgsql_argument_with_invariant_culture()
    {
        var bounds = new UrbanFeatureBoundingBox(27.95, 40.72, 29.98, 41.69);

        Assert.Equal("27.95,40.72,29.98,41.69", bounds.ToOsm2pgsqlArgument());
    }
}
