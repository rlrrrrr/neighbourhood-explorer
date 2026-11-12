namespace NeighbourhoodExplorer.Etl.Database;

public sealed record MetricValue(
    long AdminAreaId,
    double RawValue,
    double NormalizedValue) : IMetricValue;
