namespace NeighbourhoodExplorer.Api.Features.Metrics;

public sealed record MetricKeySet
{
    public static MetricKeySet Empty { get; } = new([]);

    private MetricKeySet(IReadOnlyList<string> keys) => Keys = keys;

    public IReadOnlyList<string> Keys { get; }

    public bool IsEmpty => Keys.Count == 0;

    public static MetricKeySet Parse(string? rawMetricKeys)
    {
        if (string.IsNullOrWhiteSpace(rawMetricKeys))
        {
            return Empty;
        }

        return new MetricKeySet(
            rawMetricKeys
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList());
    }
}
