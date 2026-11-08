using Npgsql;
using NpgsqlTypes;

namespace NeighbourhoodExplorer.Etl.Osm;

public sealed class AdminAreaBuilder(NpgsqlConnection connection, NpgsqlTransaction transaction)
{
    private const double NeighborhoodDisplayToleranceDegrees = 0.0005;

    private sealed record ProvinceBoundary(long RelationId, string? Name, string? OfficialName, string? IsoCode);

    private sealed record DistrictBoundary(long RelationId, string? Name, string? OfficialName);

    private sealed record NeighborhoodCandidate(
        long RelationId,
        string? Name,
        string? OfficialName,
        long DistrictId,
        string DistrictName,
        int CandidateCount,
        string DistrictList);

    private sealed record NeighborhoodInsertion(
        int Inserted,
        IReadOnlyList<UnassignedNeighborhood> Unassigned,
        IReadOnlyList<AmbiguousNeighborhood> Ambiguous);

    public async Task<AdminAreaBuildResult> BuildAsync(AdminAreaBuildRequest request, CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            "DELETE FROM admin_area WHERE source_id = @source_id",
            parameters => parameters.AddWithValue("source_id", request.SourceId),
            cancellationToken);

        var province = await FindProvinceAsync(request.ProvinceCode, cancellationToken);
        var countryCode = ReadCountryCode(province);
        var provinceId = await InsertProvinceAsync(province, countryCode, request.SourceId, cancellationToken);
        var districts = await InsertDistrictsAsync(provinceId, countryCode, request.SourceId, cancellationToken);
        var neighborhoods = await InsertNeighborhoodsAsync(provinceId, countryCode, request.SourceId, cancellationToken);
        await SetProvinceDisplayGeometryAsync(provinceId, request.SourceId, cancellationToken);
        await SetNeighborhoodDisplayGeometryAsync(request.SourceId, cancellationToken);

        return new AdminAreaBuildResult(
            provinceId,
            province.Name!,
            countryCode,
            1,
            districts,
            neighborhoods.Inserted,
            await FindNamesNotMatchingAsync(request.SourceId, cancellationToken),
            await FindDuplicateNamesAsync(request.SourceId, cancellationToken),
            await FindDuplicateNamesWithinDistrictAsync(request.SourceId, cancellationToken),
            await FindInvalidGeometriesAsync(request.SourceId, cancellationToken),
            neighborhoods.Unassigned,
            neighborhoods.Ambiguous,
            await FindOutsideProvinceAsync(provinceId, cancellationToken));
    }

    private async Task<ProvinceBoundary> FindProvinceAsync(string provinceCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT relation_id, tags->>'name', tags->>'official_name', tags->>'ISO3166-2'
            FROM osm_admin_boundaries
            WHERE admin_level = 4
              AND (tags->>'ISO3166-2' = @code OR tags->>'ref' = @code)
            ORDER BY relation_id
            """;

        var matches = await QueryAsync(
            sql,
            parameters => parameters.AddWithValue("code", provinceCode),
            ReadProvince,
            cancellationToken);

        if (matches.Count == 1)
        {
            var match = matches[0];
            if (string.IsNullOrWhiteSpace(match.Name))
            {
                throw new EtlFailureException($"la province {match.RelationId} n'a pas de tag « name ».");
            }

            return match;
        }

        if (matches.Count > 1)
        {
            throw new EtlFailureException(
                $"plusieurs provinces portent le code « {provinceCode} » : " +
                string.Join(", ", matches.Select(match => match.RelationId)) + ".");
        }

        var available = await QueryAsync(
            """
            SELECT relation_id, tags->>'name', tags->>'official_name', tags->>'ISO3166-2'
            FROM osm_admin_boundaries
            WHERE admin_level = 4
            ORDER BY 2
            """,
            _ => { },
            ReadProvince,
            cancellationToken);

        throw new EtlFailureException(
            $"province introuvable pour le code « {provinceCode} ». Provinces de l'extrait : " +
            string.Join(" ; ", available.Select(province => $"{province.Name} ({province.IsoCode ?? "sans ISO3166-2"})")));
    }

    private async Task<long> InsertProvinceAsync(
        ProvinceBoundary province,
        string countryCode,
        string sourceId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO admin_area (
                parent_id, level, country_code, name, name_normalized, official_name, official_code,
                geometry, centroid, source_id, source_feature_id)
            SELECT NULL, 'province', @country_code, @name, @name_normalized, @official_name, @official_code,
                   ST_Multi(ST_CollectionExtract(ST_MakeValid(b.geom), 3)),
                   ST_PointOnSurface(ST_Multi(ST_CollectionExtract(ST_MakeValid(b.geom), 3))),
                   @source_id, b.relation_id::text
            FROM osm_admin_boundaries b
            WHERE b.relation_id = @relation_id
            RETURNING id
            """;

        var id = await ExecuteScalarAsync(
            sql,
            parameters =>
            {
                parameters.AddWithValue("country_code", countryCode);
                parameters.AddWithValue("name", province.Name!);
                parameters.AddWithValue("name_normalized", TurkishText.Normalize(province.Name!));
                AddNullableText(parameters, "official_name", province.OfficialName);
                AddNullableText(parameters, "official_code", province.IsoCode);
                parameters.AddWithValue("source_id", sourceId);
                parameters.AddWithValue("relation_id", province.RelationId);
            },
            cancellationToken);

        return id is long provinceId
            ? provinceId
            : throw new EtlFailureException(
                $"la province {province.RelationId} n'a pas de géométrie polygonale exploitable.");
    }

    private async Task<int> InsertDistrictsAsync(
        long provinceId,
        string countryCode,
        string sourceId,
        CancellationToken cancellationToken)
    {
        var districts = await QueryAsync(
            """
            SELECT b.relation_id, b.tags->>'name', b.tags->>'official_name'
            FROM osm_admin_boundaries b
            CROSS JOIN admin_area p
            WHERE p.id = @province_id
              AND b.admin_level = 6
              AND ST_Contains(p.geometry, ST_PointOnSurface(b.geom))
            ORDER BY b.tags->>'name'
            """,
            parameters => parameters.AddWithValue("province_id", provinceId),
            reader => new DistrictBoundary(
                reader.GetInt64(0),
                ReadNullableString(reader, 1),
                ReadNullableString(reader, 2)),
            cancellationToken);

        const string insertSql = """
            INSERT INTO admin_area (
                parent_id, level, country_code, name, name_normalized, official_name, official_code,
                geometry, centroid, source_id, source_feature_id)
            SELECT @parent_id, 'district', @country_code, @name, @name_normalized, @official_name, NULL,
                   ST_Multi(ST_CollectionExtract(ST_MakeValid(b.geom), 3)),
                   ST_PointOnSurface(ST_Multi(ST_CollectionExtract(ST_MakeValid(b.geom), 3))),
                   @source_id, b.relation_id::text
            FROM osm_admin_boundaries b
            WHERE b.relation_id = @relation_id
            """;

        foreach (var district in districts)
        {
            var name = RequireName(district.Name, district.RelationId);
            await ExecuteAsync(
                insertSql,
                parameters =>
                {
                    parameters.AddWithValue("parent_id", provinceId);
                    parameters.AddWithValue("country_code", countryCode);
                    parameters.AddWithValue("name", name);
                    parameters.AddWithValue("name_normalized", TurkishText.Normalize(name));
                    AddNullableText(parameters, "official_name", district.OfficialName);
                    parameters.AddWithValue("source_id", sourceId);
                    parameters.AddWithValue("relation_id", district.RelationId);
                },
                cancellationToken);
        }

        return districts.Count;
    }

    private async Task<NeighborhoodInsertion> InsertNeighborhoodsAsync(
        long provinceId,
        string countryCode,
        string sourceId,
        CancellationToken cancellationToken)
    {
        var candidates = await QueryAsync(
            """
            WITH candidates AS (
                SELECT b.relation_id,
                       b.tags->>'name' AS name,
                       b.tags->>'official_name' AS official_name,
                       d.id AS district_id,
                       d.name AS district_name,
                       ST_Contains(d.geometry, ST_PointOnSurface(b.geom)) AS contains_point,
                       ST_Area(ST_Transform(ST_Intersection(b.geom, d.geometry), 32635)) AS overlap_area
                FROM osm_admin_boundaries b
                CROSS JOIN admin_area p
                JOIN admin_area d ON d.level = 'district' AND d.parent_id = p.id
                WHERE p.id = @province_id
                  AND b.admin_level = 8
                  AND ST_Contains(p.geometry, ST_PointOnSurface(b.geom))
                  AND ST_Intersects(b.geom, d.geometry)
            ), ranked AS (
                SELECT *,
                       row_number() OVER (
                           PARTITION BY relation_id
                           ORDER BY contains_point DESC, overlap_area DESC, district_id) AS rank,
                       count(*) OVER (PARTITION BY relation_id) AS candidate_count
                FROM candidates
            ), district_lists AS (
                SELECT relation_id, string_agg(district_name, ', ' ORDER BY district_name) AS district_list
                FROM candidates
                GROUP BY relation_id
            )
            SELECT r.relation_id, r.name, r.official_name, r.district_id, r.district_name,
                   r.candidate_count, l.district_list
            FROM ranked r
            JOIN district_lists l ON l.relation_id = r.relation_id
            WHERE r.rank = 1
            ORDER BY r.district_name, r.name
            """,
            parameters => parameters.AddWithValue("province_id", provinceId),
            reader => new NeighborhoodCandidate(
                reader.GetInt64(0),
                ReadNullableString(reader, 1),
                ReadNullableString(reader, 2),
                reader.GetInt64(3),
                reader.GetString(4),
                (int)reader.GetInt64(5),
                reader.GetString(6)),
            cancellationToken);

        var unassigned = await QueryAsync(
            """
            SELECT b.relation_id, b.tags->>'name'
            FROM osm_admin_boundaries b
            CROSS JOIN admin_area p
            WHERE p.id = @province_id
              AND b.admin_level = 8
              AND ST_Contains(p.geometry, ST_PointOnSurface(b.geom))
              AND NOT EXISTS (
                  SELECT 1
                  FROM admin_area d
                  WHERE d.level = 'district'
                    AND d.parent_id = p.id
                    AND ST_Intersects(b.geom, d.geometry))
            ORDER BY b.tags->>'name'
            """,
            parameters => parameters.AddWithValue("province_id", provinceId),
            reader => new UnassignedNeighborhood(
                reader.GetInt64(0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ReadNullableString(reader, 1) ?? "(sans nom)"),
            cancellationToken);

        const string insertSql = """
            INSERT INTO admin_area (
                parent_id, level, country_code, name, name_normalized, official_name, official_code,
                geometry, centroid, source_id, source_feature_id)
            SELECT @parent_id, 'neighborhood', @country_code, @name, @name_normalized, @official_name, NULL,
                   ST_Multi(ST_CollectionExtract(ST_MakeValid(b.geom), 3)),
                   ST_PointOnSurface(ST_Multi(ST_CollectionExtract(ST_MakeValid(b.geom), 3))),
                   @source_id, b.relation_id::text
            FROM osm_admin_boundaries b
            WHERE b.relation_id = @relation_id
            """;

        var ambiguous = new List<AmbiguousNeighborhood>();
        foreach (var candidate in candidates)
        {
            var name = RequireName(candidate.Name, candidate.RelationId);
            if (candidate.CandidateCount > 1)
            {
                ambiguous.Add(new AmbiguousNeighborhood(
                    candidate.RelationId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    name,
                    candidate.DistrictList.Split(", ", StringSplitOptions.RemoveEmptyEntries)));
            }

            await ExecuteAsync(
                insertSql,
                parameters =>
                {
                    parameters.AddWithValue("parent_id", candidate.DistrictId);
                    parameters.AddWithValue("country_code", countryCode);
                    parameters.AddWithValue("name", name);
                    parameters.AddWithValue("name_normalized", TurkishText.Normalize(name));
                    AddNullableText(parameters, "official_name", candidate.OfficialName);
                    parameters.AddWithValue("source_id", sourceId);
                    parameters.AddWithValue("relation_id", candidate.RelationId);
                },
                cancellationToken);
        }

        return new NeighborhoodInsertion(candidates.Count, unassigned, ambiguous);
    }

    private Task<List<NameIssue>> FindNamesNotMatchingAsync(string sourceId, CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT parent.name, n.name, n.source_feature_id
            FROM admin_area n
            JOIN admin_area parent ON parent.id = n.parent_id
            WHERE n.source_id = @source_id
              AND n.level = 'neighborhood'
              AND n.name NOT ILIKE '%mahallesi'
            ORDER BY parent.name, n.name
            """,
            parameters => parameters.AddWithValue("source_id", sourceId),
            reader => new NameIssue(reader.GetString(0), reader.GetString(1), reader.GetString(2)),
            cancellationToken);

    private Task<List<DuplicateName>> FindDuplicateNamesAsync(string sourceId, CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT min(n.name), count(*)::int, string_agg(DISTINCT parent.name, ', ' ORDER BY parent.name)
            FROM admin_area n
            JOIN admin_area parent ON parent.id = n.parent_id
            WHERE n.source_id = @source_id
              AND n.level = 'neighborhood'
            GROUP BY n.name_normalized
            HAVING count(*) > 1
            ORDER BY count(*) DESC, n.name_normalized
            """,
            parameters => parameters.AddWithValue("source_id", sourceId),
            reader => new DuplicateName(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2).Split(", ", StringSplitOptions.RemoveEmptyEntries)),
            cancellationToken);

    private Task<List<DuplicateName>> FindDuplicateNamesWithinDistrictAsync(
        string sourceId,
        CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT parent.name || ' / ' || min(n.name), count(*)::int, parent.name
            FROM admin_area n
            JOIN admin_area parent ON parent.id = n.parent_id
            WHERE n.source_id = @source_id
              AND n.level = 'neighborhood'
            GROUP BY parent.name, n.name_normalized
            HAVING count(*) > 1
            ORDER BY parent.name, min(n.name)
            """,
            parameters => parameters.AddWithValue("source_id", sourceId),
            reader => new DuplicateName(reader.GetString(0), reader.GetInt32(1), [reader.GetString(2)]),
            cancellationToken);

    private Task<List<GeometryIssue>> FindInvalidGeometriesAsync(string sourceId, CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT b.relation_id, COALESCE(b.tags->>'name', '(sans nom)'), b.admin_level
            FROM osm_admin_boundaries b
            WHERE b.admin_level IN (4, 6, 8)
              AND NOT ST_IsValid(b.geom)
              AND b.relation_id::text IN (SELECT source_feature_id FROM admin_area WHERE source_id = @source_id)
            ORDER BY b.admin_level, 2
            """,
            parameters => parameters.AddWithValue("source_id", sourceId),
            reader => new GeometryIssue(
                reader.GetInt64(0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                reader.GetString(1),
                reader.GetInt32(2)),
            cancellationToken);

    private Task<List<OutsideProvinceArea>> FindOutsideProvinceAsync(
        long provinceId,
        CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT b.admin_level, b.relation_id, COALESCE(b.tags->>'name', '(sans nom)')
            FROM osm_admin_boundaries b
            CROSS JOIN admin_area p
            WHERE p.id = @province_id
              AND b.admin_level IN (6, 8)
              AND ST_Intersects(p.geometry, b.geom)
              AND NOT ST_Contains(p.geometry, ST_PointOnSurface(b.geom))
            ORDER BY b.admin_level, 3
            """,
            parameters => parameters.AddWithValue("province_id", provinceId),
            reader => new OutsideProvinceArea(
                reader.GetInt32(0),
                reader.GetInt64(1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                reader.GetString(2)),
            cancellationToken);

    private static ProvinceBoundary ReadProvince(NpgsqlDataReader reader) =>
        new(
            reader.GetInt64(0),
            ReadNullableString(reader, 1),
            ReadNullableString(reader, 2),
            ReadNullableString(reader, 3));

    private static string RequireName(string? name, long relationId) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new EtlFailureException($"la relation {relationId} n'a pas de tag « name ».")
            : name;

    private static string ReadCountryCode(ProvinceBoundary province)
    {
        if (province.IsoCode is null || province.IsoCode.Length < 2)
        {
            throw new EtlFailureException(
                $"la province {province.RelationId} n'a pas de code ISO3166-2 : pays impossible à déterminer.");
        }

        return province.IsoCode[..2].ToUpperInvariant();
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static void AddNullableText(NpgsqlParameterCollection parameters, string name, string? value) =>
        parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text)
        {
            Value = (object?)value ?? DBNull.Value,
        });

    private NpgsqlCommand CreateCommand(string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private Task SetProvinceDisplayGeometryAsync(
        long provinceId,
        string sourceId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE admin_area province
            SET display_geometry = ST_Multi(ST_CollectionExtract(ST_MakeValid(
                ST_Intersection(province.geometry, parts.geometry)), 3))
            FROM (
                SELECT district.parent_id AS province_id, ST_Union(district.geometry) AS geometry
                FROM admin_area district
                WHERE district.level = 'district' AND district.source_id = @source_id
                GROUP BY district.parent_id
            ) parts
            WHERE province.id = parts.province_id
              AND province.level = 'province'
              AND province.id = @province_id
            """;

        return ExecuteAsync(
            sql,
            parameters =>
            {
                parameters.AddWithValue("source_id", sourceId);
                parameters.AddWithValue("province_id", provinceId);
            },
            cancellationToken);
    }

    private Task SetNeighborhoodDisplayGeometryAsync(
        string sourceId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE admin_area
            SET display_geometry = ST_Multi(
                ST_SimplifyPreserveTopology(geometry, @tolerance))
            WHERE level = 'neighborhood' AND source_id = @source_id
            """;

        return ExecuteAsync(
            sql,
            parameters =>
            {
                parameters.AddWithValue("source_id", sourceId);
                parameters.AddWithValue("tolerance", NeighborhoodDisplayToleranceDegrees);
            },
            cancellationToken);
    }

    private async Task ExecuteAsync(
        string sql,
        Action<NpgsqlParameterCollection> configure,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(sql);
        configure(command.Parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<object?> ExecuteScalarAsync(
        string sql,
        Action<NpgsqlParameterCollection> configure,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(sql);
        configure(command.Parameters);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task<List<T>> QueryAsync<T>(
        string sql,
        Action<NpgsqlParameterCollection> configure,
        Func<NpgsqlDataReader, T> read,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(sql);
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
