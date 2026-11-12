using Npgsql;

namespace NeighbourhoodExplorer.Etl.Database;

public sealed record MetricSeriesWriteRequest(
    NpgsqlConnection Connection,
    NpgsqlTransaction Transaction,
    MetricCatalogSource Source,
    string ReferencePeriod,
    DateOnly ObservedAt,
    DateTimeOffset ComputedAt,
    IReadOnlyList<MetricSeries> Series);
