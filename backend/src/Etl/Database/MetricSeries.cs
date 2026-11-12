namespace NeighbourhoodExplorer.Etl.Database;

public sealed record MetricSeries(
    MetricDefinition Definition,
    IReadOnlyList<IMetricValue> Values);
