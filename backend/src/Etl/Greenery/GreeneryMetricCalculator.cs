using NeighbourhoodExplorer.Etl.Database;

namespace NeighbourhoodExplorer.Etl.Greenery;

public sealed record GreeneryNeighborhoodCount(
    long AdminAreaId,
    string DistrictName,
    string NeighborhoodName,
    long GreenPixels,
    long ValidPixels);

public static class GreeneryMetricCalculator
{
    public static MetricCollection Calculate(IReadOnlyList<GreeneryNeighborhoodCount> neighborhoods)
    {
        var shares = neighborhoods
            .Select(neighborhood => Share(neighborhood))
            .ToArray();
        var normalized = PercentileRank.Normalize(shares);
        var values = neighborhoods
            .Select((neighborhood, index) => new MetricValue(
                neighborhood.AdminAreaId,
                shares[index],
                normalized[index]))
            .ToArray();

        return new MetricCollection(
            [new MetricSeries(GreeneryMetricCatalog.Greenery, values)]);
    }

    public static double Share(GreeneryNeighborhoodCount neighborhood) =>
        neighborhood.ValidPixels == 0
            ? 0
            : 100.0 * neighborhood.GreenPixels / neighborhood.ValidPixels;
}
