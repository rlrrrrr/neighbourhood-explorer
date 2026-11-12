using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Transport;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class TransportNeighborhoodCounterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgis/postgis:16-3.4")
        .Build();

    private NpgsqlDataSource dataSource = null!;
    private long neighborhoodAId;
    private long neighborhoodBId;

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
    public async Task Counts_points_by_mode_and_assigns_coastal_points_to_the_nearest_mahalle()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        var result = await TransportNeighborhoodCounter.CountAsync(
            connection,
            transaction,
            TestPoints(),
            CancellationToken.None);

        Assert.Equal(2, result.Neighborhoods.Count);
        var first = result.Neighborhoods.Single(item => item.AdminAreaId == neighborhoodAId);
        Assert.Equal(1, first.BusStops);
        Assert.Equal(1, first.RailStations);
        Assert.Equal(1, first.FerryPiers);
        Assert.True(first.AreaKm2 > 0);

        var second = result.Neighborhoods.Single(item => item.AdminAreaId == neighborhoodBId);
        Assert.Equal(1, second.BusStops);
        Assert.Equal(0, second.RailStations);
        Assert.Equal(0, second.FerryPiers);

        Assert.Equal(3, result.Assignments.Polygon);
        Assert.Equal(1, result.Assignments.Nearest);
        Assert.Equal(1, result.Assignments.Unassigned);
        var unassigned = Assert.Single(result.Assignments.UnassignedPoints);
        Assert.Equal("LOIN", unassigned.Name);
    }

    [Fact]
    public async Task Writes_the_catalog_translations_and_ok_values()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        var counted = await TransportNeighborhoodCounter.CountAsync(
            connection,
            transaction,
            TestPoints().Where(point => point.Name != "LOIN").ToArray(),
            CancellationToken.None);
        var metrics = TransportMetricCalculator.Calculate(counted.Neighborhoods);

        var result = await MetricSeriesWriter.WriteAsync(
            new MetricSeriesWriteRequest(
                connection,
                transaction,
                new MetricCatalogSource(
                    "ibb-transport-test",
                    "Transports de test",
                    "İBB",
                    "https://example.invalid/transport",
                    "İBB Open Data License",
                    "Contains public sector information licensed under CC BY 4.0.",
                    TransportMetricCatalog.SourceMethodology,
                    new DateOnly(2026, 11, 9),
                    "test-version"),
                "2026-11-09",
                new DateOnly(2026, 11, 9),
                DateTimeOffset.UtcNow,
                metrics.Series),
            CancellationToken.None);

        Assert.True(result.MetricIds["transport_accessibility"] > 0);
        Assert.Equal("points pondérés/km²", await ScalarStringAsync(
            connection,
            transaction,
            "SELECT unit FROM metric_definition WHERE source_id = 'ibb-transport-test'"));
        Assert.Equal(2, await ScalarLongAsync(
            connection,
            transaction,
            """
            SELECT count(*)
            FROM metric_translation t
            JOIN metric_definition m ON m.id = t.metric_id
            WHERE m.source_id = 'ibb-transport-test'
            """));
        Assert.Equal(2, await ScalarLongAsync(
            connection,
            transaction,
            """
            SELECT count(*)
            FROM metric_value v
            JOIN metric_definition m ON m.id = v.metric_id
            WHERE m.source_id = 'ibb-transport-test' AND v.quality_flag = 'ok'
            """));
        Assert.Equal(1, await ScalarLongAsync(
            connection,
            transaction,
            """
            SELECT count(*)
            FROM metric_value v
            JOIN metric_definition m ON m.id = v.metric_id
            WHERE m.source_id = 'ibb-transport-test'
              AND v.admin_area_id = @admin_area_id
              AND v.normalized_value = 0
            """,
            ("admin_area_id", neighborhoodBId)));

        await transaction.RollbackAsync();
    }

    private static IReadOnlyList<TransportPoint> TestPoints() =>
    [
        new(TransportMode.Bus, "BUS A", 0.002, 0.002),
        new(TransportMode.Rail, "RAIL A", 0.003, 0.003),
        new(TransportMode.Ferry, "FERRY A", 0.005, -0.002),
        new(TransportMode.Bus, "BUS B", 0.022, 0.002),
        new(TransportMode.Bus, "LOIN", 0.2, 0.2),
    ];

    private async Task SeedAsync()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();
        var provinceId = await InsertAdminAreaAsync(
            connection,
            transaction,
            "province",
            "Test İli",
            null,
            "POLYGON((0 0, 0.05 0, 0.05 0.05, 0 0.05, 0 0))");
        var districtId = await InsertAdminAreaAsync(
            connection,
            transaction,
            "district",
            "Test İlçesi",
            provinceId,
            "POLYGON((0 0, 0.05 0, 0.05 0.05, 0 0.05, 0 0))");
        neighborhoodAId = await InsertAdminAreaAsync(
            connection,
            transaction,
            "neighborhood",
            "A Mahallesi",
            districtId,
            "POLYGON((0 0, 0.01 0, 0.01 0.01, 0 0.01, 0 0))");
        neighborhoodBId = await InsertAdminAreaAsync(
            connection,
            transaction,
            "neighborhood",
            "B Mahallesi",
            districtId,
            "POLYGON((0.02 0, 0.03 0, 0.03 0.01, 0.02 0.01, 0.02 0))");
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
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

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
