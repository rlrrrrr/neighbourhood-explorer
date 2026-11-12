namespace NeighbourhoodExplorer.Etl.Database;

public sealed record MetricTranslation(
    string Locale,
    string Name,
    string Description,
    string Subject);
