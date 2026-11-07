using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Ibb;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class IbbStagingLoaderTests : IAsyncLifetime
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
    public async Task Replace_writes_rows_links_the_match_and_replaces_the_previous_load()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();
        var neighborhoodId = await InsertNeighborhoodAsync(connection, transaction);

        var rows = new[]
        {
            new IbbMatchRow(Row(1, "CAFERAĞA", 10), neighborhoodId, "name"),
            new IbbMatchRow(Row(2, "INCONNU", 20), null, null),
        };

        await IbbStagingLoader.ReplaceAsync(
            connection,
            transaction,
            "ibb-test",
            rows,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Equal(2, await CountAsync(connection, transaction, "ibb-test"));
        Assert.Equal(
            neighborhoodId,
            await ScalarAsync<long>(
                connection,
                transaction,
                "SELECT admin_area_id FROM staging_ibb_buildings_2017 " +
                "WHERE source_id = 'ibb-test' AND mahalle_uavt = 10"));

        await IbbStagingLoader.ReplaceAsync(
            connection,
            transaction,
            "ibb-test",
            [rows[0]],
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Equal(1, await CountAsync(connection, transaction, "ibb-test"));
        await transaction.CommitAsync();
    }

    private static async Task<long> InsertNeighborhoodAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        const string sql = """
            WITH province AS (
                INSERT INTO admin_area (
                    level, country_code, name, name_normalized, geometry, centroid, source_id, source_feature_id)
                VALUES (
                    'province', 'TR', 'İstanbul', 'istanbul',
                    ST_Multi(ST_GeomFromText('POLYGON((28 41,29 41,29 42,28 42,28 41))', 4326)),
                    ST_SetSRID(ST_Point(28.5, 41.5), 4326), 'osm', 'p1')
                RETURNING id),
            district AS (
                INSERT INTO admin_area (
                    parent_id, level, country_code, name, name_normalized, geometry, centroid, source_id, source_feature_id)
                SELECT
                    id, 'district', 'TR', 'Kadıköy', 'kadikoy',
                    ST_Multi(ST_GeomFromText('POLYGON((28.9 40.9,29.1 40.9,29.1 41.1,28.9 41.1,28.9 40.9))', 4326)),
                    ST_SetSRID(ST_Point(29, 41), 4326), 'osm', 'd1'
                FROM province
                RETURNING id)
            INSERT INTO admin_area (
                parent_id, level, country_code, name, name_normalized, geometry, centroid, source_id, source_feature_id)
            SELECT
                id, 'neighborhood', 'TR', 'Caferağa Mahallesi', 'caferaga mahallesi',
                ST_Multi(ST_GeomFromText('POLYGON((28.98 40.98,29.02 40.98,29.02 41.02,28.98 41.02,28.98 40.98))', 4326)),
                ST_SetSRID(ST_Point(29, 41), 4326), 'osm', 'n1'
            FROM district
            RETURNING id
            """;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }

    private static async Task<long> CountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourceId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT count(*) FROM staging_ibb_buildings_2017 WHERE source_id = @source_id";
        command.Parameters.AddWithValue("source_id", sourceId);
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }

    private static IbbBuildingRow Row(int id, string neighborhood, long uavt) => new(
        Id: id,
        DistrictName: "KADIKÖY",
        NeighborhoodName: neighborhood,
        MahalleUavt: uavt,
        BuildingsBefore1980: 1,
        BuildingsBetween1980And2000: 2,
        BuildingsAfter2000: 3,
        BuildingsWith1To4Floors: 4,
        BuildingsWith5To9Floors: 1,
        BuildingsWith9To19Floors: 1,
        DistrictNormalized: IbbNameNormalizer.NormalizeDistrict("KADIKÖY"),
        NeighborhoodNormalized: IbbNameNormalizer.NormalizeNeighborhood(neighborhood));
}
