using NeighbourhoodExplorer.Etl.Database;

namespace NeighbourhoodExplorer.Etl.Reports;

public sealed record MetricCoverageCheck
{
    public required string MetricKey { get; init; }

    public required int ExpectedValues { get; init; }

    public required IReadOnlyList<IMetricValue> Values { get; init; }

    public double MaximumRaw { get; init; } = double.PositiveInfinity;

    public void AppendFailures(List<string> failures)
    {
        if (Values.Count != ExpectedValues)
        {
            failures.Add(
                $"métrique {MetricKey} : {Values.Count} valeurs au lieu de {ExpectedValues}.");
        }

        if (Values.Any(value =>
                !double.IsFinite(value.RawValue)
                || !double.IsFinite(value.NormalizedValue)
                || value.RawValue < 0
                || value.RawValue > MaximumRaw
                || value.NormalizedValue < 0
                || value.NormalizedValue > 1))
        {
            failures.Add($"métrique {MetricKey} : valeur hors bornes.");
        }
    }
}
