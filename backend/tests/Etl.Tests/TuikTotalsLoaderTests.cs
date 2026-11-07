using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Tuik;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class TuikTotalsLoaderTests : IAsyncLifetime
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
    public async Task Replace_writes_the_totals_and_replaces_the_previous_load()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        var totals = new[]
        {
            new TuikTotal("province", "TR100", "İstanbul", 15754053),
            new TuikTotal("district", "1103", "Adalar", 17489),
            new TuikTotal("district", "1183", "Beşiktaş", 165895),
        };

        await TuikTotalsLoader.ReplaceAsync(
            connection,
            transaction,
            "tuik-test",
            2025,
            DateTimeOffset.UtcNow,
            totals,
            CancellationToken.None);

        Assert.Equal(3, await CountAsync(connection, transaction, "tuik-test"));
        Assert.Equal(
            15754053,
            await ScalarAsync<int>(
                connection,
                transaction,
                "SELECT population FROM tuik_adnks_totals " +
                "WHERE source_id = 'tuik-test' AND code = 'TR100'"));
        Assert.Equal(
            "besiktas",
            await ScalarAsync<string>(
                connection,
                transaction,
                "SELECT name_normalized FROM tuik_adnks_totals " +
                "WHERE source_id = 'tuik-test' AND code = '1183'"));

        await TuikTotalsLoader.ReplaceAsync(
            connection,
            transaction,
            "tuik-test",
            2025,
            DateTimeOffset.UtcNow,
            [totals[0]],
            CancellationToken.None);

        Assert.Equal(1, await CountAsync(connection, transaction, "tuik-test"));
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task Replace_rejects_a_non_positive_population()
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => TuikTotalsLoader.ReplaceAsync(
            connection,
            transaction,
            "tuik-test",
            2025,
            DateTimeOffset.UtcNow,
            [new TuikTotal("district", "1103", "Adalar", 0)],
            CancellationToken.None));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    private static async Task<long> CountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourceId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT count(*) FROM tuik_adnks_totals WHERE source_id = @source_id";
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
}
