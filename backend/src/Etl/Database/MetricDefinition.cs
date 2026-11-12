namespace NeighbourhoodExplorer.Etl.Database;

public sealed record MetricDefinition(
    string Key,
    string Category,
    string Unit,
    MetricPolarity Polarity,
    int DisplayPrecision,
    MetricQualityFlag QualityFlag,
    string Methodology,
    MetricTranslation French,
    MetricTranslation English,
    MetricNativeLevel NativeLevel = MetricNativeLevel.Neighborhood,
    MetricNormalizationMethod NormalizationMethod = MetricNormalizationMethod.Percentile,
    string NormalizationVersion = "percentile-v1");
