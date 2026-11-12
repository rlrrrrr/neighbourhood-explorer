using Npgsql;

namespace NeighbourhoodExplorer.Etl.Greenery;

public static class GreeneryZonalCounter
{
    public static async Task<IReadOnlyList<GreeneryNeighborhoodCount>> CountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 3600;
        command.CommandText = ZonalSql;

        var results = new List<GreeneryNeighborhoodCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GreeneryNeighborhoodCount(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetInt64(4)));
        }

        return results;
    }

    private const string ZonalSql = """
        SET LOCAL jit = off;

        SELECT n.id,
               d.name,
               n.name,
               coalesce(sum((value_count).count)
                        FILTER (WHERE (value_count).value IN (10, 20, 30)), 0)::bigint,
               coalesce(sum((value_count).count)
                        FILTER (WHERE (value_count).value <> 0), 0)::bigint
        FROM admin_area n
        JOIN admin_area d ON d.id = n.parent_id
        JOIN worldcover_raster r ON ST_Intersects(r.rast, n.geometry)
        CROSS JOIN LATERAL ST_ValueCount(ST_Clip(r.rast, n.geometry, true), 1, true)
            AS value_count
        WHERE n.level = 'neighborhood'
        GROUP BY n.id, d.name, n.name
        ORDER BY d.name, n.name
        """;
}
