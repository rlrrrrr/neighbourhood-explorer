namespace NeighbourhoodExplorer.Etl.Osm;

public sealed record CountCheck(string Level, int Actual, int Expected, bool Matches);

public sealed record CoverageRow(
    string Area,
    double AreaKm2,
    double SumPartsKm2,
    double UnionPartsKm2,
    double CoveredKm2,
    double GapKm2,
    double OverlapKm2,
    double SpillKm2,
    double GapPercent,
    double OverlapPercent);

public sealed record BoundingBox(double MinLongitude, double MinLatitude, double MaxLongitude, double MaxLatitude);

public sealed record AdminAreaQualityReport(
    DateTimeOffset GeneratedAt,
    string SourceId,
    string RawFilePath,
    string InputSha256,
    string Osm2pgsqlVersion,
    IReadOnlyList<CountCheck> Counts,
    IReadOnlyList<CoverageRow> DistrictCoverage,
    CoverageRow ProvinceCoverage,
    BoundingBox Bounds,
    AdminAreaBuildResult Build,
    IReadOnlyList<string> Failures);

public sealed record AdminAreaQualityRequest(
    Manifests.SourceManifest Manifest,
    AdminAreaBuildResult Build,
    string RawFilePath,
    string InputSha256,
    string Osm2pgsqlVersion);
