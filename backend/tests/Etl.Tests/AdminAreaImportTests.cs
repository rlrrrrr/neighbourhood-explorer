using System.Globalization;
using System.Text.Json;
using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Osm;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class AdminAreaImportTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgis/postgis:16-3.4")
        .Build();

    private NpgsqlDataSource dataSource = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        dataSource = NpgsqlDataSource.Create(container.GetConnectionString());
        await EtlSchema.EnsureAsync(dataSource, CancellationToken.None);
        await CreateStagingTableAsync();
        await LoadFixtureAsync();
    }

    public async Task DisposeAsync()
    {
        await dataSource.DisposeAsync();
        await container.DisposeAsync();
    }

    [Fact]
    public async Task Builds_hierarchy_reports_coverage_and_replaces_previous_import()
    {
        var manifest = TestManifests.Osm(province: 1, district: 1, neighborhood: 3);

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync())
        {
            var build = await new AdminAreaBuilder(connection, transaction)
                .BuildAsync(new AdminAreaBuildRequest(manifest.Id, "TR-34"), CancellationToken.None);

            Assert.Equal("İstanbul", build.ProvinceName);
            Assert.Equal("TR", build.CountryCode);
            Assert.Equal(1, build.DistrictCount);
            Assert.Equal(3, build.NeighborhoodCount);
            Assert.Empty(build.UnassignedNeighborhoods);
            Assert.Empty(build.InvalidGeometries);

            var report = await new AdminAreaQualityChecker(connection, transaction)
                .BuildAsync(
                    new AdminAreaQualityRequest(
                        manifest,
                        build,
                        "fixture",
                        "sha",
                        "1.8.0",
                        DateTimeOffset.UtcNow),
                    CancellationToken.None);

            Assert.Empty(report.Failures);
            Assert.All(report.Counts, count => Assert.True(count.Matches));
            Assert.Single(report.DistrictCoverage);
            Assert.Equal("Kadıköy", report.DistrictCoverage[0].Area);
            Assert.True(report.DistrictCoverage[0].GapKm2 > 0);
            Assert.True(report.ProvinceCoverage.AreaKm2 > 0);

            var displayArea = await ScalarAsync(
                connection,
                transaction,
                "SELECT ST_Area(display_geometry::geography) FROM admin_area WHERE id = @id",
                build.ProvinceId);
            var officialArea = await ScalarAsync(
                connection,
                transaction,
                "SELECT ST_Area(geometry::geography) FROM admin_area WHERE id = @id",
                build.ProvinceId);
            Assert.True(displayArea > 0);
            Assert.True(displayArea < officialArea);

            var neighborhoodsWithDisplayGeometry = await ScalarLongAsync(
                connection,
                transaction,
                """
                SELECT count(*)
                FROM admin_area
                WHERE level = 'neighborhood'
                  AND source_id = @source_id
                  AND display_geometry IS NOT NULL
                  AND ST_IsValid(display_geometry)
                """,
                manifest.Id);
            Assert.Equal(3, neighborhoodsWithDisplayGeometry);

            await transaction.CommitAsync();
        }

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO worldpop_neighborhood_sum (
                    source_id, grid_year, admin_area_id, population_raw, area_km2, computed_at)
                SELECT 'worldpop-test', 2025, id, 1, 1, now()
                FROM admin_area
                WHERE level = 'neighborhood' AND source_id = @source_id
                LIMIT 1
                """,
                manifest.Id);

            var secondBuild = await new AdminAreaBuilder(connection, transaction)
                .BuildAsync(new AdminAreaBuildRequest(manifest.Id, "TR-34"), CancellationToken.None);

            Assert.Equal(3, secondBuild.NeighborhoodCount);

            var dependentRows = await ScalarLongAsync(
                connection,
                transaction,
                "SELECT count(*) FROM worldpop_neighborhood_sum WHERE source_id = @source_id",
                manifest.Id);
            Assert.Equal(0, dependentRows);

            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = transaction;
            countCommand.CommandText = "SELECT count(*) FROM admin_area WHERE source_id = @source_id";
            countCommand.Parameters.AddWithValue("source_id", manifest.Id);
            var rows = (long)(await countCommand.ExecuteScalarAsync(CancellationToken.None))!;

            Assert.Equal(5, rows);
            await transaction.RollbackAsync();
        }
    }

    private static async Task<double> ScalarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        long id)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("id", id);
        return (double)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }

    private static async Task<long> ScalarLongAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        string sourceId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("source_id", sourceId);
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        string sourceId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("source_id", sourceId);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private async Task CreateStagingTableAsync()
    {
        await using var command = dataSource.CreateCommand("""
            CREATE TABLE osm_admin_boundaries (
                relation_id bigint PRIMARY KEY,
                admin_level int NOT NULL,
                tags jsonb NOT NULL,
                geom geometry(Geometry, 4326) NOT NULL
            );
            """);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private async Task LoadFixtureAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "istanbul-sample.geojson");
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);

        foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            var properties = feature.GetProperty("properties");
            var relationId = properties.GetProperty("relation_id").GetInt64();
            var adminLevel = properties.GetProperty("admin_level").GetInt32();
            var name = properties.GetProperty("name").GetString()
                ?? throw new InvalidOperationException("Fixture sans nom de relation.");
            var isoCode = properties.TryGetProperty("iso3166_2", out var iso) ? iso.GetString() : null;

            var tags = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["admin_level"] = adminLevel.ToString(CultureInfo.InvariantCulture),
                ["boundary"] = "administrative",
            };
            if (isoCode is not null)
            {
                tags["ISO3166-2"] = isoCode;
            }

            await using var command = dataSource.CreateCommand("""
                INSERT INTO osm_admin_boundaries (relation_id, admin_level, tags, geom)
                VALUES (
                    @relation_id,
                    @admin_level,
                    @tags::jsonb,
                    ST_Multi(ST_SetSRID(ST_GeomFromGeoJSON(@geometry), 4326)))
                """);
            command.Parameters.AddWithValue("relation_id", relationId);
            command.Parameters.AddWithValue("admin_level", adminLevel);
            command.Parameters.AddWithValue("tags", JsonSerializer.Serialize(tags));
            command.Parameters.AddWithValue("geometry", feature.GetProperty("geometry").GetRawText());
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
