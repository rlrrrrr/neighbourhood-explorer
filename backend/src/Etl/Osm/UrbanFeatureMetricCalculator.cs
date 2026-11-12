using NeighbourhoodExplorer.Etl.Database;

namespace NeighbourhoodExplorer.Etl.Osm;

public static class UrbanFeatureMetricCalculator
{
    public static MetricCollection Calculate(
        IReadOnlyList<UrbanFeatureNeighborhoodCounts> neighborhoods)
    {
        if (neighborhoods.Count == 0)
        {
            return MetricCollection.Empty;
        }

        foreach (var neighborhood in neighborhoods)
        {
            if (!double.IsFinite(neighborhood.AreaKm2) || neighborhood.AreaKm2 <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(neighborhoods),
                    $"surface invalide pour {neighborhood.Name} ({neighborhood.AdminAreaId}) : " +
                    $"{neighborhood.AreaKm2}.");
            }
        }

        var walkability = BuildWalkability(neighborhoods);
        var landUseDiversity = BuildSeries(
            RequireDefinition("land_use_diversity"),
            neighborhoods,
            neighborhood => LandUseDiversity(
                [
                    neighborhood.ResidentialCount,
                    neighborhood.CommercialCount,
                    neighborhood.FoodCount,
                    neighborhood.LandUseCultureCount,
                    neighborhood.OfficeCount,
                    neighborhood.GreenCount,
                ]));

        return new MetricCollection(
        [
            BuildDensitySeries("cafes_density", neighborhoods, neighborhood => neighborhood.CafeCount),
            BuildDensitySeries(
                "restaurant_density",
                neighborhoods,
                neighborhood => neighborhood.RestaurantCount),
            BuildDensitySeries("nightlife", neighborhoods, neighborhood => neighborhood.NightlifeCount),
            BuildDensitySeries("culture", neighborhoods, neighborhood => neighborhood.CultureCount),
            BuildSeries(
                RequireDefinition("education_accessibility"),
                neighborhoods,
                neighborhood => neighborhood.EducationCount),
            BuildSeries(
                RequireDefinition("healthcare_accessibility"),
                neighborhoods,
                neighborhood => neighborhood.HealthcareCount),
            walkability,
            landUseDiversity,
        ]);
    }

    public static double LandUseDiversity(IReadOnlyList<int> categoryCounts)
    {
        var total = categoryCounts.Sum();
        if (total <= 0 || categoryCounts.Count < 2)
        {
            return 0;
        }

        var entropy = 0.0;
        foreach (var count in categoryCounts)
        {
            if (count <= 0)
            {
                continue;
            }

            var share = count / (double)total;
            entropy -= share * Math.Log(share);
        }

        return Math.Clamp(Math.Round(entropy / Math.Log(categoryCounts.Count), 12), 0, 1);
    }

    private static MetricSeries BuildDensitySeries(
        string key,
        IReadOnlyList<UrbanFeatureNeighborhoodCounts> neighborhoods,
        Func<UrbanFeatureNeighborhoodCounts, int> countSelector) =>
        BuildSeries(
            RequireDefinition(key),
            neighborhoods,
            neighborhood => countSelector(neighborhood) / neighborhood.AreaKm2);

    private static MetricSeries BuildWalkability(
        IReadOnlyList<UrbanFeatureNeighborhoodCounts> neighborhoods)
    {
        var intersectionDensity = neighborhoods
            .Select(neighborhood => neighborhood.IntersectionCount / neighborhood.AreaKm2)
            .ToArray();
        var dailyDensity = neighborhoods
            .Select(neighborhood => neighborhood.DailyCount / neighborhood.AreaKm2)
            .ToArray();

        var intersectionPercentile = PercentileRank.Normalize(intersectionDensity);
        var dailyPercentile = PercentileRank.Normalize(dailyDensity);

        return BuildSeries(
            RequireDefinition("walkability"),
            neighborhoods,
            (_, index) => 0.5 * intersectionPercentile[index] + 0.5 * dailyPercentile[index]);
    }

    private static MetricSeries BuildSeries(
        MetricDefinition definition,
        IReadOnlyList<UrbanFeatureNeighborhoodCounts> neighborhoods,
        Func<UrbanFeatureNeighborhoodCounts, double> valueSelector) =>
        BuildSeries(definition, neighborhoods, (neighborhood, _) => valueSelector(neighborhood));

    private static MetricSeries BuildSeries(
        MetricDefinition definition,
        IReadOnlyList<UrbanFeatureNeighborhoodCounts> neighborhoods,
        Func<UrbanFeatureNeighborhoodCounts, int, double> valueSelector)
    {
        var rawValues = neighborhoods
            .Select(valueSelector)
            .ToArray();
        var normalizedValues = PercentileRank.Normalize(rawValues);

        var values = neighborhoods
            .Select((neighborhood, index) => new MetricValue(
                neighborhood.AdminAreaId,
                rawValues[index],
                normalizedValues[index]))
            .ToArray();

        return new MetricSeries(definition, values);
    }

    private static MetricDefinition RequireDefinition(string key) =>
        UrbanFeatureMetricCatalog.All.FirstOrDefault(
            definition => string.Equals(definition.Key, key, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"définition de métrique absente : {key}");
}
