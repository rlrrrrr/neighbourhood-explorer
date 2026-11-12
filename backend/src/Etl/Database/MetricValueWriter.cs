namespace NeighbourhoodExplorer.Etl.Database;

public static class MetricValueWriter
{
    public static async Task WriteAsync(
        MetricValueWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Values.Count == 0)
        {
            return;
        }

        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            INSERT INTO metric_value
                (admin_area_id, metric_id, observed_at, reference_period, raw_value,
                 normalized_value, quality_flag, computed_at)
            SELECT values.admin_area_id,
                   @metric_id,
                   @observed_at,
                   @reference_period,
                   values.raw_value,
                   values.normalized_value,
                   @quality_flag,
                   @computed_at
            FROM unnest(@admin_area_ids, @raw_values, @normalized_values)
                AS values(admin_area_id, raw_value, normalized_value)
            ON CONFLICT (admin_area_id, metric_id, observed_at) DO UPDATE SET
                reference_period = EXCLUDED.reference_period,
                raw_value = EXCLUDED.raw_value,
                normalized_value = EXCLUDED.normalized_value,
                quality_flag = EXCLUDED.quality_flag,
                computed_at = EXCLUDED.computed_at
            """;
        command.Parameters.AddWithValue(
            "admin_area_ids",
            request.Values.Select(value => value.AdminAreaId).ToArray());
        command.Parameters.AddWithValue(
            "raw_values",
            request.Values.Select(value => value.RawValue).ToArray());
        command.Parameters.AddWithValue(
            "normalized_values",
            request.Values.Select(value => value.NormalizedValue).ToArray());
        command.Parameters.AddWithValue("metric_id", request.MetricId);
        command.Parameters.AddWithValue("observed_at", request.ObservedAt);
        command.Parameters.AddWithValue("reference_period", request.ReferencePeriod);
        command.Parameters.AddWithValue("quality_flag", request.QualityFlag.ToWireValue());
        command.Parameters.AddWithValue("computed_at", request.ComputedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
