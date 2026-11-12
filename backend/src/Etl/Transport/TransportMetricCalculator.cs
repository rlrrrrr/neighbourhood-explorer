using NeighbourhoodExplorer.Etl.Database;

namespace NeighbourhoodExplorer.Etl.Transport;

public static class TransportMetricCalculator
{
    public static MetricCollection Calculate(
        IReadOnlyList<TransportNeighborhoodCount> neighborhoods)
    {
        var densities = neighborhoods.Select(Density).ToArray();
        var normalized = PercentileRank.Normalize(densities);
        var values = neighborhoods
            .Select((neighborhood, index) => new MetricValue(
                neighborhood.AdminAreaId,
                densities[index],
                normalized[index]))
            .ToArray();

        return new MetricCollection(
            [new MetricSeries(TransportMetricCatalog.TransportAccessibility, values)]);
    }

    public static double WeightedPoints(TransportNeighborhoodCount neighborhood) =>
        (neighborhood.BusStops * TransportMetricCatalog.BusWeight)
        + (neighborhood.RailStations * TransportMetricCatalog.RailWeight)
        + (neighborhood.FerryPiers * TransportMetricCatalog.FerryWeight);

    public static double Density(TransportNeighborhoodCount neighborhood) =>
        neighborhood.AreaKm2 <= 0
            ? 0
            : WeightedPoints(neighborhood) / neighborhood.AreaKm2;
}
