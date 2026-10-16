using Npgsql;

namespace NeighbourhoodExplorer.Api;

public sealed class DatabaseHealthProbe(NpgsqlDataSource? dataSource)
{
    public NpgsqlDataSource? DataSource { get; } = dataSource;
}
