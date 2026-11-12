using Npgsql;

namespace NeighbourhoodExplorer.Etl.Database;

public sealed record MetricCatalogWriteRequest(
    NpgsqlConnection Connection,
    NpgsqlTransaction Transaction,
    MetricCatalogSource Source,
    MetricDefinition Definition);
