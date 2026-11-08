using NeighbourhoodExplorer.Etl.Database;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class EtlRunRecorderTests : IAsyncLifetime
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
    public async Task Start_closes_a_previous_running_run_for_the_same_source()
    {
        var recorder = new EtlRunRecorder(dataSource, "tuik-test", TimeProvider.System);

        var first = await recorder.StartAsync(CancellationToken.None);
        var second = await recorder.StartAsync(CancellationToken.None);

        Assert.Equal("failed", await StatusAsync(first));
        Assert.Equal("running", await StatusAsync(second));
    }

    private async Task<string> StatusAsync(long runId)
    {
        await using var command = dataSource.CreateCommand("SELECT status FROM etl_run WHERE id = @id");
        command.Parameters.AddWithValue("id", runId);
        return (string)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }
}
