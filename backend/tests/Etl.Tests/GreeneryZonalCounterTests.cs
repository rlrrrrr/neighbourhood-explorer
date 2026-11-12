using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Greenery;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class GreeneryZonalCounterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgis/postgis:16-3.4")
        .Build();

    private NpgsqlDataSource dataSource = null!;
    private long provinceId;
    private long districtId;
    private long neighborhoodId;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        dataSource = NpgsqlDataSource.Create(container.GetConnectionString());
        await EtlSchema.EnsureAsync(dataSource, CancellationToken.None);
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await dataSource.DisposeAsync();
        await container.DisposeAsync();
    }

    [Fact]
    public async Task Counts_green_pixels_by_pixel_center_and_excludes_nodata()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        var counts = await GreeneryZonalCounter.CountAsync(
            connection,
            transaction,
            CancellationToken.None);

        var neighborhood = Assert.Single(counts);
        Assert.Equal(neighborhoodId, neighborhood.AdminAreaId);
        Assert.Equal("Test İlçesi", neighborhood.DistrictName);
        Assert.Equal("Test Mahallesi", neighborhood.NeighborhoodName);
        Assert.Equal(2, neighborhood.GreenPixels);
        Assert.Equal(3, neighborhood.ValidPixels);
        Assert.Equal(66.6667, GreeneryMetricCalculator.Share(neighborhood), 3);
    }

    [Fact]
    public async Task Reads_the_province_bounding_box()
    {
        var bounds = await WorldCoverBoundsReader.ReadAsync(dataSource, CancellationToken.None);

        Assert.Equal(0, bounds.MinLongitude, 6);
        Assert.Equal(0, bounds.MinLatitude, 6);
        Assert.Equal(0.004, bounds.MaxLongitude, 6);
        Assert.Equal(0.004, bounds.MaxLatitude, 6);
    }

    [Fact]
    public async Task Writes_the_catalog_translations_and_estimated_values()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        var counts = await GreeneryZonalCounter.CountAsync(
            connection,
            transaction,
            CancellationToken.None);
        var metrics = GreeneryMetricCalculator.Calculate(counts);

        var result = await MetricSeriesWriter.WriteAsync(
            new MetricSeriesWriteRequest(
                connection,
                transaction,
                new MetricCatalogSource(
                    "esa-test",
                    "ESA WorldCover de test",
                    "ESA WorldCover consortium",
                    "https://example.invalid/N39E027.tif",
                    "CC BY 4.0",
                    "© ESA WorldCover project 2021",
                    GreeneryMetricCatalog.SourceMethodology,
                    new DateOnly(2026, 11, 9),
                    "test-version"),
                "2021",
                new DateOnly(2021, 12, 31),
                DateTimeOffset.UtcNow,
                metrics.Series),
            CancellationToken.None);

        var metricId = result.MetricIds["greenery"];
        Assert.True(metricId > 0);
        Assert.Equal(1, await ScalarLongAsync(
            connection,
            transaction,
            "SELECT count(*) FROM data_source WHERE id = 'esa-test'"));
        Assert.Equal("greenery", await ScalarStringAsync(
            connection,
            transaction,
            "SELECT key FROM metric_definition WHERE source_id = 'esa-test'"));
        Assert.Equal(2, await ScalarLongAsync(
            connection,
            transaction,
            """
            SELECT count(*)
            FROM metric_translation t
            JOIN metric_definition m ON m.id = t.metric_id
            WHERE m.source_id = 'esa-test'
            """));

        await using var value = connection.CreateCommand();
        value.Transaction = transaction;
        value.CommandText = """
            SELECT raw_value, normalized_value, quality_flag, reference_period
            FROM metric_value
            WHERE admin_area_id = @admin_area_id AND metric_id = @metric_id
            """;
        value.Parameters.AddWithValue("admin_area_id", neighborhoodId);
        value.Parameters.AddWithValue("metric_id", metricId);
        await using var reader = await value.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(66.6667, reader.GetDouble(0), 3);
        Assert.Equal(0, reader.GetDouble(1));
        Assert.Equal("estimated", reader.GetString(2));
        Assert.Equal("2021", reader.GetString(3));
        await reader.DisposeAsync();

        await transaction.RollbackAsync();
    }

    private async Task SeedAsync()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();
        provinceId = await InsertAdminAreaAsync(
            connection,
            transaction,
            "province",
            "Test İli",
            null,
            "POLYGON((0 0, 0.004 0, 0.004 0.004, 0 0.004, 0 0))");
        districtId = await InsertAdminAreaAsync(
            connection,
            transaction,
            "district",
            "Test İlçesi",
            provinceId,
            "POLYGON((0 0, 0.004 0, 0.004 0.004, 0 0.004, 0 0))");
        neighborhoodId = await InsertAdminAreaAsync(
            connection,
            transaction,
            "neighborhood",
            "Test Mahallesi",
            districtId,
            "POLYGON((0 0, 0.0024 0, 0.0024 0.0024, 0 0.0024, 0 0))");

        await using var raster = connection.CreateCommand();
        raster.Transaction = transaction;
        raster.CommandText = """
            CREATE TABLE worldcover_raster (rid serial PRIMARY KEY, rast raster);
            INSERT INTO worldcover_raster (rast)
            SELECT ST_SetValues(
                ST_AddBand(
                    ST_MakeEmptyRaster(4, 4, 0, 0.004, 0.001, -0.001, 0, 0, 4326),
                    '8BUI'::text,
                    0,
                    0),
                1, 1, 1,
                ARRAY[
                    [10, 10, 50, 50],
                    [20, 30, 50, 50],
                    [10, 50,  0,  0],
                    [ 0, 20,  0,  0]
                ]::double precision[][]);
            """;
        await raster.ExecuteNonQueryAsync();

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

    private static async Task<string> ScalarStringAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
