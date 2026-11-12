using Npgsql;

namespace NeighbourhoodExplorer.Etl.Database;

public sealed record MetricValueWriteRequest(
    NpgsqlConnection Connection,
    NpgsqlTransaction Transaction,
    long MetricId,
    DateOnly ObservedAt,
    string ReferencePeriod,
    MetricQualityFlag QualityFlag,
    DateTimeOffset ComputedAt,
    IReadOnlyList<IMetricValue> Values);
