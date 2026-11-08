using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NeighbourhoodExplorer.Etl.Database;
using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;

namespace NeighbourhoodExplorer.Api.Tests;

public sealed class MetricsApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgis/postgis:16-3.4").Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public string ModaNeighborhoodId { get; private set; } = null!;

    public string FenerbahceNeighborhoodId { get; private set; } = null!;

    public string KizilayNeighborhoodId { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        DataSource = NpgsqlDataSource.Create(container.GetConnectionString());
        await EtlSchema.EnsureAsync(DataSource, CancellationToken.None);
        await SeedAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("DATABASE_CONNECTION_STRING", container.GetConnectionString())
            .UseSetting("AI_ENABLED", "false"));
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await DataSource.DisposeAsync();
        await container.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        var istanbulId = await InsertAdminAreaAsync(
            level: "province",
            parentId: null,
            name: "İstanbul",
            nameNormalized: "istanbul",
            officialCode: "TR-34",
            sourceFeatureId: "province-34",
            geometryWkt: "POLYGON((28 41, 29 41, 29 42, 28 42, 28 41))",
            centroidWkt: "POINT(28.5 41.5)");

        var ankaraId = await InsertAdminAreaAsync(
            level: "province",
            parentId: null,
            name: "Ankara",
            nameNormalized: "ankara",
            officialCode: "TR-06",
            sourceFeatureId: "province-06",
            geometryWkt: "POLYGON((32 39, 33 39, 33 40, 32 40, 32 39))",
            centroidWkt: "POINT(32.5 39.5)");

        var kadikoyId = await InsertAdminAreaAsync(
            level: "district",
            parentId: istanbulId,
            name: "Kadıköy",
            nameNormalized: "kadikoy",
            officialCode: null,
            sourceFeatureId: "district-kadikoy",
            geometryWkt: "POLYGON((28.9 40.9, 29 40.9, 29 41, 28.9 41, 28.9 40.9))",
            centroidWkt: "POINT(28.95 40.95)");

        var cankayaId = await InsertAdminAreaAsync(
            level: "district",
            parentId: ankaraId,
            name: "Çankaya",
            nameNormalized: "cankaya",
            officialCode: null,
            sourceFeatureId: "district-cankaya",
            geometryWkt: "POLYGON((32.8 39.8, 32.9 39.8, 32.9 39.9, 32.8 39.9, 32.8 39.8))",
            centroidWkt: "POINT(32.85 39.85)");

        var modaId = await InsertAdminAreaAsync(
            level: "neighborhood",
            parentId: kadikoyId,
            name: "Moda Mahallesi",
            nameNormalized: "moda mahallesi",
            officialCode: null,
            sourceFeatureId: "neighborhood-moda",
            geometryWkt: "POLYGON((28.94 40.94, 28.96 40.94, 28.96 40.96, 28.94 40.96, 28.94 40.94))",
            centroidWkt: "POINT(28.95 40.95)");

        var fenerbahceId = await InsertAdminAreaAsync(
            level: "neighborhood",
            parentId: kadikoyId,
            name: "Fenerbahçe Mahallesi",
            nameNormalized: "fenerbahce mahallesi",
            officialCode: null,
            sourceFeatureId: "neighborhood-fenerbahce",
            geometryWkt: "POLYGON((28.96 40.96, 28.98 40.96, 28.98 40.98, 28.96 40.98, 28.96 40.96))",
            centroidWkt: "POINT(28.97 40.97)");

        var kizilayId = await InsertAdminAreaAsync(
            level: "neighborhood",
            parentId: cankayaId,
            name: "Kızılay Mahallesi",
            nameNormalized: "kizilay mahallesi",
            officialCode: null,
            sourceFeatureId: "neighborhood-kizilay",
            geometryWkt: "POLYGON((32.84 39.84, 32.86 39.84, 32.86 39.86, 32.84 39.86, 32.84 39.84))",
            centroidWkt: "POINT(32.85 39.85)");

        ModaNeighborhoodId = modaId.ToString(CultureInfo.InvariantCulture);
        FenerbahceNeighborhoodId = fenerbahceId.ToString(CultureInfo.InvariantCulture);
        KizilayNeighborhoodId = kizilayId.ToString(CultureInfo.InvariantCulture);

        await ExecuteAsync("""
            INSERT INTO data_source (
                id, name, organization, url, license, attribution_text, methodology, retrieved_at, source_version)
            VALUES (
                'worldpop-test', 'WorldPop test', 'WorldPop', 'https://www.worldpop.org', 'CC-BY-4.0',
                'WorldPop (test)', 'Méthode de test', DATE '2026-01-15', '2025')
            """);

        var metricId = await InsertMetricDefinitionAsync();
        await InsertTranslationsAsync(metricId);
        await InsertMetricValuesAsync(metricId, modaId, fenerbahceId, kizilayId);
    }

    private async Task<long> InsertAdminAreaAsync(
        string level,
        long? parentId,
        string name,
        string nameNormalized,
        string? officialCode,
        string sourceFeatureId,
        string geometryWkt,
        string centroidWkt)
    {
        const string sql = """
            INSERT INTO admin_area (
                parent_id, level, country_code, name, name_normalized, official_code,
                geometry, centroid, source_id, source_feature_id)
            VALUES (
                @parent_id, @level, 'TR', @name, @name_normalized, @official_code,
                ST_Multi(ST_GeomFromText(@geometry, 4326)),
                ST_GeomFromText(@centroid, 4326),
                'osm-test', @source_feature_id)
            RETURNING id
            """;

        await using var command = DataSource.CreateCommand(sql);
        command.Parameters.Add(new NpgsqlParameter("parent_id", NpgsqlDbType.Bigint)
        {
            Value = (object?)parentId ?? DBNull.Value,
        });
        command.Parameters.AddWithValue("level", level);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("name_normalized", nameNormalized);
        command.Parameters.Add(new NpgsqlParameter("official_code", NpgsqlDbType.Text)
        {
            Value = (object?)officialCode ?? DBNull.Value,
        });
        command.Parameters.AddWithValue("geometry", geometryWkt);
        command.Parameters.AddWithValue("centroid", centroidWkt);
        command.Parameters.AddWithValue("source_feature_id", sourceFeatureId);

        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> InsertMetricDefinitionAsync()
    {
        const string sql = """
            INSERT INTO metric_definition (
                key, category, unit, native_level, polarity,
                normalization_method, normalization_version, display_precision, source_id)
            VALUES (
                'population_density', 'population', 'habitants/km²', 'neighborhood', 'neutral',
                'percentile', 'percentile-v1', 0, 'worldpop-test')
            RETURNING id
            """;

        return (long)(await ExecuteScalarAsync(sql))!;
    }

    private async Task InsertTranslationsAsync(long metricId)
    {
        const string sql = """
            INSERT INTO metric_translation (metric_id, locale, name, description, subject)
            VALUES
                (@metric_id, 'fr', 'Densité de population', 'Habitants par kilomètre carré.', 'population'),
                (@metric_id, 'en', 'Population density', 'Inhabitants per square kilometre.', 'population')
            """;

        await using var command = DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("metric_id", metricId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertMetricValuesAsync(
        long metricId,
        long modaId,
        long fenerbahceId,
        long kizilayId)
    {
        const string sql = """
            INSERT INTO metric_value (
                admin_area_id, metric_id, observed_at, reference_period,
                raw_value, normalized_value, quality_flag, computed_at)
            VALUES
                (@moda, @metric_id, DATE '2024-12-31', '2024', 100, 0.25, 'estimated', now()),
                (@moda, @metric_id, DATE '2025-12-31', '2025', 200, 0.5, 'estimated', now()),
                (@fenerbahce, @metric_id, DATE '2025-12-31', '2025', 300, 0.75, 'estimated', now()),
                (@kizilay, @metric_id, DATE '2025-12-31', '2025', 999, 0.99, 'estimated', now())
            """;

        await using var command = DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("moda", modaId);
        command.Parameters.AddWithValue("fenerbahce", fenerbahceId);
        command.Parameters.AddWithValue("kizilay", kizilayId);
        command.Parameters.AddWithValue("metric_id", metricId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> ExecuteScalarAsync(string sql)
    {
        await using var command = DataSource.CreateCommand(sql);
        return await command.ExecuteScalarAsync();
    }
}
