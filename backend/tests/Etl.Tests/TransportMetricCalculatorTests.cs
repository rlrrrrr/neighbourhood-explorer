using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Transport;

namespace NeighbourhoodExplorer.Etl.Tests;

public class TransportMetricCalculatorTests
{
    [Fact]
    public void Computes_the_weighted_density()
    {
        var neighborhood = new TransportNeighborhoodCount(
            1,
            "Kadıköy",
            "Moda",
            BusStops: 4,
            RailStations: 2,
            FerryPiers: 1,
            AreaKm2: 2);

        Assert.Equal(13, TransportMetricCalculator.WeightedPoints(neighborhood));
        Assert.Equal(6.5, TransportMetricCalculator.Density(neighborhood), 6);
    }

    [Fact]
    public void Returns_zero_when_the_area_is_zero()
    {
        var neighborhood = new TransportNeighborhoodCount(
            1,
            "Kadıköy",
            "Moda",
            BusStops: 4,
            RailStations: 2,
            FerryPiers: 1,
            AreaKm2: 0);

        Assert.Equal(0, TransportMetricCalculator.Density(neighborhood));
    }

    [Fact]
    public void Normalizes_densities_with_percentile_rank()
    {
        TransportNeighborhoodCount[] neighborhoods =
        [
            new(1, "A", "a", 1, 0, 0, 1),
            new(2, "A", "b", 5, 0, 0, 1),
            new(3, "A", "c", 10, 0, 0, 1),
        ];

        var metrics = TransportMetricCalculator.Calculate(neighborhoods);
        var series = Assert.Single(metrics.Series);

        Assert.Equal("transport_accessibility", series.Definition.Key);
        Assert.Equal("ok", series.Definition.QualityFlag.ToWireValue());
        Assert.Equal([1, 5, 10], series.Values.Select(value => value.RawValue));
        Assert.Equal([0, 0.5, 1], series.Values.Select(value => value.NormalizedValue));
    }

    [Fact]
    public void Gives_equal_densities_the_same_normalized_value()
    {
        TransportNeighborhoodCount[] neighborhoods =
        [
            new(1, "A", "a", 2, 0, 0, 1),
            new(2, "A", "b", 2, 0, 0, 1),
            new(3, "A", "c", 4, 0, 0, 1),
        ];

        var metrics = TransportMetricCalculator.Calculate(neighborhoods);

        Assert.Equal(
            [0, 0, 1],
            metrics.Series[0].Values.Select(value => value.NormalizedValue));
    }

    [Fact]
    public void Exposes_french_and_english_translations()
    {
        var definition = TransportMetricCatalog.TransportAccessibility;

        Assert.Equal("l'accès aux transports en commun", definition.French.Subject);
        Assert.Equal("access to public transport", definition.English.Subject);
        Assert.NotEmpty(definition.French.Description);
        Assert.NotEmpty(definition.English.Description);
    }
}
