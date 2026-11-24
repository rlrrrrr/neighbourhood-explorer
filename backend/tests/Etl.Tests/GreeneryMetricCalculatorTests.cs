using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Greenery;

namespace NeighbourhoodExplorer.Etl.Tests;

public class GreeneryMetricCalculatorTests
{
    [Fact]
    public void Computes_share_as_a_percentage_of_valid_pixels()
    {
        var count = new GreeneryNeighborhoodCount(1, "Kadıköy", "Moda", 30, 200);

        Assert.Equal(15, GreeneryMetricCalculator.Share(count)!.Value, 6);
    }

    [Fact]
    public void Returns_no_share_when_no_pixel_is_valid()
    {
        var count = new GreeneryNeighborhoodCount(1, "Kadıköy", "Moda", 0, 0);

        Assert.Null(GreeneryMetricCalculator.Share(count));
    }

    [Fact]
    public void Excludes_neighborhoods_without_valid_pixels_from_the_series()
    {
        GreeneryNeighborhoodCount[] counts =
        [
            new(1, "A", "a", 0, 0),
            new(2, "A", "b", 10, 100),
            new(3, "A", "c", 90, 100),
        ];

        var metrics = GreeneryMetricCalculator.Calculate(counts);
        var series = Assert.Single(metrics.Series);

        Assert.Equal([2L, 3L], series.Values.Select(value => value.AdminAreaId));
        Assert.Equal([10, 90], series.Values.Select(value => value.RawValue));
        Assert.Equal([0, 1], series.Values.Select(value => value.NormalizedValue));
    }

    [Fact]
    public void Normalizes_shares_with_percentile_rank()
    {
        GreeneryNeighborhoodCount[] counts =
        [
            new(1, "A", "a", 10, 100),
            new(2, "A", "b", 50, 100),
            new(3, "A", "c", 90, 100),
        ];

        var metrics = GreeneryMetricCalculator.Calculate(counts);
        var series = Assert.Single(metrics.Series);

        Assert.Equal("greenery", series.Definition.Key);
        Assert.Equal("estimated", series.Definition.QualityFlag.ToWireValue());
        Assert.Equal([0, 0.5, 1], series.Values.Select(value => value.NormalizedValue));
        Assert.Equal([10, 50, 90], series.Values.Select(value => value.RawValue));
    }

    [Fact]
    public void Gives_equal_shares_the_same_normalized_value()
    {
        GreeneryNeighborhoodCount[] counts =
        [
            new(1, "A", "a", 10, 100),
            new(2, "A", "b", 10, 100),
            new(3, "A", "c", 20, 100),
        ];

        var metrics = GreeneryMetricCalculator.Calculate(counts);

        Assert.Equal(
            [0, 0, 1],
            metrics.Series[0].Values.Select(value => value.NormalizedValue));
    }

    [Fact]
    public void Exposes_french_and_english_translations()
    {
        var definition = GreeneryMetricCatalog.Greenery;

        Assert.Equal("fr", definition.French.Locale);
        Assert.Equal("en", definition.English.Locale);
        Assert.Equal("les espaces verts", definition.French.Subject);
        Assert.Equal("green spaces", definition.English.Subject);
        Assert.NotEmpty(definition.French.Description);
        Assert.NotEmpty(definition.English.Description);
    }

    [Fact]
    public void Share_stays_bounded_and_grows_with_green_pixels()
    {
        var random = new Random(20261111);
        for (var run = 0; run < 200; run++)
        {
            var validPixels = random.Next(1, 10_000);
            var greenPixels = random.Next(0, validPixels + 1);
            var extraPixels = random.Next(0, validPixels - greenPixels + 1);

            var share = GreeneryMetricCalculator.Share(
                new GreeneryNeighborhoodCount(1, "A", "a", greenPixels, validPixels));
            var largerShare = GreeneryMetricCalculator.Share(
                new GreeneryNeighborhoodCount(1, "A", "a", greenPixels + extraPixels, validPixels));

            Assert.NotNull(share);
            Assert.NotNull(largerShare);
            Assert.InRange(share.Value, 0, 100);
            Assert.True(largerShare.Value >= share.Value);
        }
    }
}
