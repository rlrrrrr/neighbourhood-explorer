using Npgsql;

namespace NeighbourhoodExplorer.Etl.Database;

public static class MetricCatalogWriter
{
    public static async Task<long> UpsertAsync(
        MetricCatalogWriteRequest request,
        CancellationToken cancellationToken)
    {
        await UpsertSourceAsync(request, cancellationToken);
        var metricId = await UpsertMetricAsync(request, cancellationToken);

        foreach (var translation in new[] { request.Definition.French, request.Definition.English })
        {
            await UpsertTranslationAsync(
                request.Connection,
                request.Transaction,
                metricId,
                translation,
                cancellationToken);
        }

        return metricId;
    }

    private static async Task UpsertSourceAsync(
        MetricCatalogWriteRequest request,
        CancellationToken cancellationToken)
    {
        var source = request.Source;
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            INSERT INTO data_source
                (id, name, organization, url, license, attribution_text, methodology,
                 retrieved_at, source_version)
            VALUES
                (@id, @name, @organization, @url, @license, @attribution_text, @methodology,
                 @retrieved_at, @source_version)
            ON CONFLICT (id) DO UPDATE SET
                name = EXCLUDED.name,
                organization = EXCLUDED.organization,
                url = EXCLUDED.url,
                license = EXCLUDED.license,
                attribution_text = EXCLUDED.attribution_text,
                methodology = EXCLUDED.methodology,
                retrieved_at = EXCLUDED.retrieved_at,
                source_version = EXCLUDED.source_version
            """;
        command.Parameters.AddWithValue("id", source.Id);
        command.Parameters.AddWithValue("name", source.Name);
        command.Parameters.AddWithValue("organization", source.Organization);
        command.Parameters.AddWithValue("url", source.Url);
        command.Parameters.AddWithValue("license", source.License);
        command.Parameters.AddWithValue("attribution_text", source.AttributionText);
        command.Parameters.AddWithValue("methodology", source.Methodology);
        command.Parameters.AddWithValue("retrieved_at", source.RetrievedAt);
        command.Parameters.AddWithValue("source_version", source.SourceVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> UpsertMetricAsync(
        MetricCatalogWriteRequest request,
        CancellationToken cancellationToken)
    {
        var definition = request.Definition;
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            INSERT INTO metric_definition
                (key, category, unit, native_level, polarity, normalization_method,
                 normalization_version, display_precision, source_id)
            VALUES
                (@key, @category, @unit, @native_level, @polarity, @normalization_method,
                 @normalization_version, @display_precision, @source_id)
            ON CONFLICT (key) DO UPDATE SET
                category = EXCLUDED.category,
                unit = EXCLUDED.unit,
                native_level = EXCLUDED.native_level,
                polarity = EXCLUDED.polarity,
                normalization_method = EXCLUDED.normalization_method,
                normalization_version = EXCLUDED.normalization_version,
                display_precision = EXCLUDED.display_precision,
                source_id = EXCLUDED.source_id
            RETURNING id
            """;
        command.Parameters.AddWithValue("key", definition.Key);
        command.Parameters.AddWithValue("category", definition.Category);
        command.Parameters.AddWithValue("unit", definition.Unit);
        command.Parameters.AddWithValue("native_level", definition.NativeLevel.ToWireValue());
        command.Parameters.AddWithValue("polarity", definition.Polarity.ToWireValue());
        command.Parameters.AddWithValue(
            "normalization_method",
            definition.NormalizationMethod.ToWireValue());
        command.Parameters.AddWithValue(
            "normalization_version",
            definition.NormalizationVersion);
        command.Parameters.AddWithValue("display_precision", definition.DisplayPrecision);
        command.Parameters.AddWithValue("source_id", request.Source.Id);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task UpsertTranslationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long metricId,
        MetricTranslation translation,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO metric_translation
                (metric_id, locale, name, description, subject)
            VALUES
                (@metric_id, @locale, @name, @description, @subject)
            ON CONFLICT (metric_id, locale) DO UPDATE SET
                name = EXCLUDED.name,
                description = EXCLUDED.description,
                subject = EXCLUDED.subject
            """;
        command.Parameters.AddWithValue("metric_id", metricId);
        command.Parameters.AddWithValue("locale", translation.Locale);
        command.Parameters.AddWithValue("name", translation.Name);
        command.Parameters.AddWithValue("description", translation.Description);
        command.Parameters.AddWithValue("subject", translation.Subject);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
