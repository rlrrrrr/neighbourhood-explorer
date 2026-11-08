using NeighbourhoodExplorer.Etl.Ibb;
using NeighbourhoodExplorer.Etl.Manifests;

namespace NeighbourhoodExplorer.Etl.Tests;

public class IbbBuildingsPreparerTests
{
    private static IbbDatastoreDocument LoadDocument()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ibb-buildings-sample.json");
        using var stream = File.OpenRead(path);
        return IbbDatastoreDocument.Parse(stream);
    }

    private static ExpectedCounts ValidExpectedCounts() => new()
    {
        Rows = 3,
        Districts = 3,
        Uavt = 3,
        Buildings = 71,
    };

    [Fact]
    public void Prepare_repairs_names_and_checks_counts()
    {
        var prepared = IbbBuildingsPreparer.Prepare(LoadDocument(), ValidExpectedCounts());

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
            ValidExpectedCounts());

        Assert.Contains(prepared.Failures, failure => failure.Contains("tranches d'âge"));
    }

    [Fact]
    public void Prepare_requires_expected_counts()
    {
        var prepared = IbbBuildingsPreparer.Prepare(LoadDocument(), ExpectedCounts.Empty);

        Assert.Contains(prepared.Failures, failure => failure.Contains("expected_counts.rows"));
    }
}
