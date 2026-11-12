using NeighbourhoodExplorer.Etl.Infrastructure;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Greenery;

public static class WorldCoverBoundsReader
{
    public static async Task<WorldCoverBounds> ReadAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT ST_XMin(extent), ST_YMin(extent), ST_XMax(extent), ST_YMax(extent)
            FROM (
                SELECT ST_Extent(geometry) AS extent
                FROM admin_area
                WHERE level = 'province'
            ) AS bounds
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || await reader.IsDBNullAsync(0, cancellationToken))
        {
            throw new EtlFailureException(
                "emprise introuvable : aucun polygone provincial dans admin_area ; " +
                "lancer osm-admin-boundaries avant worldcover-greenery.");
        }

        return new WorldCoverBounds(
            reader.GetDouble(0),
            reader.GetDouble(1),
            reader.GetDouble(2),
            reader.GetDouble(3));
    }
}
