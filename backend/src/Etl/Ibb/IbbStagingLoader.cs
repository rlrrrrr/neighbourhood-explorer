using Npgsql;
using NpgsqlTypes;

namespace NeighbourhoodExplorer.Etl.Ibb;

public static class IbbStagingLoader
{
    public static async Task ReplaceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourceId,
        IReadOnlyList<IbbMatchRow> rows,
        DateTimeOffset loadedAt,
        CancellationToken cancellationToken)
    {
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM staging_ibb_buildings_2017 WHERE source_id = @source_id";
            delete.Parameters.AddWithValue("source_id", sourceId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var importer = await connection.BeginBinaryImportAsync(
            """
            COPY staging_ibb_buildings_2017 (
                source_id,
                mahalle_uavt,
                district_name,
                neighborhood_name,
                district_normalized,
                neighborhood_normalized,
                buildings_before_1980,
                buildings_1980_2000,
                buildings_after_2000,
                buildings_1_4_floors,
                buildings_5_9_floors,
                buildings_9_19_floors,
                admin_area_id,
                match_method,
                loaded_at)
            FROM STDIN (FORMAT BINARY)
            """,
            cancellationToken);

        foreach (var matchRow in rows)
        {
            var row = matchRow.Row;
            await importer.StartRowAsync(cancellationToken);
            await importer.WriteAsync(sourceId, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteAsync(row.MahalleUavt, NpgsqlDbType.Bigint, cancellationToken);
            await importer.WriteAsync(row.DistrictName, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteAsync(row.NeighborhoodName, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteAsync(row.DistrictNormalized, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteAsync(row.NeighborhoodNormalized, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteAsync(row.BuildingsBefore1980, NpgsqlDbType.Integer, cancellationToken);
            await importer.WriteAsync(row.BuildingsBetween1980And2000, NpgsqlDbType.Integer, cancellationToken);
            await importer.WriteAsync(row.BuildingsAfter2000, NpgsqlDbType.Integer, cancellationToken);
            await importer.WriteAsync(row.BuildingsWith1To4Floors, NpgsqlDbType.Integer, cancellationToken);
            await importer.WriteAsync(row.BuildingsWith5To9Floors, NpgsqlDbType.Integer, cancellationToken);
            await importer.WriteAsync(row.BuildingsWith9To19Floors, NpgsqlDbType.Integer, cancellationToken);

            if (matchRow.AdminAreaId is long adminAreaId)
            {
                await importer.WriteAsync(adminAreaId, NpgsqlDbType.Bigint, cancellationToken);
            }
            else
            {
                await importer.WriteNullAsync(cancellationToken);
            }

            if (matchRow.MatchMethod is string matchMethod)
            {
                await importer.WriteAsync(matchMethod, NpgsqlDbType.Text, cancellationToken);
            }
            else
            {
                await importer.WriteNullAsync(cancellationToken);
            }

            await importer.WriteAsync(loadedAt, NpgsqlDbType.TimestampTz, cancellationToken);
        }

        await importer.CompleteAsync(cancellationToken);
    }
}
