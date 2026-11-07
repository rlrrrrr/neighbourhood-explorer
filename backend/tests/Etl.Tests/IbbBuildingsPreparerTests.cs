using NeighbourhoodExplorer.Etl.Ibb;

namespace NeighbourhoodExplorer.Etl.Tests;

public class IbbBuildingsPreparerTests
{
    private static IbbDatastoreDocument LoadDocument()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ibb-buildings-sample.json");
        using var stream = File.OpenRead(path);
        return IbbDatastoreDocument.Parse(stream);
    }

    private static Dictionary<string, int> ExpectedCounts() => new(StringComparer.Ordinal)
    {
        ["rows"] = 3,
        ["districts"] = 3,
        ["uavt"] = 3,
        ["buildings"] = 71,
    };

    [Fact]
    public void Prepare_repairs_names_and_checks_counts()
    {
        var prepared = IbbBuildingsPreparer.Prepare(LoadDocument(), ExpectedCounts());

        Assert.Empty(prepared.Failures);
        Assert.All(prepared.Counts, count => Assert.True(count.Matches));
        Assert.Equal(5, prepared.RepairedNames);
        Assert.Equal("ZEYTİNBURNU", prepared.Rows[1].DistrictName);
        Assert.Equal("zeytinburnu", prepared.Rows[1].DistrictNormalized);
        Assert.Equal("yesiltepe", prepared.Rows[1].NeighborhoodNormalized);
        Assert.Equal(71, prepared.Rows.Sum(row => row.BuildingsByAge));
    }

    [Fact]
    public void Prepare_reports_age_and_floor_mismatch()
    {
        var document = LoadDocument();
        var broken = document.Records[0] with { BuildingsWith9To19Floors = 6 };

        var prepared = IbbBuildingsPreparer.Prepare(
            new IbbDatastoreDocument(document.Total, [broken]),
            ExpectedCounts());

        Assert.Contains(prepared.Failures, failure => failure.Contains("tranches d'âge"));
    }

    [Fact]
    public void Prepare_requires_expected_counts()
    {
        var prepared = IbbBuildingsPreparer.Prepare(LoadDocument(), new Dictionary<string, int>());

        Assert.Contains(prepared.Failures, failure => failure.Contains("expected_counts.rows"));
    }
}
