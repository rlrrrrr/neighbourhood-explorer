using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.WorldPop;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class WorldPopTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgis/postgis:16-3.4")
        .Build();

    private NpgsqlDataSource dataSource = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        dataSource = NpgsqlDataSource.Create(container.GetConnectionString());
        await EtlSchema.EnsureAsync(dataSource, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await dataSource.DisposeAsync();
        await container.DisposeAsync();
    }

    [Fact]
    public async Task ZonalSummer_WeightsBoundaryPixelsByCoveredFraction()
    {
        await ResetAsync();
        await CreateRasterAsync();
        var (_, districtId) = await CreateProvinceAndDistrictAsync();

        await InsertNeighborhoodAsync(
            districtId,
            "Demi-colonne",
            "POLYGON((665000 4563000, 665050 4563000, 665050 4562800, " +
            "665000 4562800, 665000 4563000))");
        await InsertNeighborhoodAsync(
            districtId,
            "Colonne entière",
            "POLYGON((665000 4563000, 665100 4563000, 665100 4562800, " +
            "665000 4562800, 665000 4563000))");

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var result = await WorldPopZonalSummer.ComputeAsync(
            new WorldPopZonalSumRequest(
                connection,
                transaction,
                "worldpop-test",
                2025,
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(2, result.NeighborhoodCount);
        Assert.Equal(0, result.ZeroCount);
        Assert.Equal(6.0, result.TotalRaw, 3);

        var raw = await ReadNeighborhoodRawAsync(connection, transaction, "Demi-colonne");
        var full = await ReadNeighborhoodRawAsync(connection, transaction, "Colonne entière");
        Assert.Equal(2.0, raw, 2);
        Assert.Equal(4.0, full, 2);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task PercentileNormalization_matches_postgres_percent_rank()
    {
        var values = new[] { 10.0, 10.0, 20.0, 40.0, 80.0 };
        var sortedValues = values.OrderBy(value => value).ToArray();

        await using var command = dataSource.CreateCommand("""
            SELECT value, PERCENT_RANK() OVER (ORDER BY value)
            FROM unnest(@values) AS t(value)
            ORDER BY value
            """);
        command.Parameters.AddWithValue("values", sortedValues);

        var postgres = new List<double>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            postgres.Add(reader.GetDouble(1));
        }

        Assert.Equal(postgres, PercentileRank.Normalize(sortedValues));
    }

    [Fact]
    public async Task Recalibrator_SetsCoefficientAndMetricValues()
    {
        await ResetAsync();
        var (_, districtId, _, _) = await CreateRecalibrationFixtureAsync(
            rawA: 30,
            rawB: 70,
            districtPopulation: 100);

        var result = await RecalculateAsync(districtId);

        Assert.Empty(result.Failures);
        Assert.Equal(1, result.DistrictCount);
        Assert.Equal(2, result.NeighborhoodCount);
        Assert.Equal(100, result.TotalAdnks);
        Assert.Equal(100, result.TotalEstimated, 6);
        Assert.Equal(0, result.FlaggedCount);
        Assert.Single(result.Coefficients);
        Assert.Equal(1.0, result.Coefficients[0].Coefficient, 9);
        Assert.Equal(30, result.MinDensity, 6);
        Assert.Equal(70, result.MaxDensity, 6);

        var byName = result.Neighborhoods.ToDictionary(item => item.NeighborhoodName);
        Assert.Equal(30, byName["Mahalle A"].Density, 6);
        Assert.Equal(70, byName["Mahalle B"].Density, 6);
        Assert.Equal(0, byName["Mahalle A"].Normalized, 6);
        Assert.Equal(1, byName["Mahalle B"].Normalized, 6);
        Assert.All(result.Neighborhoods, item => Assert.Equal("estimated", item.QualityFlag));

        Assert.Equal(2, await CountAsync("metric_value"));
        Assert.Equal(1, await CountAsync("metric_definition"));
        Assert.Equal(2, await CountAsync("metric_translation"));
        Assert.Equal(1, await CountAsync("data_source"));
    }

    [Fact]
    public async Task Recalibrator_FlagsCoefficientOutsideRange()
    {
        await ResetAsync();
        var (_, districtId, _, _) = await CreateRecalibrationFixtureAsync(
            rawA: 20,
            rawB: 30,
            districtPopulation: 100);

        var result = await RecalculateAsync(districtId);

        Assert.Empty(result.Failures);
        Assert.Equal(1, result.FlaggedCount);
        Assert.Equal(2.0, result.Coefficients[0].Coefficient, 9);
        Assert.Equal(40, result.Neighborhoods.Single(item => item.NeighborhoodName == "Mahalle A").Density, 6);
        Assert.Equal(60, result.Neighborhoods.Single(item => item.NeighborhoodName == "Mahalle B").Density, 6);
    }

    [Fact]
    public async Task Recalibrator_StopsWhenDistrictWorldPopSumIsZero()
    {
        await ResetAsync();
        var (_, districtId, _, _) = await CreateRecalibrationFixtureAsync(
            rawA: 0,
            rawB: 0,
            districtPopulation: 100);

        var result = await RecalculateAsync(districtId);

        Assert.NotEmpty(result.Failures);
        Assert.Equal(0, result.MetricId);
        Assert.Empty(result.Neighborhoods);
    }

    private async Task<WorldPopRecalibrationResult> RecalculateAsync(long districtId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var metadata = new WorldPopMetricMetadata(
            new MetricCatalogSource(
                "worldpop-test",
                "WorldPop test",
                "WorldPop",
                "https://example.org/worldpop",
                "CC BY 4.0",
                "WorldPop test, valeurs modifiées",
                "Somme des cases puis recalage ADNKS.",
                new DateOnly(2026, 11, 4),
                "test-v1"),
            new MetricDefinition(
                "population_density",
                "People",
                "people/km²",
                MetricPolarity.Neutral,
                0,
                MetricQualityFlag.Estimated,
                "Somme des cases puis recalage ADNKS.",
                new MetricTranslation("fr", "Densité", "Description", "la densité"),
                new MetricTranslation("en", "Density", "Description", "density")),
            "2025",
            new DateOnly(2025, 12, 31));

        var result = await WorldPopRecalibrator.RecalculateAsync(
            new WorldPopRecalibrationRequest(
                connection,
                transaction,
                "worldpop-test",
                2025,
                2025,
                new WorldPopExpectedCounts(2, 1, 100),
                DateTimeOffset.UtcNow,
                metadata),
            CancellationToken.None);

        if (result.Failures.Count == 0)
        {
            await transaction.CommitAsync();
        }
        else
        {
            await transaction.RollbackAsync();
        }

        Assert.True(districtId > 0);
        return result;
    }

    private async Task<(long ProvinceId, long DistrictId, long NeighborhoodA, long NeighborhoodB)>
        CreateRecalibrationFixtureAsync(int rawA, int rawB, int districtPopulation)
    {
        var (provinceId, districtId) = await CreateProvinceAndDistrictAsync();
        var neighborhoodA = await InsertNeighborhoodAsync(
            districtId,
            "Mahalle A",
            "POLYGON((665000 4563000, 665050 4563000, 665050 4562800, " +
            "665000 4562800, 665000 4563000))");
        var neighborhoodB = await InsertNeighborhoodAsync(
            districtId,
            "Mahalle B",
            "POLYGON((665050 4563000, 665100 4563000, 665100 4562800, " +
            "665050 4562800, 665050 4563000))");

        await ExecuteAsync($"""
            INSERT INTO tuik_adnks_totals
                (source_id, reference_year, level, code, name, name_normalized, population, retrieved_at)
            VALUES
                ('tuik-adnks', 2025, 'province', 'TR100', 'İstanbul', 'istanbul',
                 {districtPopulation}, now()),
                ('tuik-adnks', 2025, 'district', '2048', 'Arnavutköy', 'arnavutkoy',
                 {districtPopulation}, now());

            INSERT INTO worldpop_neighborhood_sum
                (source_id, grid_year, admin_area_id, population_raw, area_km2, computed_at)
            VALUES
                ('worldpop-test', 2025, {neighborhoodA}, {rawA}, 1.0, now()),
                ('worldpop-test', 2025, {neighborhoodB}, {rawB}, 1.0, now());
            """);

        return (provinceId, districtId, neighborhoodA, neighborhoodB);
    }

    private async Task CreateRasterAsync()
    {
        await ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS worldpop_raster (
                rid serial PRIMARY KEY,
                rast raster
            );

            INSERT INTO worldpop_raster (rast)
            SELECT ST_SetValue(
                       ST_SetValue(
                           ST_SetValue(
                               ST_SetValue(
                                   ST_AddBand(
                                       ST_MakeEmptyRaster(2, 2, 665000, 4563000, 100, -100, 0, 0, 32635),
                                       '32BF'::text, 0, -99999),
                                   1, 1, 1, 1),
                               1, 2, 1, 2),
                           1, 1, 2, 3),
                       1, 2, 2, 4);
            """);
    }

    private async Task<(long ProvinceId, long DistrictId)> CreateProvinceAndDistrictAsync()
    {
        var provinceId = await InsertAdminAreaAsync(
            null,
            "province",
            "İstanbul",
            "istanbul",
            "POLYGON((664000 4562000, 666000 4562000, 666000 4564000, " +
            "664000 4564000, 664000 4562000))");
        var districtId = await InsertAdminAreaAsync(
            provinceId,
            "district",
            "Arnavutköy",
            "arnavutkoy",
            "POLYGON((664000 4562000, 666000 4562000, 666000 4564000, " +
            "664000 4564000, 664000 4562000))");
        return (provinceId, districtId);
    }

    private async Task<long> InsertNeighborhoodAsync(long districtId, string name, string wkt)
    {
        return await InsertAdminAreaAsync(
            districtId,
            "neighborhood",
            name,
            name.ToLowerInvariant(),
            wkt);
    }

    private async Task<long> InsertAdminAreaAsync(
        long? parentId,
        string level,
        string name,
        string normalized,
        string wkt)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO admin_area
                (parent_id, level, country_code, name, name_normalized, geometry, centroid,
                 source_id, source_feature_id)
            VALUES
                (@parent_id, @level, 'TR', @name, @normalized,
                 ST_Multi(ST_Transform(ST_GeomFromText(@wkt, 32635), 4326)),
                 ST_Transform(ST_Centroid(ST_GeomFromText(@wkt, 32635)), 4326),
                 'test', @feature_id)
            RETURNING id
            """);
        command.Parameters.AddWithValue("parent_id", (object?)parentId ?? DBNull.Value);
        command.Parameters.AddWithValue("level", level);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("normalized", normalized);
        command.Parameters.AddWithValue("wkt", wkt);
        command.Parameters.AddWithValue("feature_id", $"{level}-{name}-{Guid.NewGuid():N}");
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<double> ReadNeighborhoodRawAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string name)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT s.population_raw
            FROM worldpop_neighborhood_sum s
            JOIN admin_area n ON n.id = s.admin_area_id
            WHERE s.source_id = 'worldpop-test' AND s.grid_year = 2025 AND n.name = @name
            """;
        command.Parameters.AddWithValue("name", name);
        return (double)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> CountAsync(string table)
    {
        await using var command = dataSource.CreateCommand($"SELECT count(*) FROM {table}");
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task ResetAsync()
    {
        await ExecuteAsync("""
            DROP TABLE IF EXISTS worldpop_raster;
            TRUNCATE metric_value, metric_translation, metric_definition, data_source,
                     worldpop_district_coefficient, worldpop_neighborhood_sum,
                     tuik_adnks_totals, admin_area RESTART IDENTITY CASCADE;
            """);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }
}
