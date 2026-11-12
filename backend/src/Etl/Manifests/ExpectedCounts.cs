namespace NeighbourhoodExplorer.Etl.Manifests;

public sealed record ExpectedCounts
{
    public static ExpectedCounts Empty { get; } = new();

    public IReadOnlyDictionary<ExpectedCountField, int> Counts { get; init; } =
        new Dictionary<ExpectedCountField, int>();

    public bool TryGet(ExpectedCountField field, out int value) =>
        Counts.TryGetValue(field, out value);

    public int Require(ExpectedCountField field, string manifestId) =>
        Counts.TryGetValue(field, out var value)
            ? value
            : throw new EtlFailureException(
                $"expected_counts.{field.ToWireValue()} manquant dans le manifeste « {manifestId} ».");

    public ExpectedCounts With(ExpectedCountField field, int value)
    {
        if (Counts.TryGetValue(field, out var current) && current != value)
        {
            throw new ManifestException(
                $"expected_counts.{field.ToWireValue()} ({value}) contredit " +
                $"une valeur précédente ({current}).");
        }

        var counts = new Dictionary<ExpectedCountField, int>(Counts) { [field] = value };
        return this with { Counts = counts };
    }
}
