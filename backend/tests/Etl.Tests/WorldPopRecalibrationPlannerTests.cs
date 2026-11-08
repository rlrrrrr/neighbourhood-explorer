using NeighbourhoodExplorer.Etl.WorldPop;

namespace NeighbourhoodExplorer.Etl.Tests;

public class WorldPopRecalibrationPlannerTests
{
    [Fact]
    public void Build_matches_districts_and_flags_coefficient_outside_range()
    {
        var plan = BuildPlan(
            [District("2048", "Arnavutköy", "arnavutkoy", 100)],
            [new WorldPopOsmDistrict(10, "Arnavutköy", "arnavutkoy")],
            new Dictionary<long, WorldPopDistrictSum>
            {
                [10] = new(50, 2, 0),
            });

        Assert.Empty(plan.Failures);
        Assert.Empty(plan.UnmatchedDistricts);
        var coefficient = Assert.Single(plan.Coefficients);
        Assert.Equal(2, coefficient.Coefficient, 9);
        Assert.True(coefficient.Flagged);
        Assert.Equal(100, plan.TotalAdnks);
    }

    [Fact]
    public void Build_reports_unmatched_districts_and_zero_sums()
    {
        var plan = BuildPlan(
            [
                District("1", "Absent", "absent", 100),
                District("2", "Zero", "zero", 100),
            ],
            [
                new WorldPopOsmDistrict(10, "Zero", "zero"),
                new WorldPopOsmDistrict(11, "Orphan", "orphan"),
            ],
            new Dictionary<long, WorldPopDistrictSum>
            {
                [10] = new(0, 1, 1),
            },
            expectedDistricts: 2,
            expectedProvincePopulation: 200,
            provincePopulation: 200);

        Assert.Contains(plan.Failures, failure => failure.Contains("somme nulle"));
        Assert.Contains(plan.UnmatchedDistricts, item => item.Contains("Absent"));
        Assert.Contains(plan.UnmatchedDistricts, item => item.Contains("Orphan"));
        Assert.Empty(plan.Coefficients);
    }

    [Fact]
    public void Build_reports_count_and_province_mismatches()
    {
        var plan = BuildPlan(
            [District("1", "Un", "un", 100)],
            [new WorldPopOsmDistrict(10, "Un", "un")],
            new Dictionary<long, WorldPopDistrictSum>
            {
                [10] = new(100, 1, 0),
            },
            expectedDistricts: 2,
            expectedProvincePopulation: 200,
            provincePopulation: 101);

        Assert.Contains(plan.Failures, failure => failure.Contains("au lieu de 2"));
        Assert.Contains(plan.Failures, failure => failure.Contains("somme des ilçe"));
        Assert.Contains(plan.Failures, failure => failure.Contains("population provinciale"));
    }

    private static WorldPopPlan BuildPlan(
        IReadOnlyList<WorldPopTuikDistrict> tuikDistricts,
        IReadOnlyList<WorldPopOsmDistrict> osmDistricts,
        IReadOnlyDictionary<long, WorldPopDistrictSum> worldpopSums,
        int expectedDistricts = 1,
        int expectedProvincePopulation = 100,
        int provincePopulation = 100) =>
        WorldPopRecalibrationPlanner.Build(
            new WorldPopPlannerInput(
                tuikDistricts,
                new WorldPopTuikProvince("TR100", "İstanbul", provincePopulation),
                osmDistricts,
                worldpopSums,
                expectedDistricts,
                expectedProvincePopulation));

    private static WorldPopTuikDistrict District(
        string code,
        string name,
        string normalized,
        int population) =>
        new(code, name, normalized, population);
}
