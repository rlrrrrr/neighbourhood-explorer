using Npgsql;

namespace NeighbourhoodExplorer.Etl.WorldPop;

public static class WorldPopProvinceBoundsReader
{
    public static async Task<WorldPopBounds> ReadAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ST_XMin(bounds), ST_YMin(bounds), ST_XMax(bounds), ST_YMax(bounds)
            FROM (
                SELECT ST_Envelope(ST_Collect(ST_Transform(geometry, 32635))) AS bounds
                FROM admin_area
                WHERE level = 'province'
            ) AS province
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || await reader.IsDBNullAsync(0, cancellationToken))
        {
            throw new EtlFailureException(
                "admin_area ne contient aucune province : importer OSM avant WorldPop.");
        }

        return new WorldPopBounds(
            reader.GetDouble(0),
            reader.GetDouble(1),
            reader.GetDouble(2),
            reader.GetDouble(3));
    }
}
