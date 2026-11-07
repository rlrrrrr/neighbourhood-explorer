using NeighbourhoodExplorer.Etl.Manifests;

namespace NeighbourhoodExplorer.Etl.Tests;

internal static class TestManifests
{
    public static SourceManifest Osm(int province, int district, int neighborhood) => new()
    {
        Id = "osm-geofabrik-istanbul",
        Name = "OpenStreetMap, extrait de test",
        Organization = "OpenStreetMap contributors",
        Url = "https://example.invalid/turkey.osm.pbf",
        License = "ODbL 1.0",
        AttributionText = "© les contributeurs d'OpenStreetMap",
        RetrievedAt = "2026-10-31",
        ReferencePeriod = "2026-10-30",
        PublishedAt = "2026-10-30",
        Format = "pbf",
        ChecksumSha256 = "sha",
        VerifiedBy = "test",
        VerifiedAt = "2026-10-31",
        Steps = [],
        Outputs = [],
        Metrics = [],
        Pipeline = "osm-admin-boundaries",
        ExpectedCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["province"] = province,
            ["district"] = district,
            ["neighborhood"] = neighborhood,
        },
    };
}
