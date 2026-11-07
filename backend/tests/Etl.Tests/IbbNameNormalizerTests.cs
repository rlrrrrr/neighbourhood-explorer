using NeighbourhoodExplorer.Etl.Ibb;

namespace NeighbourhoodExplorer.Etl.Tests;

public class IbbNameNormalizerTests
{
    [Theory]
    [InlineData("Caferağa Mahallesi", "caferaga")]
    [InlineData("Yeni Mahalle Mahallesi", "yeni")]
    [InlineData("Yeni Mahalle", "yeni")]
    [InlineData("Akevler Mahallesi 7 Sokak", "akevler mahallesi 7 sokak")]
    [InlineData("19.May", "19.may")]
    public void NormalizeNeighborhood_strips_repeated_suffixes(string value, string expected) =>
        Assert.Equal(expected, IbbNameNormalizer.NormalizeNeighborhood(value));

    [Theory]
    [InlineData("EYÜP", "eyupsultan")]
    [InlineData("Eyüpsultan", "eyupsultan")]
    [InlineData("Kâğıthane", "kagithane")]
    [InlineData("KAĞITHANE", "kagithane")]
    public void NormalizeDistrict_applies_the_eyup_alias(string value, string expected) =>
        Assert.Equal(expected, IbbNameNormalizer.NormalizeDistrict(value));
}
