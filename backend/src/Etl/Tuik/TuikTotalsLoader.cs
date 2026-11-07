using Npgsql;

namespace NeighbourhoodExplorer.Etl.Tuik;

public sealed record TuikTotal(string Level, string Code, string Name, int Population);

public static class TuikTotalsLoader
{
    public static async Task ReplaceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourceId,
        int referenceYear,
        DateTimeOffset retrievedAt,
        IReadOnlyList<TuikTotal> totals,
        CancellationToken cancellationToken)
    {
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText =
                "DELETE FROM tuik_adnks_totals WHERE source_id = @source_id AND reference_year = @reference_year";
            delete.Parameters.AddWithValue("source_id", sourceId);
            delete.Parameters.AddWithValue("reference_year", referenceYear);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = """
            INSERT INTO tuik_adnks_totals
                (source_id, reference_year, level, code, name, name_normalized, population, retrieved_at)
            VALUES
                (@source_id, @reference_year, @level, @code, @name, @name_normalized, @population, @retrieved_at)
            """;

        foreach (var total in totals)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = insertSql;
            insert.Parameters.AddWithValue("source_id", sourceId);
            insert.Parameters.AddWithValue("reference_year", referenceYear);
            insert.Parameters.AddWithValue("level", total.Level);
            insert.Parameters.AddWithValue("code", total.Code);
            insert.Parameters.AddWithValue("name", total.Name);
            insert.Parameters.AddWithValue("name_normalized", TurkishText.Normalize(total.Name));
            insert.Parameters.AddWithValue("population", total.Population);
            insert.Parameters.AddWithValue("retrieved_at", retrievedAt);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
