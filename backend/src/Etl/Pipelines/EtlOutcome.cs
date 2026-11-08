namespace NeighbourhoodExplorer.Etl.Pipelines;

public sealed record EtlOutcome(
    bool Success,
    string? InputSha256,
    IReadOnlyDictionary<string, int> RowCounts,
    string? ErrorMessage);
