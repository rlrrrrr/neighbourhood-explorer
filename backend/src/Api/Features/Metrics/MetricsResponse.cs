namespace NeighbourhoodExplorer.Api.Features.Metrics;

public sealed record MetricsResponse(IReadOnlyList<MetricDefinitionResponse> Metrics);

public sealed record MetricDefinitionResponse(
    string Key,
    string Category,
    string Unit,
    string NativeLevel,
    string Polarity,
    MetricNormalizationResponse Normalization,
    int DisplayPrecision,
    IReadOnlyList<MetricTranslationResponse> Translations,
    MetricSourceResponse Source,
    DateOnly? ObservedAt);

public sealed record MetricNormalizationResponse(string Method, string Version);

public sealed record MetricTranslationResponse(
    string Locale,
    string Name,
    string Description,
    string Subject);

public sealed record MetricSourceResponse(
    string Id,
    string Name,
    string Organization,
    string Url,
    string License,
    string Attribution,
    string Methodology,
    DateOnly RetrievedAt,
    string Version);

public sealed record MetricValueResponse(double Raw, double Normalized, QualityFlag QualityFlag);

public sealed record NeighborhoodMetricsResponse(
    long NeighborhoodId,
    IReadOnlyList<NeighborhoodMetricResponse> Metrics);

public sealed record NeighborhoodMetricResponse(
    string Key,
    double Raw,
    double Normalized,
    QualityFlag QualityFlag,
    DateOnly ObservedAt,
    string ReferencePeriod,
    NeighborhoodMetricSourceResponse Source);

public sealed record NeighborhoodMetricSourceResponse(
    string Id,
    string Name,
    string Organization,
    string License,
    string Attribution);
