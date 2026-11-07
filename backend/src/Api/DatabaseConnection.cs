using Npgsql;

namespace NeighbourhoodExplorer.Api;

public sealed class DatabaseConnection(NpgsqlDataSource? dataSource)
{
    public NpgsqlDataSource? DataSource { get; } = dataSource;
}
