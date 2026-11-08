namespace NeighbourhoodExplorer.Etl.Manifests;

public enum ExpectedCountField
{
    Province,
    Districts,
    Neighborhoods,
    Rows,
    Uavt,
    Buildings,
    ProvincePopulation,
}

public sealed record ExpectedCounts
{
    public static ExpectedCounts Empty { get; } = new();

    public int? Province { get; init; }

    public int? Districts { get; init; }

    public int? Neighborhoods { get; init; }

    public int? Rows { get; init; }

    public int? Uavt { get; init; }

    public int? Buildings { get; init; }

    public int? ProvincePopulation { get; init; }

    public bool TryGet(ExpectedCountField field, out int value)
    {
        int? candidate = field switch
        {
            ExpectedCountField.Province => Province,
            ExpectedCountField.Districts => Districts,
            ExpectedCountField.Neighborhoods => Neighborhoods,
            ExpectedCountField.Rows => Rows,
            ExpectedCountField.Uavt => Uavt,
            ExpectedCountField.Buildings => Buildings,
            ExpectedCountField.ProvincePopulation => ProvincePopulation,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };

        value = candidate.GetValueOrDefault();
        return candidate.HasValue;
    }

    public int Require(ExpectedCountField field, string manifestId) =>
        TryGet(field, out var value)
            ? value
            : throw new EtlFailureException(
                $"expected_counts.{FieldName(field)} manquant dans le manifeste « {manifestId} ».");

    private static string FieldName(ExpectedCountField field) => field switch
    {
        ExpectedCountField.Province => "province",
        ExpectedCountField.Districts => "districts",
        ExpectedCountField.Neighborhoods => "neighborhoods",
        ExpectedCountField.Rows => "rows",
        ExpectedCountField.Uavt => "uavt",
        ExpectedCountField.Buildings => "buildings",
        ExpectedCountField.ProvincePopulation => "province_population",
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };
}
