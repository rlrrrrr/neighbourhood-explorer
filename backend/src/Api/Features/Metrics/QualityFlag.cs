using System.Text.Json.Serialization;

namespace NeighbourhoodExplorer.Api.Features.Metrics;

[JsonConverter(typeof(JsonStringEnumConverter<QualityFlag>))]
public enum QualityFlag
{
    [JsonStringEnumMemberName("ok")]
    Ok,

    [JsonStringEnumMemberName("estimated")]
    Estimated,

    [JsonStringEnumMemberName("stale")]
    Stale,

    [JsonStringEnumMemberName("low_coverage")]
    LowCoverage,

    [JsonStringEnumMemberName("inherited")]
    Inherited,
}

public static class QualityFlagValues
{
    public static QualityFlag Parse(string value) => value switch
    {
        "ok" => QualityFlag.Ok,
        "estimated" => QualityFlag.Estimated,
        "stale" => QualityFlag.Stale,
        "low_coverage" => QualityFlag.LowCoverage,
        "inherited" => QualityFlag.Inherited,
        _ => throw new InvalidOperationException($"quality_flag inconnu : « {value} »."),
    };
}
