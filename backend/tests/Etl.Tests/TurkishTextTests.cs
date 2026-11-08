using NeighbourhoodExplorer.Etl;

namespace NeighbourhoodExplorer.Etl.Tests;

public class TurkishTextTests
{
    [Theory]
    [InlineData("Kâğıthane", "kagithane")]
    [InlineData("İSTANBUL", "istanbul")]
    [InlineData("IŞIK", "isik")]
    [InlineData("Çiftlikköy", "ciftlikkoy")]
    [InlineData("Eyüpsultan", "eyupsultan")]
    [InlineData("  Yeni   Mahalle ", "yeni mahalle")]
    [InlineData("PİRİ PAŞA", "piri pasa")]
    public void Normalize_applies_turkish_case_and_folds_diacritics(string value, string expected)
    {
        Assert.Equal(expected, TurkishText.Normalize(value));
    }

    [Fact]
    public void Normalize_makes_istanbul_spellings_identical()
    {
        var variants = new[] { "İstanbul", "istanbul", "ISTANBUL", "İSTANBUL" };

        var normalized = variants.Select(TurkishText.Normalize).Distinct().ToArray();

        Assert.Single(normalized);
        Assert.Equal("istanbul", normalized[0]);
    }
}
