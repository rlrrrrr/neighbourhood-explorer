using Npgsql;

namespace NeighbourhoodExplorer.Etl.Transport;

public sealed record TransportProvinceFilterResult(
    IReadOnlyList<TransportPoint> Points,
    IReadOnlyList<TransportPoint> Excluded);

public static class TransportProvinceFilter
{
    public static async Task<TransportProvinceFilterResult> ApplyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<TransportPoint> points,
        CancellationToken cancellationToken)
    {
        if (points.Count == 0)
        {
            return new TransportProvinceFilterResult([], []);
        }

        await CreateProvincePartsAsync(connection, transaction, cancellationToken);
        var inside = await ReadInsideFlagsAsync(connection, transaction, points, cancellationToken);

        var kept = new List<TransportPoint>(points.Count);
        var excluded = new List<TransportPoint>();
        for (var index = 0; index < points.Count; index++)
        {
            if (inside[index])
            {
                kept.Add(points[index]);
            }
            else
            {
                excluded.Add(points[index]);
            }
        }

        return new TransportProvinceFilterResult(kept, excluded);
    }

    private static async Task CreateProvincePartsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TEMP TABLE transport_province_part ON COMMIT DROP AS
            SELECT ST_Subdivide(province.geometry, 256) AS geometry
            FROM admin_area province
            WHERE province.level = 'province';

            CREATE INDEX ON transport_province_part USING gist (geometry);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool[]> ReadInsideFlagsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<TransportPoint> points,
        CancellationToken cancellationToken)
    {
        var flags = new bool[points.Count];
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT source.ordinality - 1 AS position,
                   EXISTS (
                       SELECT 1
                       FROM transport_province_part part
                       WHERE part.geometry && point.geom
                         AND ST_Covers(part.geometry, point.geom)
                   ) AS inside
            FROM unnest(@longitudes, @latitudes) WITH ORDINALITY
                AS source(longitude, latitude, ordinality)
            CROSS JOIN LATERAL (
                SELECT ST_SetSRID(ST_MakePoint(source.longitude, source.latitude), 4326) AS geom
            ) point
            """;
        command.Parameters.AddWithValue(
            "longitudes",
            points.Select(point => point.Longitude).ToArray());
        command.Parameters.AddWithValue(
            "latitudes",
            points.Select(point => point.Latitude).ToArray());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            flags[reader.GetInt32(0)] = reader.GetBoolean(1);
        }

        return flags;
    }
}
