using NeighbourhoodExplorer.Etl.Manifests;

namespace NeighbourhoodExplorer.Etl.Tests;

public class ExpectedCountsTests
{
    [Fact]
    public void Keeps_the_last_value_when_the_same_key_is_repeated()
    {
        var counts = ExpectedCounts.Empty
            .With(ExpectedCountField.Neighborhoods, 964)
            .With(ExpectedCountField.Neighborhoods, 964);

        Assert.True(counts.TryGet(ExpectedCountField.Neighborhoods, out var value));
        Assert.Equal(964, value);
    }

    [Fact]
    public void Rejects_conflicting_values_for_the_same_field()
    {
        var counts = ExpectedCounts.Empty.With(ExpectedCountField.Districts, 39);

        Assert.Throws<ManifestException>(
            () => counts.With(ExpectedCountField.Districts, 40));
    }

    [Fact]
    public void Names_a_missing_count_with_its_database_key()
    {
        var failure = Assert.Throws<EtlFailureException>(
            () => ExpectedCounts.Empty.Require(ExpectedCountField.BusStops, "ibb-transport"));

        Assert.Contains("expected_counts.bus_stops", failure.Message);
    }
}
