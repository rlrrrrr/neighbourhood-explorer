using NeighbourhoodExplorer.Etl.Ibb;

namespace NeighbourhoodExplorer.Etl.Tests;

public class IbbNeighborhoodAliasesTests
{
    [Fact]
    public void Every_alias_key_and_target_is_already_normalized()
    {
        foreach (var alias in IbbNeighborhoodAliases.All)
        {
            Assert.Equal(alias.District, IbbNameNormalizer.NormalizeDistrict(alias.District));
            Assert.Equal(alias.Neighborhood, IbbNameNormalizer.NormalizeNeighborhood(alias.Neighborhood));
            Assert.Equal(alias.OsmNeighborhood, IbbNameNormalizer.NormalizeNeighborhood(alias.OsmNeighborhood));
        }
    }

    [Fact]
    public void Alias_table_covers_the_orthographic_variants()
    {
        Assert.True(IbbNeighborhoodAliases.TryResolve("kadikoy", "19.may", out var mayis));
        Assert.Equal("19 mayis", mayis);

        Assert.True(IbbNeighborhoodAliases.TryResolve("kartal", "topselvi", out var topselvi));
        Assert.Equal("topselvi mahalesi", topselvi);

        Assert.True(IbbNeighborhoodAliases.TryResolve("beykoz", "ciftlik", out var ciftlik));
        Assert.Equal("cavusbasi ciftlik", ciftlik);
    }

    [Fact]
    public void Alias_table_covers_the_arbitrated_historical_variants()
    {
        Assert.Equal(31, IbbNeighborhoodAliases.All.Count);

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
            Assert.True(IbbNeighborhoodAliases.TryResolve(key.District, key.Neighborhood, out var resolved));
            Assert.Equal(expectedTarget, resolved);
        }
    }
}
