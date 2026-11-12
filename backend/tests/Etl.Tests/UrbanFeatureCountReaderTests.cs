using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Osm;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class UrbanFeatureCountReaderTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgis/postgis:16-3.4")
        .Build();

    private NpgsqlDataSource dataSource = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        dataSource = NpgsqlDataSource.Create(container.GetConnectionString());
        await EtlSchema.EnsureAsync(dataSource, CancellationToken.None);
        await CreateStagingTablesAsync();
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await dataSource.DisposeAsync();
        await container.DisposeAsync();
    }

    [Fact]
    public async Task Counts_pois_buffers_land_use_and_intersections()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        var counts = await UrbanFeatureCountReader.ReadAsync(
            connection,
            transaction,
            CancellationToken.None);

        Assert.Equal(1, counts.Staging.DistrictCount);
        Assert.Equal(3, counts.Staging.HighwayRows);
        Assert.True(counts.Staging.PoiRows > 0);

        var neighborhood = Assert.Single(counts.Neighborhoods);
        Assert.Equal("Test Mahallesi", neighborhood.Name);
        Assert.Equal(1, neighborhood.CafeCount);
        Assert.Equal(1, neighborhood.RestaurantCount);
        Assert.Equal(1, neighborhood.NightlifeCount);
        Assert.Equal(1, neighborhood.CultureCount);
        Assert.Equal(3, neighborhood.DailyCount);
        Assert.Equal(2, neighborhood.EducationCount);
        Assert.Equal(2, neighborhood.HealthcareCount);
        Assert.Equal(1, neighborhood.IntersectionCount);
        Assert.Equal(1, neighborhood.ResidentialCount);
        Assert.Equal(1, neighborhood.CommercialCount);
        Assert.Equal(2, neighborhood.FoodCount);
        Assert.Equal(1, neighborhood.LandUseCultureCount);
        Assert.Equal(1, neighborhood.OfficeCount);
        Assert.Equal(1, neighborhood.GreenCount);

        var cafe = Assert.Single(counts.PoiCategories, category => category.Category == "cafe");
        Assert.Equal(2, cafe.BeforeDeduplication);
        Assert.Equal(1, cafe.AfterDeduplication);
    }

    [Fact]
    public async Task Writes_the_catalog_and_one_value_per_neighborhood_and_metric()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        var counts = await UrbanFeatureCountReader.ReadAsync(
            connection,
            transaction,
            CancellationToken.None);
        var metrics = UrbanFeatureMetricCalculator.Calculate(counts.Neighborhoods);

        var result = await MetricSeriesWriter.WriteAsync(
            new MetricSeriesWriteRequest(
                connection,
                transaction,
                new MetricCatalogSource(
                    "osm-test",
                    "OpenStreetMap de test",
                    "OpenStreetMap contributors",
                    "https://example.invalid/turkey.osm.pbf",
                    "ODbL 1.0",
                    "© les contributeurs d'OpenStreetMap",
                    UrbanFeatureMetricCatalog.SourceMethodology,
                    new DateOnly(2026, 10, 31),
                    "test-version"),
                "2026-10-30",
                new DateOnly(2026, 10, 30),
                DateTimeOffset.UtcNow,
                metrics.Series),
            CancellationToken.None);

        Assert.Equal(8, result.MetricIds.Count);
        Assert.Equal(
            UrbanFeatureMetricCatalog.All.Select(definition => definition.Key),
            result.MetricIds.Keys);

        Assert.Equal(1, await ScalarLongAsync(
            connection,
            transaction,
            "SELECT count(*) FROM data_source WHERE id = 'osm-test'"));
        Assert.Equal(8, await ScalarLongAsync(
            connection,
            transaction,
            "SELECT count(*) FROM metric_definition WHERE source_id = 'osm-test'"));
        Assert.Equal(16, await ScalarLongAsync(
            connection,
            transaction,
            """
            SELECT count(*)
            FROM metric_translation t
            JOIN metric_definition m ON m.id = t.metric_id
            WHERE m.source_id = 'osm-test'
            """));
        Assert.Equal(8, await ScalarLongAsync(
            connection,
            transaction,
            """
            SELECT count(*)
            FROM metric_value v
            JOIN metric_definition m ON m.id = v.metric_id
            WHERE m.source_id = 'osm-test'
            """));
        Assert.Equal(8, await ScalarLongAsync(
            connection,
            transaction,
            """
            SELECT count(*)
            FROM metric_value v
            JOIN metric_definition m ON m.id = v.metric_id
            WHERE m.source_id = 'osm-test'
              AND (
                  (m.key IN ('walkability', 'land_use_diversity') AND v.quality_flag = 'estimated')
                  OR (m.key NOT IN ('walkability', 'land_use_diversity') AND v.quality_flag = 'ok')
              )
            """));

        await transaction.RollbackAsync();
    }

    private static async Task<long> ScalarLongAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task CreateStagingTablesAsync()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE osm_urban_poi (
                osm_type char(1) NOT NULL,
                osm_id bigint NOT NULL,
                name text,
                tags jsonb NOT NULL,
                geom geometry(Geometry, 4326) NOT NULL
            );

            CREATE TABLE osm_urban_area (
                osm_type char(1) NOT NULL,
                osm_id bigint NOT NULL,
                name text,
                tags jsonb NOT NULL,
                geom geometry(Geometry, 4326) NOT NULL
            );

            CREATE TABLE osm_urban_highway (
                way_id bigint PRIMARY KEY,
                highway text NOT NULL,
                geom geometry(LineString, 4326) NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedAsync()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        var provinceId = await InsertAdminAreaAsync(
            connection,
            transaction,
            "province",
            "Test",
            null,
            "POLYGON((-0.05 -0.05, 0.05 -0.05, 0.05 0.05, -0.05 0.05, -0.05 -0.05))");
        var districtId = await InsertAdminAreaAsync(
            connection,
            transaction,
            "district",
            "Test İlçe",
            provinceId,
            "POLYGON((-0.02 -0.02, 0.02 -0.02, 0.02 0.02, -0.02 0.02, -0.02 -0.02))");
        await InsertAdminAreaAsync(
            connection,
            transaction,
            "neighborhood",
            "Test Mahallesi",
            districtId,
            "POLYGON((-0.005 -0.005, 0.005 -0.005, 0.005 0.005, -0.005 0.005, -0.005 -0.005))");

        await InsertPoiAsync(connection, transaction, "N", 1, "Cafe A", """{"amenity":"cafe"}""", 0, 0);
        await InsertPoiAsync(connection, transaction, "N", 2, "Cafe A", """{"amenity":"cafe"}""", 0.0001, 0);
        await InsertPoiAsync(
            connection,
            transaction,
            "N",
            3,
            "Resto",
            """{"amenity":"restaurant"}""",
            0.001,
            0);
        await InsertPoiAsync(connection, transaction, "N", 4, "Bar", """{"amenity":"bar"}""", 0.002, 0);
        await InsertPoiAsync(
            connection,
            transaction,
            "N",
            5,
            "Museum",
            """{"tourism":"museum"}""",
            0.003,
            0);
        await InsertPoiAsync(
            connection,
            transaction,
            "N",
            6,
            "School in",
            """{"amenity":"school"}""",
            -0.001,
            0);
        await InsertPoiAsync(
            connection,
            transaction,
            "N",
            7,
            "School out",
            """{"amenity":"school"}""",
            0.009,
            0);
        await InsertPoiAsync(
            connection,
            transaction,
            "N",
            8,
            "Hospital out",
            """{"amenity":"hospital"}""",
            0,
            -0.01);
        await InsertPoiAsync(
            connection,
            transaction,
            "N",
            9,
            "Pharmacy out",
            """{"amenity":"pharmacy"}""",
            0,
            -0.012);

        await InsertAreaAsync(
            connection,
            transaction,
            "W",
            10,
            """{"building":"apartments"}""",
            "POLYGON((0.001 0.001, 0.002 0.001, 0.002 0.002, 0.001 0.002, 0.001 0.001))");
        await InsertAreaAsync(
            connection,
            transaction,
            "W",
            11,
            """{"building":"commercial"}""",
            "POLYGON((0.002 0.001, 0.003 0.001, 0.003 0.002, 0.002 0.002, 0.002 0.001))");
        await InsertAreaAsync(
            connection,
            transaction,
            "W",
            12,
            """{"amenity":"restaurant"}""",
            "POLYGON((0.003 0.001, 0.004 0.001, 0.004 0.002, 0.003 0.002, 0.003 0.001))");
        await InsertAreaAsync(
            connection,
            transaction,
            "W",
            13,
            """{"tourism":"museum"}""",
            "POLYGON((0.001 -0.002, 0.002 -0.002, 0.002 -0.001, 0.001 -0.001, 0.001 -0.002))");
        await InsertAreaAsync(
            connection,
            transaction,
            "W",
            14,
            """{"office":"yes"}""",
            "POLYGON((0.002 -0.002, 0.003 -0.002, 0.003 -0.001, 0.002 -0.001, 0.002 -0.002))");
        await InsertAreaAsync(
            connection,
            transaction,
            "W",
            15,
            """{"leisure":"park"}""",
            "POLYGON((0.003 -0.002, 0.004 -0.002, 0.004 -0.001, 0.003 -0.001, 0.003 -0.002))");
        await InsertAreaAsync(
            connection,
            transaction,
            "W",
            16,
            """{"amenity":"restaurant","building":"commercial"}""",
            "POLYGON((0.004 -0.002, 0.005 -0.002, 0.005 -0.001, 0.004 -0.001, 0.004 -0.002))");

        await InsertHighwayAsync(connection, transaction, 20, "(-0.002 0, 0 0)");
        await InsertHighwayAsync(connection, transaction, 21, "(0.002 0, 0 0)");
        await InsertHighwayAsync(connection, transaction, 22, "(0 -0.002, 0 0)");

        await transaction.CommitAsync();
    }

    private static async Task<long> InsertAdminAreaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string level,
        string name,
        long? parentId,
        string polygon)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO admin_area
                (parent_id, level, country_code, name, name_normalized, geometry, centroid,
                 source_id, source_feature_id)
            VALUES
                (@parent_id, @level, 'TR', @name, @name_normalized,
                 ST_Multi(ST_GeomFromText(@polygon, 4326)),
                 ST_Centroid(ST_GeomFromText(@polygon, 4326)),
                 'test', @source_feature_id)
            RETURNING id
            """;
        command.Parameters.AddWithValue("parent_id", (object?)parentId ?? DBNull.Value);
        command.Parameters.AddWithValue("level", level);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("name_normalized", name.ToLowerInvariant());
        command.Parameters.AddWithValue("polygon", polygon);
        command.Parameters.AddWithValue("source_feature_id", $"{level}-{name}");
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task InsertPoiAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string osmType,
        long osmId,
        string name,
        string tags,
        double longitude,
        double latitude)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO osm_urban_poi (osm_type, osm_id, name, tags, geom)
            VALUES (@osm_type, @osm_id, @name, @tags::jsonb,
                    ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326))
            """;
        command.Parameters.AddWithValue("osm_type", osmType);
        command.Parameters.AddWithValue("osm_id", osmId);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("tags", tags);
        command.Parameters.AddWithValue("longitude", longitude);
        command.Parameters.AddWithValue("latitude", latitude);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertAreaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string osmType,
        long osmId,
        string tags,
        string polygon)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO osm_urban_area (osm_type, osm_id, tags, geom)
            VALUES (@osm_type, @osm_id, @tags::jsonb, ST_GeomFromText(@polygon, 4326))
            """;
        command.Parameters.AddWithValue("osm_type", osmType);
        command.Parameters.AddWithValue("osm_id", osmId);
        command.Parameters.AddWithValue("tags", tags);
        command.Parameters.AddWithValue("polygon", polygon);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertHighwayAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long wayId,
        string lineString)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO osm_urban_highway (way_id, highway, geom)
            VALUES (@way_id, 'residential', ST_GeomFromText(@geometry, 4326))
            """;
        command.Parameters.AddWithValue("way_id", wayId);
        command.Parameters.AddWithValue("geometry", $"LINESTRING{lineString}");
        await command.ExecuteNonQueryAsync();
    }
}
