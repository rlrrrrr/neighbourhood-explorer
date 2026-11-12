using NeighbourhoodExplorer.Etl.Osm;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class UrbanFeatureMetricCalculatorTests
{
    [Fact]
    public void Shannon_entropy_is_zero_without_objects()
    {
        Assert.Equal(0, UrbanFeatureMetricCalculator.LandUseDiversity([0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Shannon_entropy_is_zero_for_a_single_category()
    {
        Assert.Equal(0, UrbanFeatureMetricCalculator.LandUseDiversity([12, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Shannon_entropy_is_one_for_six_equal_categories()
    {
        Assert.Equal(1, UrbanFeatureMetricCalculator.LandUseDiversity([4, 4, 4, 4, 4, 4]), 10);
    }

    [Fact]
    public void Shannon_entropy_matches_two_equal_categories()
    {
        var expected = Math.Log(2) / Math.Log(6);

        Assert.Equal(
            expected,
            UrbanFeatureMetricCalculator.LandUseDiversity([5, 5, 0, 0, 0, 0]),
            10);
    }

    [Fact]
    public void Densities_are_counted_per_square_kilometre()
    {
        var metrics = UrbanFeatureMetricCalculator.Calculate(
        [
            Neighborhood(1, areaKm2: 2, cafeCount: 8),
            Neighborhood(2, areaKm2: 4, cafeCount: 2),
        ]);

        var cafes = metrics.Require("cafes_density");

        Assert.Equal([4, 0.5], cafes.Values.Select(value => value.RawValue));
        Assert.Equal(0, cafes.Values[1].NormalizedValue);
        Assert.Equal(1, cafes.Values[0].NormalizedValue);
    }

    [Fact]
    public void Education_and_healthcare_keep_raw_buffer_counts()
    {
        var metrics = UrbanFeatureMetricCalculator.Calculate(
        [
            Neighborhood(1, areaKm2: 1, educationCount: 3, healthcareCount: 0),
            Neighborhood(2, areaKm2: 1, educationCount: 7, healthcareCount: 2),
        ]);

        Assert.Equal([3, 7], metrics.Require("education_accessibility")
            .Values.Select(value => value.RawValue));
        Assert.Equal([0, 2], metrics.Require("healthcare_accessibility")
            .Values.Select(value => value.RawValue));
    }

    [Fact]
    public void Walkability_combines_intersection_and_daily_density_percentiles()
    {
        var metrics = UrbanFeatureMetricCalculator.Calculate(
        [
            Neighborhood(1, areaKm2: 1, dailyCount: 0, intersectionCount: 0),
            Neighborhood(2, areaKm2: 1, dailyCount: 10, intersectionCount: 4),
            Neighborhood(3, areaKm2: 1, dailyCount: 1, intersectionCount: 1),
        ]);

        var walkability = metrics.Require("walkability");

        Assert.Equal(0, walkability.Values[0].RawValue);
        Assert.Equal(1, walkability.Values[1].RawValue);
        Assert.Equal(0.5, walkability.Values[2].RawValue);
    }

    [Fact]
    public void Land_use_diversity_is_an_entropy_index()
    {
        var metrics = UrbanFeatureMetricCalculator.Calculate(
        [
            Neighborhood(1, areaKm2: 1, residentialCount: 10),
            Neighborhood(
                2,
                areaKm2: 1,
                residentialCount: 1,
                commercialCount: 1,
                foodCount: 1,
                landUseCultureCount: 1,
                officeCount: 1,
                greenCount: 1),
        ]);

        var diversity = metrics.Require("land_use_diversity");

        Assert.Equal(0, diversity.Values[0].RawValue);
        Assert.Equal(1, diversity.Values[1].RawValue);
        Assert.Equal(0, diversity.Values[0].NormalizedValue);
        Assert.Equal(1, diversity.Values[1].NormalizedValue);
    }

    [Fact]
    public void Produces_all_eight_metrics_for_every_neighborhood()
    {
        var metrics = UrbanFeatureMetricCalculator.Calculate(
        [
            Neighborhood(1, areaKm2: 1),
            Neighborhood(2, areaKm2: 1),
        ]);

        Assert.Equal(8, metrics.Series.Count);
        Assert.All(metrics.Series, series => Assert.Equal(2, series.Values.Count));
        Assert.Equal(
            UrbanFeatureMetricCatalog.All.Select(definition => definition.Key),
            metrics.Series.Select(series => series.Definition.Key));
    }

    [Fact]
    public void Rejects_a_neighborhood_without_area()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => UrbanFeatureMetricCalculator.Calculate([Neighborhood(1, areaKm2: 0)]));
    }

    [Fact]
    public void Shannon_entropy_is_bounded_and_permutation_invariant()
    {
        var random = new Random(20261111);
        for (var run = 0; run < 200; run++)
        {
            int[] counts = Enumerable.Range(0, 6).Select(_ => random.Next(0, 20)).ToArray();
            var permuted = counts.OrderBy(_ => random.Next()).ToArray();

            var entropy = UrbanFeatureMetricCalculator.LandUseDiversity(counts);

            Assert.InRange(entropy, 0, 1);
            Assert.Equal(entropy, UrbanFeatureMetricCalculator.LandUseDiversity(permuted), 12);
        }
    }

    private static UrbanFeatureNeighborhoodCounts Neighborhood(
        long id,
        double areaKm2,
        int cafeCount = 0,
        int restaurantCount = 0,
        int nightlifeCount = 0,
        int cultureCount = 0,
        int dailyCount = 0,
        int educationCount = 0,
        int healthcareCount = 0,
        int intersectionCount = 0,
        int residentialCount = 0,
        int commercialCount = 0,
        int foodCount = 0,
        int landUseCultureCount = 0,
        int officeCount = 0,
        int greenCount = 0) =>
        new(
            id,
            $"Mahalle {id}",
            "District",
            areaKm2,
            cafeCount,
            restaurantCount,
            nightlifeCount,
            cultureCount,
            dailyCount,
            educationCount,
            healthcareCount,
            intersectionCount,
            residentialCount,
            commercialCount,
            foodCount,
            landUseCultureCount,
            officeCount,
            greenCount);
}
