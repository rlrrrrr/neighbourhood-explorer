using System.Text.Json;

namespace NeighbourhoodExplorer.Etl.Manifests;

public static class ExpectedCountFieldNames
{
    private static readonly JsonNamingPolicy WireNames = JsonNamingPolicy.SnakeCaseLower;

    public static string ToWireValue(this ExpectedCountField field) =>
        WireNames.ConvertName(field.ToString());

    public static bool TryParse(string key, out ExpectedCountField field)
    {
        switch (key)
        {
            case "district":
                field = ExpectedCountField.Districts;
                return true;
            case "neighborhood":
                field = ExpectedCountField.Neighborhoods;
                return true;
            default:
                foreach (var candidate in Enum.GetValues<ExpectedCountField>())
                {
                    if (string.Equals(candidate.ToWireValue(), key, StringComparison.Ordinal))
                    {
                        field = candidate;
                        return true;
                    }
                }

                field = default;
                return false;
        }
    }
}
