using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Transport;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class TransportProvinceFilterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgis/postgis:16-3.4")
        .Build();

    private NpgsqlDataSource dataSource = null!;

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
    public async Task Keeps_points_inside_the_province_and_excludes_the_others()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        TransportPoint[] points =
        [
            new(TransportMode.Bus, "DEDANS", 0.002, 0.002),
            new(TransportMode.Bus, "KOCOAELI", 0.200, 0.200),
            new(TransportMode.Rail, "DEDANS AUSSI", 0.010, 0.010),
        ];

        var result = await TransportProvinceFilter.ApplyAsync(
            connection,
            transaction,
            points,
            CancellationToken.None);

        Assert.Equal(["DEDANS", "DEDANS AUSSI"], result.Points.Select(point => point.Name));
        var excluded = Assert.Single(result.Excluded);
        Assert.Equal("KOCOAELI", excluded.Name);
        Assert.Equal(TransportMode.Bus, excluded.Mode);
    }

    [Fact]
    public async Task Returns_an_empty_result_without_querying_the_database()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        var result = await TransportProvinceFilter.ApplyAsync(
            connection,
            transaction,
            [],
            CancellationToken.None);

        Assert.Empty(result.Points);
        Assert.Empty(result.Excluded);
    }

    private async Task SeedAsync()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();
        await InsertAdminAreaAsync(
            connection,
            transaction,
            "province",
            "Test İli",
            null,
            "POLYGON((0 0, 0.05 0, 0.05 0.05, 0 0.05, 0 0))");
        await transaction.CommitAsync();
    }

    private static async Task InsertAdminAreaAsync(
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
            """;
        command.Parameters.AddWithValue("parent_id", (object?)parentId ?? DBNull.Value);
        command.Parameters.AddWithValue("level", level);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("name_normalized", name.ToLowerInvariant());
        command.Parameters.AddWithValue("polygon", polygon);
        command.Parameters.AddWithValue("source_feature_id", $"{level}-{name}");
        await command.ExecuteNonQueryAsync();
    }
}
