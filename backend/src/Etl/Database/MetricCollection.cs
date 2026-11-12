namespace NeighbourhoodExplorer.Etl.Database;

public sealed record MetricCollection(IReadOnlyList<MetricSeries> Series)
{
    public static MetricCollection Empty { get; } = new([]);

    public MetricSeries Require(string key) =>
        Series.FirstOrDefault(
            series => string.Equals(series.Definition.Key, key, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"série de métrique absente : {key}");
}
