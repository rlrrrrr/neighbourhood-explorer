using NeighbourhoodExplorer.Etl.Ibb;

namespace NeighbourhoodExplorer.Etl.Tests;

public class IbbNeighborhoodMatcherTests
{
    private static readonly IbbNeighborhoodAliasTable Aliases = IbbNeighborhoodAliasTable.Load(
        Path.Combine(
            AppContext.BaseDirectory,
            "Ibb",
            "Aliases",
            "ibb-neighborhood-aliases.json"));

    [Fact]
    public void Match_uses_district_and_normalized_name_then_lists_candidates()
    {
        var rows = new[]
        {
            Row("KADIKÖY", "CAFERAĞA", 1),
            Row("KADIKÖY", "INCONNU", 2),
        };
        var osm = new[]
        {
            new OsmNeighborhood(10, "kadikoy", "caferaga", "Caferağa Mahallesi"),
            new OsmNeighborhood(11, "kadikoy", "osmanaga", "Osmanağa Mahallesi"),
        };

        var result = new IbbNeighborhoodMatcher(Aliases).Match(rows, osm);

        Assert.Equal(10, result.Rows[0].AdminAreaId);
        Assert.Equal("name", result.Rows[0].MatchMethod);
        Assert.Null(result.Rows[1].AdminAreaId);
        var unmatched = Assert.Single(result.Unmatched);
        Assert.Equal("INCONNU", unmatched.Neighborhood);
        Assert.Equal(["Osmanağa Mahallesi"], unmatched.Candidates);
    }

    [Fact]
    public void Match_applies_an_explicit_alias_and_records_it()
    {
        var rows = new[] { Row("KADIKÖY", "19.May", 1) };
        var osm = new[]
        {
            new OsmNeighborhood(10, "kadikoy", "19 mayis", "19 Mayıs Mahallesi"),
        };

        var result = new IbbNeighborhoodMatcher(Aliases).Match(rows, osm);

        Assert.Equal(10, result.Rows[0].AdminAreaId);
        Assert.Equal("alias", result.Rows[0].MatchMethod);
        var alias = Assert.Single(result.AliasMatches);
        Assert.Equal("19.May", alias.Neighborhood);
        Assert.Equal("19 Mayıs Mahallesi", alias.OsmNeighborhood);
    }

    [Fact]
    public void Match_does_not_reuse_a_neighborhood_for_two_rows()
    {
        var rows = new[]
        {
            Row("KADIKÖY", "CAFERAĞA", 1),
            Row("KADIKÖY", "CAFERAĞA", 2),
        };
        var osm = new[]
        {
            new OsmNeighborhood(10, "kadikoy", "caferaga", "Caferağa Mahallesi"),
        };

        var result = new IbbNeighborhoodMatcher(Aliases).Match(rows, osm);

        Assert.Equal(10, result.Rows[0].AdminAreaId);
        Assert.Null(result.Rows[1].AdminAreaId);
    }

    private static IbbBuildingRow Row(string district, string neighborhood, long uavt) => new(
        Id: (int)uavt,
        DistrictName: district,
        NeighborhoodName: neighborhood,
        MahalleUavt: uavt,
        BuildingsBefore1980: 0,
        BuildingsBetween1980And2000: 0,
        BuildingsAfter2000: 0,
        BuildingsWith1To4Floors: 0,
        BuildingsWith5To9Floors: 0,
        BuildingsWith9To19Floors: 0,
        DistrictNormalized: IbbNameNormalizer.NormalizeDistrict(district),
        NeighborhoodNormalized: IbbNameNormalizer.NormalizeNeighborhood(neighborhood));
}
