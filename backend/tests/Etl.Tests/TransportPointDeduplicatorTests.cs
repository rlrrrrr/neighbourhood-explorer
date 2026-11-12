using NeighbourhoodExplorer.Etl.Transport;

namespace NeighbourhoodExplorer.Etl.Tests;

public class TransportPointDeduplicatorTests
{
    [Fact]
    public void Merges_opposite_directions_of_a_bus_stop()
    {
        TransportPoint[] points =
        [
            new(TransportMode.Bus, "KADIKÖY", 29.0000, 41.0000),
            new(TransportMode.Bus, "KADIKÖY", 29.0004, 41.0000),
        ];

        var result = TransportPointDeduplicator.Deduplicate(points);

        Assert.Single(result.Points);
        Assert.Equal(2, result.Stats[0].Input);
        Assert.Equal(1, result.Stats[0].Distinct);
    }

    [Fact]
    public void Keeps_same_named_bus_stops_far_apart()
    {
        TransportPoint[] points =
        [
            new(TransportMode.Bus, "ATATÜRK CADDESİ", 29.0000, 41.0000),
            new(TransportMode.Bus, "ATATÜRK CADDESİ", 29.0100, 41.0000),
        ];

        var result = TransportPointDeduplicator.Deduplicate(points);

        Assert.Equal(2, result.Points.Count);
    }

    [Fact]
    public void Keeps_different_names_at_the_same_location()
    {
        TransportPoint[] points =
        [
            new(TransportMode.Bus, "KADIKÖY", 29.0000, 41.0000),
            new(TransportMode.Bus, "İSKELE", 29.0001, 41.0000),
        ];

        var result = TransportPointDeduplicator.Deduplicate(points);

        Assert.Equal(2, result.Points.Count);
    }

    [Fact]
    public void Merges_the_quays_of_a_ferry_terminal()
    {
        TransportPoint[] points =
        [
            new(TransportMode.Ferry, "BEŞİKTAŞ-1", 29.0000, 41.0000),
            new(TransportMode.Ferry, "BEŞİKTAŞ-2", 29.0008, 41.0000),
        ];

        var result = TransportPointDeduplicator.Deduplicate(points);

        Assert.Single(result.Points);
        Assert.Equal(1, result.Stats.Single(stats => stats.Mode == TransportMode.Ferry).Distinct);
    }

    [Fact]
    public void Deduplicates_each_mode_independently()
    {
        TransportPoint[] points =
        [
            new(TransportMode.Bus, "YENİKAPI", 29.0000, 41.0000),
            new(TransportMode.Rail, "YENİKAPI", 29.0000, 41.0000),
        ];

        var result = TransportPointDeduplicator.Deduplicate(points);

        Assert.Equal(2, result.Points.Count);
        Assert.Equal(1, result.Stats.Single(stats => stats.Mode == TransportMode.Bus).Distinct);
        Assert.Equal(1, result.Stats.Single(stats => stats.Mode == TransportMode.Rail).Distinct);
    }

    [Fact]
    public void Uses_a_wider_radius_for_rail_interchanges()
    {
        TransportPoint[] points =
        [
            new(TransportMode.Rail, "YENİKAPI", 29.0000, 41.0000),
            new(TransportMode.Rail, "YENİKAPI", 29.0012, 41.0000),
        ];

        var result = TransportPointDeduplicator.Deduplicate(points);

        Assert.Single(result.Points);
    }

    [Fact]
    public void Merges_a_chain_of_same_named_stops_through_transitive_proximity()
    {
        TransportPoint[] points =
        [
            new(TransportMode.Bus, "SAHİL", 29.0000, 41.0000),
            new(TransportMode.Bus, "SAHİL", 29.0006, 41.0000),
            new(TransportMode.Bus, "SAHİL", 29.0012, 41.0000),
        ];

        var result = TransportPointDeduplicator.Deduplicate(points);

        Assert.Single(result.Points);
        Assert.Equal(3, result.Stats[0].Input);
        Assert.Equal(1, result.Stats[0].Distinct);
    }

    [Fact]
    public void Merges_turkish_case_variants_of_the_same_name()
    {
        TransportPoint[] points =
        [
            new(TransportMode.Bus, "İSTANBUL", 29.0000, 41.0000),
            new(TransportMode.Bus, "istanbul", 29.0004, 41.0000),
        ];

        var result = TransportPointDeduplicator.Deduplicate(points);

        Assert.Single(result.Points);
    }
}
