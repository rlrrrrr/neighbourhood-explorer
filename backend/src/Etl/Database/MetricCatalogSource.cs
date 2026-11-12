namespace NeighbourhoodExplorer.Etl.Database;

public sealed record MetricCatalogSource(
    string Id,
    string Name,
    string Organization,
    string Url,
    string License,
    string AttributionText,
    string Methodology,
    DateOnly RetrievedAt,
    string SourceVersion);
