using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace NeighbourhoodExplorer.Etl.Database;

public sealed record EtlRunResult(
    bool Success,
    string? InputSha256,
    IReadOnlyDictionary<string, int> RowCounts,
    string? ErrorMessage);

public sealed class EtlRunRecorder(
    NpgsqlDataSource dataSource,
    string sourceId,
    TimeProvider timeProvider)
{
    private static readonly string ToolVersion =
        typeof(EtlRunRecorder).Assembly.GetName().Version?.ToString() ?? "unknown";

    public async Task<long> StartAsync(CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        const string closeInterruptedSql = """
            UPDATE etl_run
            SET finished_at = @started_at,
                status = 'failed',
                error_message = 'exécution interrompue (processus ETL arrêté avant la fin)'
            WHERE source_id = @source_id AND status = 'running'
            """;

        await using (var closeCommand = dataSource.CreateCommand(closeInterruptedSql))
        {
            closeCommand.Parameters.AddWithValue("source_id", sourceId);
            closeCommand.Parameters.AddWithValue("started_at", startedAt);
            await closeCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string sql = """
            INSERT INTO etl_run (source_id, started_at, status, tool_version)
            VALUES (@source_id, @started_at, 'running', @tool_version)
            RETURNING id
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("started_at", startedAt);
        command.Parameters.AddWithValue("tool_version", ToolVersion);

        var id = await command.ExecuteScalarAsync(cancellationToken);
        return (long)(id ?? throw new InvalidOperationException("etl_run: identifiant non retourné."));
    }

    public async Task FinishAsync(long runId, EtlRunResult result, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE etl_run
            SET finished_at = @finished_at,
                status = @status,
                input_sha256 = @input_sha256,
                row_counts = @row_counts,
                error_message = @error_message
            WHERE id = @id
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", runId);
        command.Parameters.AddWithValue("finished_at", timeProvider.GetUtcNow());
        command.Parameters.AddWithValue("status", result.Success ? "succeeded" : "failed");
        command.Parameters.Add(new NpgsqlParameter("input_sha256", NpgsqlDbType.Text)
        {
            Value = (object?)result.InputSha256 ?? DBNull.Value,
        });
        command.Parameters.Add(new NpgsqlParameter("row_counts", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(result.RowCounts),
        });
        command.Parameters.Add(new NpgsqlParameter("error_message", NpgsqlDbType.Text)
        {
            Value = (object?)result.ErrorMessage ?? DBNull.Value,
        });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
