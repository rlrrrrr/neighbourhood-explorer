using System.Globalization;
using Npgsql;

namespace NeighbourhoodExplorer.Api.Features.Metrics;

public static class MetricsReader
{
    public static async Task<IReadOnlyList<MetricDefinitionResponse>> ReadDefinitionsAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                m.id,
                m.key,
                m.category,
                m.unit,
                m.native_level,
                m.polarity,
                m.normalization_method,
                m.normalization_version,
                m.display_precision,
                s.id,
                s.name,
                s.organization,
                s.url,
                s.license,
                s.attribution_text,
                s.methodology,
                s.retrieved_at,
                s.source_version,
                (SELECT max(v.observed_at) FROM metric_value v WHERE v.metric_id = m.id)
            FROM metric_definition m
            JOIN data_source s ON s.id = m.source_id
            ORDER BY m.key
            """;

        var definitions = new List<MetricDefinitionRow>();
        await using (var command = dataSource.CreateCommand(sql))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                definitions.Add(new MetricDefinitionRow(
                    Id: reader.GetInt64(0),
                    Key: reader.GetString(1),
                    Category: reader.GetString(2),
                    Unit: reader.GetString(3),
                    NativeLevel: reader.GetString(4),
                    Polarity: reader.GetString(5),
                    NormalizationMethod: reader.GetString(6),
                    NormalizationVersion: reader.GetString(7),
                    DisplayPrecision: reader.GetInt32(8),
                    SourceId: reader.GetString(9),
                    SourceName: reader.GetString(10),
                    SourceOrganization: reader.GetString(11),
                    SourceUrl: reader.GetString(12),
                    SourceLicense: reader.GetString(13),
                    SourceAttribution: reader.GetString(14),
                    SourceMethodology: reader.GetString(15),
                    SourceRetrievedAt: reader.GetFieldValue<DateOnly>(16),
                    SourceVersion: reader.GetString(17),
                    ObservedAt: reader.IsDBNull(18) ? null : reader.GetFieldValue<DateOnly>(18)));
            }
        }

        var translations = await ReadTranslationsAsync(
            dataSource,
            definitions.Select(definition => definition.Id).ToList(),
            cancellationToken);

        return definitions
            .Select(definition => new MetricDefinitionResponse(
                Key: definition.Key,
                Category: definition.Category,
                Unit: definition.Unit,
                NativeLevel: definition.NativeLevel,
                Polarity: definition.Polarity,
                Normalization: new MetricNormalizationResponse(
                    definition.NormalizationMethod,
                    definition.NormalizationVersion),
                DisplayPrecision: definition.DisplayPrecision,
                Translations: translations.GetValueOrDefault(definition.Id, []),
                Source: new MetricSourceResponse(
                    Id: definition.SourceId,
                    Name: definition.SourceName,
                    Organization: definition.SourceOrganization,
                    Url: definition.SourceUrl,
                    License: definition.SourceLicense,
                    Attribution: definition.SourceAttribution,
                    Methodology: definition.SourceMethodology,
                    RetrievedAt: definition.SourceRetrievedAt,
                    Version: definition.SourceVersion),
                ObservedAt: definition.ObservedAt))
            .ToList();
    }

    public static async Task<IReadOnlySet<string>> ReadKnownKeysAsync(
        NpgsqlDataSource dataSource,
        IReadOnlyList<string> metricKeys,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT key
            FROM metric_definition
            WHERE key = ANY(@metric_keys)
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("metric_keys", metricKeys.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            keys.Add(reader.GetString(0));
        }

        return keys;
    }

    public static async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, MetricValueResponse>>> ReadLatestValuesAsync(
        NpgsqlDataSource dataSource,
        long cityId,
        IReadOnlyList<string> metricKeys,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT m.key,
                   n.id,
                   latest.raw_value,
                   latest.normalized_value,
                   latest.quality_flag
            FROM metric_definition m
            JOIN admin_area d ON d.level = 'district' AND d.parent_id = @city_id
            JOIN admin_area n ON n.level = 'neighborhood' AND n.parent_id = d.id
            CROSS JOIN LATERAL (
                SELECT v.raw_value, v.normalized_value, v.quality_flag
                FROM metric_value v
                WHERE v.metric_id = m.id
                  AND v.admin_area_id = n.id
                ORDER BY v.observed_at DESC
                LIMIT 1
            ) latest
            WHERE m.key = ANY(@metric_keys)
            """;

        var grouped = metricKeys.ToDictionary(
            key => key,
            _ => new Dictionary<string, MetricValueResponse>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("city_id", cityId);
        command.Parameters.AddWithValue("metric_keys", metricKeys.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(0);
            var neighborhoodId = reader.GetInt64(1).ToString(CultureInfo.InvariantCulture);
            grouped[key][neighborhoodId] = new MetricValueResponse(
                Raw: reader.GetDouble(2),
                Normalized: reader.GetDouble(3),
                QualityFlag: QualityFlagValues.Parse(reader.GetString(4)));
        }

        return grouped.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyDictionary<string, MetricValueResponse>)entry.Value,
            StringComparer.Ordinal);
    }

    public static async Task<NeighborhoodMetricsResponse?> ReadNeighborhoodMetricsAsync(
        NpgsqlDataSource dataSource,
        long neighborhoodId,
        CancellationToken cancellationToken)
    {
        if (!await NeighborhoodExistsAsync(dataSource, neighborhoodId, cancellationToken))
        {
            return null;
        }

        const string sql = """
            SELECT DISTINCT ON (m.key)
                   m.key,
                   v.raw_value,
                   v.normalized_value,
                   v.quality_flag,
                   v.observed_at,
                   v.reference_period,
                   s.id,
                   s.name,
                   s.organization,
                   s.license,
                   s.attribution_text
            FROM metric_value v
            JOIN metric_definition m ON m.id = v.metric_id
            JOIN data_source s ON s.id = m.source_id
            WHERE v.admin_area_id = @neighborhood_id
            ORDER BY m.key, v.observed_at DESC
            """;

        var metrics = new List<NeighborhoodMetricResponse>();
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("neighborhood_id", neighborhoodId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            metrics.Add(new NeighborhoodMetricResponse(
                Key: reader.GetString(0),
                Raw: reader.GetDouble(1),
                Normalized: reader.GetDouble(2),
                QualityFlag: QualityFlagValues.Parse(reader.GetString(3)),
                ObservedAt: reader.GetFieldValue<DateOnly>(4),
                ReferencePeriod: reader.GetString(5),
                Source: new NeighborhoodMetricSourceResponse(
                    Id: reader.GetString(6),
                    Name: reader.GetString(7),
                    Organization: reader.GetString(8),
                    License: reader.GetString(9),
                    Attribution: reader.GetString(10))));
        }

        return new NeighborhoodMetricsResponse(neighborhoodId, metrics);
    }

    private static async Task<bool> NeighborhoodExistsAsync(
        NpgsqlDataSource dataSource,
        long neighborhoodId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM admin_area
            WHERE id = @neighborhood_id AND level = 'neighborhood'
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("neighborhood_id", neighborhoodId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<IReadOnlyDictionary<long, IReadOnlyList<MetricTranslationResponse>>> ReadTranslationsAsync(
        NpgsqlDataSource dataSource,
        IReadOnlyList<long> metricIds,
        CancellationToken cancellationToken)
    {
        if (metricIds.Count == 0)
        {
            return new Dictionary<long, IReadOnlyList<MetricTranslationResponse>>();
        }

        const string sql = """
            SELECT metric_id, locale, name, description, subject
            FROM metric_translation
            WHERE metric_id = ANY(@metric_ids)
            ORDER BY metric_id, locale
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("metric_ids", metricIds.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var grouped = new Dictionary<long, List<MetricTranslationResponse>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var metricId = reader.GetInt64(0);
            if (!grouped.TryGetValue(metricId, out var translations))
            {
                translations = [];
                grouped[metricId] = translations;
            }

            translations.Add(new MetricTranslationResponse(
                Locale: reader.GetString(1),
                Name: reader.GetString(2),
                Description: reader.GetString(3),
                Subject: reader.GetString(4)));
        }

        return grouped.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<MetricTranslationResponse>)entry.Value);
    }

    private sealed record MetricDefinitionRow(
        long Id,
        string Key,
        string Category,
        string Unit,
        string NativeLevel,
        string Polarity,
        string NormalizationMethod,
        string NormalizationVersion,
        int DisplayPrecision,
        string SourceId,
        string SourceName,
        string SourceOrganization,
        string SourceUrl,
        string SourceLicense,
        string SourceAttribution,
        string SourceMethodology,
        DateOnly SourceRetrievedAt,
        string SourceVersion,
        DateOnly? ObservedAt);
}
