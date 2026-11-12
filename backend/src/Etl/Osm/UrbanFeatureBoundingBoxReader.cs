using NeighbourhoodExplorer.Etl.Infrastructure;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Osm;

public static class UrbanFeatureBoundingBoxReader
{
    public static async Task<UrbanFeatureBoundingBox> ReadAsync(
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
                "lancer osm-admin-boundaries avant osm-urban-features.");
        }

        var bounds = new UrbanFeatureBoundingBox(
            reader.GetDouble(0),
            reader.GetDouble(1),
            reader.GetDouble(2),
            reader.GetDouble(3));

        if (bounds.MinLongitude >= bounds.MaxLongitude
            || bounds.MinLatitude >= bounds.MaxLatitude)
        {
            throw new EtlFailureException("emprise provinciale invalide dans admin_area.");
        }

        return bounds;
    }
}
