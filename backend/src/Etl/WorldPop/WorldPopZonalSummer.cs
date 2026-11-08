using Npgsql;

namespace NeighbourhoodExplorer.Etl.WorldPop;

public sealed record WorldPopZeroNeighborhood(
    string DistrictName,
    string NeighborhoodName,
    double AreaKm2);

public sealed record WorldPopZonalSumResult(
    int NeighborhoodCount,
    int ZeroCount,
    double TotalRaw,
    IReadOnlyList<WorldPopZeroNeighborhood> Zeros);

public sealed record WorldPopZonalSumRequest(
    NpgsqlConnection Connection,
    NpgsqlTransaction Transaction,
    string SourceId,
    int GridYear,
    DateTimeOffset ComputedAt);

public static class WorldPopZonalSummer
{
    public static async Task<WorldPopZonalSumResult> ComputeAsync(
        WorldPopZonalSumRequest request,
        CancellationToken cancellationToken)
    {
        await using (var command = request.Connection.CreateCommand())
        {
            command.Transaction = request.Transaction;
            command.CommandTimeout = 1800;
            command.CommandText = ZonalSql;
            command.Parameters.AddWithValue("source_id", request.SourceId);
            command.Parameters.AddWithValue("grid_year", request.GridYear);
            command.Parameters.AddWithValue("computed_at", request.ComputedAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var summary = request.Connection.CreateCommand();
        summary.Transaction = request.Transaction;
        summary.CommandText = """
            SELECT count(*)::int,
                   count(*) FILTER (WHERE population_raw = 0)::int,
                   coalesce(sum(population_raw), 0)
            FROM worldpop_neighborhood_sum
            WHERE source_id = @source_id AND grid_year = @grid_year
            """;
        summary.Parameters.AddWithValue("source_id", request.SourceId);
        summary.Parameters.AddWithValue("grid_year", request.GridYear);

        int neighborhoodCount;
        int zeroCount;
        double totalRaw;
        await using (var reader = await summary.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            neighborhoodCount = reader.GetInt32(0);
            zeroCount = reader.GetInt32(1);
            totalRaw = reader.GetDouble(2);
        }

        await using var zeros = request.Connection.CreateCommand();
        zeros.Transaction = request.Transaction;
        zeros.CommandText = """
            SELECT d.name, n.name, s.area_km2
            FROM worldpop_neighborhood_sum s
            JOIN admin_area n ON n.id = s.admin_area_id
            JOIN admin_area d ON d.id = n.parent_id
            WHERE s.source_id = @source_id
              AND s.grid_year = @grid_year
              AND s.population_raw = 0
            ORDER BY d.name, n.name
            """;
        zeros.Parameters.AddWithValue("source_id", request.SourceId);
        zeros.Parameters.AddWithValue("grid_year", request.GridYear);

        var zeroNeighborhoods = new List<WorldPopZeroNeighborhood>();
        await using (var reader = await zeros.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                zeroNeighborhoods.Add(new WorldPopZeroNeighborhood(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetDouble(2)));
            }
        }

        return new WorldPopZonalSumResult(
            neighborhoodCount,
            zeroCount,
            totalRaw,
            zeroNeighborhoods);
    }

    private const string ZonalSql = """
        SET LOCAL jit = off;

        DELETE FROM worldpop_neighborhood_sum
        WHERE source_id = @source_id AND grid_year = @grid_year;

        WITH base AS MATERIALIZED (
            SELECT n.id, ST_Transform(n.geometry, 32635) AS geom
            FROM admin_area n
            WHERE n.level = 'neighborhood'
        ),
        neighborhoods AS MATERIALIZED (
            SELECT id,
                   geom,
                   ST_Buffer(ST_Boundary(geom), 150) AS band,
                   ST_Area(geom) / 1000000.0 AS area_km2
            FROM base
        ),
        summary AS MATERIALIZED (
            SELECT n.id,
                   sum((ST_SummaryStats(ST_Clip(r.rast, n.geom, true), 1, true)).sum) AS centroid_sum
            FROM neighborhoods n
            JOIN worldpop_raster r ON ST_Intersects(r.rast, n.geom)
            GROUP BY n.id
        ),
        band AS MATERIALIZED (
            SELECT n.id,
                   n.geom AS area,
                   ST_Clip(r.rast, n.band, true) AS rast
            FROM neighborhoods n
            JOIN worldpop_raster r ON ST_Intersects(r.rast, n.band)
        ),
        boundary AS (
            SELECT b.id,
                   b.area,
                   (p).geom AS pixel,
                   (p).val AS val
            FROM band b
            CROSS JOIN LATERAL ST_PixelAsPolygons(b.rast, 1, true) p
            WHERE ST_Intersects((p).geom, b.area)
        ),
        corrections AS (
            SELECT id,
                   sum(
                       val * ST_Area(ST_Intersection(pixel, area)) / ST_Area(pixel)
                       - CASE WHEN ST_Contains(area, ST_Centroid(pixel)) THEN val ELSE 0 END
                   ) AS correction
            FROM boundary
            GROUP BY id
        )
        INSERT INTO worldpop_neighborhood_sum
            (source_id, grid_year, admin_area_id, population_raw, area_km2, population_estimated, computed_at)
        SELECT @source_id,
               @grid_year,
               n.id,
               coalesce(s.centroid_sum, 0) + coalesce(c.correction, 0),
               n.area_km2,
               NULL,
               @computed_at
        FROM neighborhoods n
        LEFT JOIN summary s ON s.id = n.id
        LEFT JOIN corrections c ON c.id = n.id;
        """;
}
