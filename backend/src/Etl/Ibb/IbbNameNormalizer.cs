namespace NeighbourhoodExplorer.Etl.Ibb;

public static class IbbNameNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> DistrictAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["eyup"] = "eyupsultan",
        };

    public static string NormalizeDistrict(string value)
    {
        var normalized = TurkishText.Normalize(value);
        return DistrictAliases.TryGetValue(normalized, out var alias) ? alias : normalized;
    }

    public static string NormalizeNeighborhood(string value) =>
        StripNeighborhoodSuffix(TurkishText.Normalize(value));

    private static string StripNeighborhoodSuffix(string value)
    {
        var current = value;
        while (true)
        {
            var stripped = current.EndsWith(" mahallesi", StringComparison.Ordinal)
                ? current[..^" mahallesi".Length]
                : current.EndsWith(" mahalle", StringComparison.Ordinal)
                    ? current[..^" mahalle".Length]
                    : null;

            if (string.IsNullOrWhiteSpace(stripped))
            {
                return current;
            }

            current = stripped;
        }
    }
}
