using System.Globalization;

namespace NeighbourhoodExplorer.Etl.Osm;

public sealed record UrbanFeatureBoundingBox(
    double MinLongitude,
    double MinLatitude,
    double MaxLongitude,
    double MaxLatitude)
{
    public UrbanFeatureBoundingBox Expand(double marginDegrees) => new(
        Math.Max(-180, MinLongitude - marginDegrees),
        Math.Max(-90, MinLatitude - marginDegrees),
        Math.Min(180, MaxLongitude + marginDegrees),
        Math.Min(90, MaxLatitude + marginDegrees));

    public string ToOsm2pgsqlArgument() => string.Join(
        ",",
        new[] { MinLongitude, MinLatitude, MaxLongitude, MaxLatitude }
            .Select(value => value.ToString("0.######", CultureInfo.InvariantCulture)));
}
