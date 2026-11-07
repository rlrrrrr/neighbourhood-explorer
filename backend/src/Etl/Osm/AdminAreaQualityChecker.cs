using Npgsql;

namespace NeighbourhoodExplorer.Etl.Osm;

public sealed class AdminAreaQualityChecker(NpgsqlConnection connection, NpgsqlTransaction transaction)
{
    private const double SquareMetersPerSquareKilometer = 1_000_000;

    public async Task<AdminAreaQualityReport> BuildAsync(
        AdminAreaQualityRequest request,
        CancellationToken cancellationToken)
    {
        var counts = ReadCounts(request);
        var failures = new List<string>();

        foreach (var count in counts.Where(count => !count.Matches))
        {
            failures.Add($"nombre de {count.Level} : {count.Actual} au lieu de {count.Expected} attendu(s).");
        }

        if (request.Build.UnassignedNeighborhoods.Count > 0)
        {
            failures.Add($"{request.Build.UnassignedNeighborhoods.Count} mahalle(s) sans ilçe parent.");
        }

        return new AdminAreaQualityReport(
            DateTimeOffset.UtcNow,
            request.Manifest.Id,
            request.RawFilePath,
            request.InputSha256,
            request.Osm2pgsqlVersion,
            counts,
            await ReadDistrictCoverageAsync(request.Build.ProvinceId, cancellationToken),
            await ReadProvinceCoverageAsync(request.Build.ProvinceId, cancellationToken),
            await ReadBoundsAsync(request.Manifest.Id, cancellationToken),
            request.Build,
            failures);
    }

    private static IReadOnlyList<CountCheck> ReadCounts(AdminAreaQualityRequest request) =>
    [
        CreateCount("province", request.Build.ProvinceCount, request.Manifest.ExpectedCounts["province"]),
        CreateCount("district", request.Build.DistrictCount, request.Manifest.ExpectedCounts["district"]),
        CreateCount("neighborhood", request.Build.NeighborhoodCount, request.Manifest.ExpectedCounts["neighborhood"]),
    ];

    private static CountCheck CreateCount(string level, int actual, int expected) =>
        new(level, actual, expected, actual == expected);

    private Task<List<CoverageRow>> ReadDistrictCoverageAsync(
        long provinceId,
        CancellationToken cancellationToken) =>
        QueryAsync(
            """
            WITH parts AS (
                SELECT n.parent_id, ST_Transform(n.geometry, 32635) AS geom
                FROM admin_area n
                JOIN admin_area d ON d.id = n.parent_id
                WHERE n.level = 'neighborhood'
                  AND d.level = 'district'
                  AND d.parent_id = @province_id
            ), aggregated AS (
                SELECT parent_id, sum(ST_Area(geom)) AS sum_area, ST_Union(geom) AS union_geom
                FROM parts
                GROUP BY parent_id
            ), areas AS (
                SELECT d.id, d.name, ST_Transform(d.geometry, 32635) AS geom
                FROM admin_area d
                WHERE d.level = 'district' AND d.parent_id = @province_id
            )
            SELECT a.name,
                   ST_Area(a.geom),
                   COALESCE(g.sum_area, 0),
                   COALESCE(ST_Area(g.union_geom), 0),
                   ST_Area(ST_Intersection(a.geom, COALESCE(g.union_geom, ST_GeomFromText('POLYGON EMPTY', 32635)))),
                   ST_Area(ST_Difference(a.geom, COALESCE(g.union_geom, ST_GeomFromText('POLYGON EMPTY', 32635))))
            FROM areas a
            LEFT JOIN aggregated g ON g.parent_id = a.id
            ORDER BY a.name
            """,
            parameters => parameters.AddWithValue("province_id", provinceId),
            ReadCoverageRow,
            cancellationToken);

    private async Task<CoverageRow> ReadProvinceCoverageAsync(
        long provinceId,
        CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            """
            WITH parts AS (
                SELECT d.parent_id, ST_Transform(d.geometry, 32635) AS geom
                FROM admin_area d
                WHERE d.level = 'district' AND d.parent_id = @province_id
            ), aggregated AS (
                SELECT parent_id, sum(ST_Area(geom)) AS sum_area, ST_Union(geom) AS union_geom
                FROM parts
                GROUP BY parent_id
            ), area AS (
                SELECT name, ST_Transform(geometry, 32635) AS geom
                FROM admin_area
                WHERE id = @province_id
            )
            SELECT area.name,
                   ST_Area(area.geom),
                   COALESCE(g.sum_area, 0),
                   COALESCE(ST_Area(g.union_geom), 0),
                   ST_Area(ST_Intersection(area.geom, COALESCE(g.union_geom, ST_GeomFromText('POLYGON EMPTY', 32635)))),
                   ST_Area(ST_Difference(area.geom, COALESCE(g.union_geom, ST_GeomFromText('POLYGON EMPTY', 32635))))
            FROM area
            LEFT JOIN aggregated g ON g.parent_id = @province_id
            """,
            parameters => parameters.AddWithValue("province_id", provinceId),
            ReadCoverageRow,
            cancellationToken);

        return rows.Count == 1
            ? rows[0]
            : throw new EtlFailureException($"surface introuvable pour la province {provinceId}.");
    }

    private static CoverageRow ReadCoverageRow(NpgsqlDataReader reader) =>
        CreateCoverageRow(
            reader.GetString(0),
            reader.GetDouble(1),
            reader.GetDouble(2),
            reader.GetDouble(3),
            reader.GetDouble(4),
            reader.GetDouble(5));

    private static CoverageRow CreateCoverageRow(
        string area,
        double areaSquareMeters,
        double sumPartsSquareMeters,
        double unionPartsSquareMeters,
        double coveredSquareMeters,
        double gapSquareMeters)
    {
        var areaKm2 = areaSquareMeters / SquareMetersPerSquareKilometer;
        var sumPartsKm2 = sumPartsSquareMeters / SquareMetersPerSquareKilometer;
        var unionPartsKm2 = unionPartsSquareMeters / SquareMetersPerSquareKilometer;
        var coveredKm2 = coveredSquareMeters / SquareMetersPerSquareKilometer;
        var gapKm2 = gapSquareMeters / SquareMetersPerSquareKilometer;
        var overlapKm2 = Math.Max(0, sumPartsKm2 - unionPartsKm2);
        var spillKm2 = Math.Max(0, unionPartsKm2 - coveredKm2);

        return new CoverageRow(
            area,
            areaKm2,
            sumPartsKm2,
            unionPartsKm2,
            coveredKm2,
            gapKm2,
            overlapKm2,
            spillKm2,
            areaKm2 == 0 ? 0 : gapKm2 / areaKm2 * 100,
            areaKm2 == 0 ? 0 : overlapKm2 / areaKm2 * 100);
    }

    private async Task<BoundingBox> ReadBoundsAsync(string sourceId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ST_XMin(extent), ST_YMin(extent), ST_XMax(extent), ST_YMax(extent)
            FROM (SELECT ST_Extent(geometry) AS extent FROM admin_area WHERE source_id = @source_id) AS bounds
            """;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("source_id", sourceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
        {
            return new BoundingBox(0, 0, 0, 0);
        }

        return new BoundingBox(reader.GetDouble(0), reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3));
    }

    private async Task<List<T>> QueryAsync<T>(
        string sql,
        Action<NpgsqlParameterCollection> configure,
        Func<NpgsqlDataReader, T> read,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        configure(command.Parameters);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(read(reader));
        }

        return results;
    }
}
