using System.Text.Json;

namespace NeighbourhoodExplorer.Etl.Database;

public static class MetricVocabulary
{
    private static readonly JsonNamingPolicy WireNames = JsonNamingPolicy.SnakeCaseLower;

    public static string ToWireValue(this MetricNativeLevel value) =>
        WireNames.ConvertName(value.ToString());

    public static string ToWireValue(this MetricPolarity value) =>
        WireNames.ConvertName(value.ToString());

    public static string ToWireValue(this MetricNormalizationMethod value) =>
        WireNames.ConvertName(value.ToString());

    public static string ToWireValue(this MetricQualityFlag value) =>
        WireNames.ConvertName(value.ToString());
}
