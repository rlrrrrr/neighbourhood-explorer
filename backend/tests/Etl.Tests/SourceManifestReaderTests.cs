using NeighbourhoodExplorer.Etl;
using NeighbourhoodExplorer.Etl.Manifests;

namespace NeighbourhoodExplorer.Etl.Tests;

public class SourceManifestReaderTests
{
    private const string CompleteManifest = """
        id: osm-geofabrik-istanbul
        name: Test
        organization: Test
        url: https://example.invalid/data.pbf
        license: ODbL 1.0
        attribution_text: © les contributeurs d'OpenStreetMap
        retrieved_at: 2026-10-31
        reference_period: 2026-10-30
        published_at: 2026-10-30
        format: pbf
        checksum_sha256: 9f900a7a296b86e37a60d1686c28187612b70dc65ee7d70c917d83689be3ec50
        verified_by:
        verified_at:
        steps: [import]
        outputs: [data/processed/report.md]
        metrics: []
        pipeline: osm-admin-boundaries
        expected_counts:
          province: 1
          district: 39
          neighborhood: 964
        """;

    [Fact]
    public void Reports_missing_human_verification()
    {
        var manifest = ReadManifest(CompleteManifest);

        var missing = SourceManifestReader.FindMissingFields(manifest);

        Assert.Equal(["verified_by", "verified_at"], missing);
    }

    [Fact]
    public void Accepts_manifest_after_human_verification()
    {
        var manifest = ReadManifest(CompleteManifest.Replace(
            "verified_by:\nverified_at:",
            "verified_by: humain\nverified_at: 2026-10-31"));

        Assert.Empty(SourceManifestReader.FindMissingFields(manifest));
        Assert.Equal("humain", manifest.VerifiedBy);
        Assert.Equal(
            39,
            manifest.ExpectedCounts.Require(ExpectedCountField.Districts, manifest.Id));
        Assert.Equal(["import"], manifest.Steps);
    }

    [Fact]
    public void Reads_the_optional_province_and_grid_year()
    {
        var manifest = ReadManifest(CompleteManifest.Replace(
            "reference_period: 2026-10-30",
            "reference_period: 2026-10-30\nprovince_code: TR100\nprovince_name: İstanbul\ngrid_year: 2025"));

        Assert.Equal("TR100", manifest.ProvinceCode);
        Assert.Equal("İstanbul", manifest.ProvinceName);
        Assert.Equal(2025, manifest.GridYear);
    }

    [Fact]
    public void Reads_the_optional_raw_and_data_source_ids()
    {
        var manifest = ReadManifest(CompleteManifest.Replace(
            "name: Test",
            "raw_source_id: osm-geofabrik-istanbul\ndata_source_id: osm-geofabrik-istanbul\nname: Test"));

        Assert.Equal("osm-geofabrik-istanbul", manifest.RawSourceId);
        Assert.Equal("osm-geofabrik-istanbul", manifest.DataSourceId);
    }

    [Fact]
    public void Leaves_the_raw_and_data_source_ids_empty_when_absent()
    {
        var manifest = ReadManifest(CompleteManifest);

        Assert.Null(manifest.RawSourceId);
        Assert.Null(manifest.DataSourceId);
    }

    [Fact]
    public void Reads_transport_expected_counts()
    {
        var manifest = ReadManifest(CompleteManifest.Replace(
            "  neighborhood: 964",
            """
              neighborhood: 964
              bus_stops: 15387
              rail_stations: 245
              ferry_piers: 55
            """));

        Assert.Equal(
            15387,
            manifest.ExpectedCounts.Require(ExpectedCountField.BusStops, manifest.Id));
        Assert.Equal(
            245,
            manifest.ExpectedCounts.Require(ExpectedCountField.RailStations, manifest.Id));
        Assert.Equal(
            55,
            manifest.ExpectedCounts.Require(ExpectedCountField.FerryPiers, manifest.Id));
    }

    [Fact]
    public void Rejects_a_non_integer_grid_year()
    {
        var path = WriteManifest(CompleteManifest.Replace(
            "reference_period: 2026-10-30",
            "reference_period: 2026-10-30\ngrid_year: vingt-vingt-cinq"));
        try
        {
            Assert.Throws<ManifestException>(() => SourceManifestReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Rejects_missing_required_field()
    {
        var path = WriteManifest(CompleteManifest.Replace("format: pbf", "format:"));
        try
        {
            Assert.Throws<ManifestException>(() => SourceManifestReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SourceManifest ReadManifest(string content)
    {
        var path = WriteManifest(content);
        try
        {
            return SourceManifestReader.Read(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteManifest(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"manifest-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, content);
        return path;
    }
}
