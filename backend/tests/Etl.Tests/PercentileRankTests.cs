using NeighbourhoodExplorer.Etl.WorldPop;

namespace NeighbourhoodExplorer.Etl.Tests;

public class PercentileRankTests
{
    [Fact]
    public void Normalize_returns_an_empty_list_for_no_values()
    {
        Assert.Empty(PercentileRank.Normalize([]));
    }

    [Fact]
    public void Normalize_returns_zero_for_a_single_value()
    {
        Assert.Equal([0.0], PercentileRank.Normalize([42.0]));
    }

    [Fact]
    public void Normalize_uses_the_same_formula_as_percent_rank()
    {
        var normalized = PercentileRank.Normalize([10.0, 20.0, 30.0, 40.0]);

        Assert.Equal([0.0, 1.0 / 3.0, 2.0 / 3.0, 1.0], normalized);
    }

    [Fact]
    public void Normalize_preserves_the_input_order()
    {
        var normalized = PercentileRank.Normalize([30.0, 10.0, 20.0]);

        Assert.Equal([1.0, 0.0, 0.5], normalized);
    }

    [Fact]
    public void Normalize_gives_tied_values_the_lowest_rank()
    {
        var normalized = PercentileRank.Normalize([10.0, 10.0, 20.0]);

        Assert.Equal([0.0, 0.0, 1.0], normalized);
    }

    [Fact]
    public void Normalize_returns_zero_when_every_value_is_tied()
    {
        var normalized = PercentileRank.Normalize([5.0, 5.0, 5.0]);

        Assert.Equal([0.0, 0.0, 0.0], normalized);
    }

    [Fact]
    public void Normalize_is_bounded_and_order_preserving_for_generated_values()
    {
        var random = new Random(20261111);
        for (var run = 0; run < 200; run++)
        {
            var values = Enumerable.Range(0, random.Next(1, 40))
                .Select(_ => Math.Round(random.NextDouble() * 100, 1))
                .ToArray();

            var normalized = PercentileRank.Normalize(values);

            Assert.Equal(values.Length, normalized.Count);
            Assert.All(normalized, value => Assert.InRange(value, 0, 1));
            for (var left = 0; left < values.Length; left++)
            {
                for (var right = 0; right < values.Length; right++)
                {
                    if (values[left] < values[right])
                    {
                        Assert.True(normalized[left] <= normalized[right]);
                    }
                    else if (values[left] == values[right])
                    {
                        Assert.Equal(normalized[left], normalized[right]);
                    }
                }
            }
        }
    }
}
