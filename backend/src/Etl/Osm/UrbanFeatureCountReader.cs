using Npgsql;

namespace NeighbourhoodExplorer.Etl.Osm;

public sealed record UrbanFeatureNeighborhoodCounts(
    long AdminAreaId,
    string Name,
    string DistrictName,
    double AreaKm2,
    int CafeCount,
    int RestaurantCount,
    int NightlifeCount,
    int CultureCount,
    int DailyCount,
    int EducationCount,
    int HealthcareCount,
    int IntersectionCount,
    int ResidentialCount,
    int CommercialCount,
    int FoodCount,
    int LandUseCultureCount,
    int OfficeCount,
    int GreenCount);

public sealed record UrbanFeaturePoiCategoryTotal(
    string Category,
    int BeforeDeduplication,
    int AfterDeduplication);

public sealed record UrbanFeatureStagingCounts(
    int PoiRows,
    int AreaRows,
    int HighwayRows,
    int DistrictCount);

public sealed record UrbanFeatureCountsResult(
    IReadOnlyList<UrbanFeatureNeighborhoodCounts> Neighborhoods,
    IReadOnlyList<UrbanFeaturePoiCategoryTotal> PoiCategories,
    UrbanFeatureStagingCounts Staging);

public static class UrbanFeatureCountReader
{
    private const int CountQueryTimeoutSeconds = 3600;

    public static async Task<UrbanFeatureCountsResult> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await ConfigureTransactionAsync(connection, transaction, cancellationToken);
        var neighborhoods = await ReadNeighborhoodsAsync(connection, transaction, cancellationToken);
        var categories = await ReadPoiCategoryTotalsAsync(connection, transaction, cancellationToken);
        var staging = await ReadStagingCountsAsync(connection, transaction, cancellationToken);
        return new UrbanFeatureCountsResult(neighborhoods, categories, staging);
    }

    private static async Task ConfigureTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SET LOCAL jit = off; SET LOCAL work_mem = '256MB';";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<UrbanFeatureNeighborhoodCounts>> ReadNeighborhoodsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CountQueryTimeoutSeconds;
        command.CommandText = NeighborhoodCountsSql();

        var neighborhoods = new List<UrbanFeatureNeighborhoodCounts>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            neighborhoods.Add(new UrbanFeatureNeighborhoodCounts(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDouble(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                reader.GetInt32(13),
                reader.GetInt32(14),
                reader.GetInt32(15),
                reader.GetInt32(16),
                reader.GetInt32(17)));
        }

        return neighborhoods;
    }

    private static async Task<IReadOnlyList<UrbanFeaturePoiCategoryTotal>> ReadPoiCategoryTotalsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CountQueryTimeoutSeconds;
        command.CommandText = $"""
            WITH {PoiCtes()},
            before AS (
                SELECT category, count(*)::int AS feature_count
                FROM labeled
                GROUP BY category
            ),
            after AS (
                SELECT category, count(*)::int AS feature_count
                FROM dedup
                GROUP BY category
            )
            SELECT coalesce(before.category, after.category),
                   coalesce(before.feature_count, 0),
                   coalesce(after.feature_count, 0)
            FROM before
            FULL OUTER JOIN after USING (category)
            ORDER BY 1
            """;

        var categories = new List<UrbanFeaturePoiCategoryTotal>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(new UrbanFeaturePoiCategoryTotal(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2)));
        }

        return categories;
    }

    private static async Task<UrbanFeatureStagingCounts> ReadStagingCountsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CountQueryTimeoutSeconds;
        command.CommandText = """
            SELECT (SELECT count(*) FROM osm_urban_poi)::int,
                   (SELECT count(*) FROM osm_urban_area)::int,
                   (SELECT count(*) FROM osm_urban_highway)::int,
                   (SELECT count(*) FROM admin_area WHERE level = 'district')::int
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new UrbanFeatureStagingCounts(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3))
            : new UrbanFeatureStagingCounts(0, 0, 0, 0);
    }

    private static string NeighborhoodCountsSql() => $"""
        WITH neighborhoods AS (
            SELECT n.id,
                   n.name,
                   d.name AS district_name,
                   ST_Area(ST_Transform(n.geometry, 32635)) / 1000000.0 AS area_km2,
                   n.geometry
            FROM admin_area n
            JOIN admin_area d ON d.id = n.parent_id
            WHERE n.level = 'neighborhood'
        ),
        {PoiCtes()},
        poi_counts AS (
            SELECT contained.admin_area_id,
                   d.category,
                   count(*)::int AS feature_count
            FROM dedup d
            CROSS JOIN LATERAL (
                SELECT a.id AS admin_area_id
                FROM admin_area a
                WHERE a.level = 'neighborhood'
                  AND ST_Covers(a.geometry, d.point)
                ORDER BY a.id
                LIMIT 1
            ) AS contained
            GROUP BY contained.admin_area_id, d.category
        ),
        poi_pivot AS (
            SELECT admin_area_id,
                   coalesce(max(feature_count) FILTER (WHERE category = 'cafe'), 0) AS cafe_count,
                   coalesce(max(feature_count) FILTER (WHERE category = 'restaurant'), 0) AS restaurant_count,
                   coalesce(max(feature_count) FILTER (WHERE category = 'nightlife'), 0) AS nightlife_count,
                   coalesce(max(feature_count) FILTER (WHERE category = 'culture'), 0) AS culture_count,
                   coalesce(max(feature_count) FILTER (WHERE category = 'daily'), 0) AS daily_count
            FROM poi_counts
            GROUP BY admin_area_id
        ),
        buffer_counts AS (
            SELECT within.admin_area_id,
                   d.category,
                   count(*)::int AS feature_count
            FROM dedup d
            CROSS JOIN LATERAL (
                SELECT a.id AS admin_area_id
                FROM admin_area a
                WHERE a.level = 'neighborhood'
                  AND ST_DWithin(a.geometry, d.point, 0.012)
                  AND ST_DWithin(a.geometry::geography, d.point::geography, 800)
            ) AS within
            WHERE d.category IN ('education', 'healthcare')
            GROUP BY within.admin_area_id, d.category
        ),
        buffer_pivot AS (
            SELECT admin_area_id,
                   coalesce(max(feature_count) FILTER (WHERE category = 'education'), 0) AS education_count,
                   coalesce(max(feature_count) FILTER (WHERE category = 'healthcare'), 0) AS healthcare_count
            FROM buffer_counts
            GROUP BY admin_area_id
        ),
        area_features AS (
            SELECT a.geom,
                   CASE
                       WHEN a.tags->>'amenity' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.FoodLandUseAmenities)})
                           THEN 'food'
                       WHEN a.tags->>'tourism' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.CultureTourism)})
                                OR a.tags->>'amenity' IN ({UrbanFeatureTagCatalog.SqlList([.. UrbanFeatureTagCatalog.CultureAmenities, "library"])})
                           THEN 'culture'
                       WHEN a.tags->>'office' IS NOT NULL
                                OR a.tags->>'landuse' = 'industrial'
                           THEN 'office'
                       WHEN a.tags->>'shop' IS NOT NULL
                                OR a.tags->>'landuse' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.CommercialLandUses)})
                                OR a.tags->>'building' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.CommercialBuildings)})
                           THEN 'commercial'
                       WHEN a.tags->>'landuse' = 'residential'
                                OR a.tags->>'building' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.ResidentialBuildings)})
                           THEN 'residential'
                       WHEN a.tags->>'landuse' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.GreenLandUses)})
                                OR a.tags->>'natural' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.GreenNaturals)})
                                OR a.tags->>'leisure' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.GreenLeisures)})
                           THEN 'green'
                   END AS category
            FROM osm_urban_area a
        ),
        land_use_counts AS (
            SELECT intersected.admin_area_id,
                   f.category,
                   count(*)::int AS feature_count
            FROM area_features f
            CROSS JOIN LATERAL (
                SELECT a.id AS admin_area_id
                FROM admin_area a
                WHERE a.level = 'neighborhood'
                  AND ST_Intersects(a.geometry, f.geom)
            ) AS intersected
            WHERE f.category IS NOT NULL
            GROUP BY intersected.admin_area_id, f.category
        ),
        land_use_pivot AS (
            SELECT admin_area_id,
                   coalesce(max(feature_count) FILTER (WHERE category = 'residential'), 0) AS residential_count,
                   coalesce(max(feature_count) FILTER (WHERE category = 'commercial'), 0) AS commercial_count,
                   coalesce(max(feature_count) FILTER (WHERE category = 'food'), 0) AS food_count,
                   coalesce(max(feature_count) FILTER (WHERE category = 'culture'), 0) AS culture_count,
                   coalesce(max(feature_count) FILTER (WHERE category = 'office'), 0) AS office_count,
                   coalesce(max(feature_count) FILTER (WHERE category = 'green'), 0) AS green_count
            FROM land_use_counts
            GROUP BY admin_area_id
        ),
        highway_points AS (
            SELECT ST_NPoints(h.geom) AS point_count,
                   dp.path[1] AS point_index,
                   dp.geom
            FROM osm_urban_highway h
            CROSS JOIN LATERAL ST_DumpPoints(h.geom) AS dp
        ),
        junctions AS (
            SELECT ST_SnapToGrid(geom, 0.000001) AS point
            FROM highway_points
            GROUP BY 1
            HAVING sum(CASE WHEN point_index IN (1, point_count) THEN 1 ELSE 2 END) >= 3
        ),
        junction_counts AS (
            SELECT contained.admin_area_id,
                   count(*)::int AS intersection_count
            FROM junctions j
            CROSS JOIN LATERAL (
                SELECT a.id AS admin_area_id
                FROM admin_area a
                WHERE a.level = 'neighborhood'
                  AND ST_Covers(a.geometry, j.point)
                ORDER BY a.id
                LIMIT 1
            ) AS contained
            GROUP BY contained.admin_area_id
        )
        SELECT n.id,
               n.name,
               n.district_name,
               n.area_km2,
               coalesce(p.cafe_count, 0),
               coalesce(p.restaurant_count, 0),
               coalesce(p.nightlife_count, 0),
               coalesce(p.culture_count, 0),
               coalesce(p.daily_count, 0),
               coalesce(b.education_count, 0),
               coalesce(b.healthcare_count, 0),
               coalesce(j.intersection_count, 0),
               coalesce(l.residential_count, 0),
               coalesce(l.commercial_count, 0),
               coalesce(l.food_count, 0),
               coalesce(l.culture_count, 0),
               coalesce(l.office_count, 0),
               coalesce(l.green_count, 0)
        FROM neighborhoods n
        LEFT JOIN poi_pivot p ON p.admin_area_id = n.id
        LEFT JOIN buffer_pivot b ON b.admin_area_id = n.id
        LEFT JOIN junction_counts j ON j.admin_area_id = n.id
        LEFT JOIN land_use_pivot l ON l.admin_area_id = n.id
        ORDER BY n.id
        """;

    private static string PoiCtes() => $"""
        poi AS (
            SELECT p.osm_type,
                   p.osm_id,
                   p.name,
                   p.tags,
                   ST_PointOnSurface(p.geom) AS point
            FROM osm_urban_poi p
        ),
        labeled AS (
            SELECT osm_type,
                   osm_id,
                   point,
                   coalesce(nullif({TurkishText.SqlNormalizeExpression("name")}, ''), osm_type || osm_id::text) AS dedupe_name,
                   category
            FROM poi
            CROSS JOIN LATERAL (
                VALUES
                    (CASE WHEN tags->>'amenity' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.CafeAmenities)}) THEN 'cafe' END),
                    (CASE WHEN tags->>'amenity' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.RestaurantAmenities)}) THEN 'restaurant' END),
                    (CASE WHEN tags->>'amenity' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.NightlifeAmenities)}) THEN 'nightlife' END),
                    (CASE WHEN tags->>'tourism' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.CultureTourism)})
                               OR tags->>'amenity' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.CultureAmenities)})
                          THEN 'culture' END),
                    (CASE WHEN tags->>'amenity' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.EducationAmenities)})
                          THEN 'education' END),
                    (CASE WHEN tags->>'amenity' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.HealthcareAmenities)})
                          THEN 'healthcare' END),
                    (CASE WHEN tags->>'amenity' IN ({UrbanFeatureTagCatalog.SqlList(UrbanFeatureTagCatalog.DailyAmenities)})
                               OR tags->>'shop' IS NOT NULL
                          THEN 'daily' END)
            ) AS c(category)
            WHERE category IS NOT NULL
        ),
        dedup AS (
            SELECT DISTINCT ON (category, dedupe_name, ST_SnapToGrid(point, 0.0005))
                   category,
                   point
            FROM labeled
            ORDER BY category,
                     dedupe_name,
                     ST_SnapToGrid(point, 0.0005),
                     (osm_type IN ('W', 'R')) DESC,
                     osm_id
        )
        """;
}
