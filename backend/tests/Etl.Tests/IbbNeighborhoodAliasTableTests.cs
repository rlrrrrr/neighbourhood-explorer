using NeighbourhoodExplorer.Etl.Ibb;

namespace NeighbourhoodExplorer.Etl.Tests;

public class IbbNeighborhoodAliasTableTests
{
    private static readonly IbbNeighborhoodAliasTable Aliases = IbbNeighborhoodAliasTable.Load(
        Path.Combine(
            AppContext.BaseDirectory,
            "Ibb",
            "Aliases",
            "ibb-neighborhood-aliases.json"));

    [Fact]
    public void Every_alias_key_and_target_is_already_normalized()
    {
        foreach (var alias in Aliases.All)
        {
            Assert.Equal(alias.District, IbbNameNormalizer.NormalizeDistrict(alias.District));
            Assert.Equal(alias.Neighborhood, IbbNameNormalizer.NormalizeNeighborhood(alias.Neighborhood));
            Assert.Equal(alias.OsmNeighborhood, IbbNameNormalizer.NormalizeNeighborhood(alias.OsmNeighborhood));
        }
    }

    [Fact]
    public void Alias_table_covers_the_orthographic_variants()
    {
        Assert.True(Aliases.TryResolve("kadikoy", "19.may", out var mayis));
        Assert.Equal("19 mayis", mayis);

        Assert.True(Aliases.TryResolve("kartal", "topselvi", out var topselvi));
        Assert.Equal("topselvi mahalesi", topselvi);

        Assert.True(Aliases.TryResolve("beykoz", "ciftlik", out var ciftlik));
        Assert.Equal("cavusbasi ciftlik", ciftlik);
    }

    [Fact]
    public void Alias_table_covers_the_arbitrated_historical_variants()
    {
        Assert.Equal(31, Aliases.All.Count);

        var expected = new Dictionary<(string District, string Neighborhood), string>
        {
            [("bagcilar", "evren")] = "15 temmuz",
            [("bayrampasa", "ortamahalle")] = "orta",
            [("esenler", "havaalani")] = "atisalani",
            [("sariyer", "sariyer merkez")] = "merkez",
            [("silivri", "balaban")] = "canta balaban",
            [("silivri", "fevzipasa")] = "degirmenkoy fevzipasa",
            [("silivri", "hurriyet")] = "kavakli hurriyet",
            [("silivri", "ismetpasa")] = "degirmenkoy ismetpasa",
            [("silivri", "kavakli")] = "kavakli istiklal",
            [("silivri", "sancaktepe")] = "canta sancaktepe",
            [("sile", "kizilca")] = "kizilcakoy",
            [("sisli", "izzet pasa")] = "izzetpasa",
        };

        foreach (var (key, expectedTarget) in expected)
        {
            Assert.True(Aliases.TryResolve(key.District, key.Neighborhood, out var resolved));
            Assert.Equal(expectedTarget, resolved);
        }
    }

    [Fact]
    public void Alias_table_rejects_a_duplicate_key()
    {
        var alias = new IbbNeighborhoodAlias("kadikoy", "19.may", "19 mayis");

        Assert.Throws<InvalidOperationException>(() =>
            IbbNeighborhoodAliasTable.From([alias, alias]));
    }
}
