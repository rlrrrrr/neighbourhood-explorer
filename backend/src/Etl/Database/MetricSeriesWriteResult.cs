namespace NeighbourhoodExplorer.Etl.Database;

public sealed record MetricSeriesWriteResult(IReadOnlyDictionary<string, long> MetricIds);
