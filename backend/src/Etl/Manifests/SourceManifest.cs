namespace NeighbourhoodExplorer.Etl.Manifests;

public sealed record SourceManifest
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Organization { get; init; }

    public required string Url { get; init; }

    public required string License { get; init; }

    public required string AttributionText { get; init; }

    public string? RetrievedAt { get; init; }

    public string? ProvinceCode { get; init; }

    public string? ProvinceName { get; init; }

    public int? GridYear { get; init; }

    public required string ReferencePeriod { get; init; }

    public required string PublishedAt { get; init; }

    public string? SourceVersion { get; init; }

    public required string Format { get; init; }

    public string? CoordinateSystem { get; init; }

    public string? Encoding { get; init; }

    public string? ChecksumSha256 { get; init; }

    public string? VerifiedBy { get; init; }

    public string? VerifiedAt { get; init; }

    public required IReadOnlyList<string> Steps { get; init; }

    public required IReadOnlyList<string> Outputs { get; init; }

    public required IReadOnlyList<string> Metrics { get; init; }

    public required string Pipeline { get; init; }

    public IReadOnlyDictionary<string, int> ExpectedCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
}
