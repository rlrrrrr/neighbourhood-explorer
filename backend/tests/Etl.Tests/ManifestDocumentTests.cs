using NeighbourhoodExplorer.Etl.Manifests;

namespace NeighbourhoodExplorer.Etl.Tests;

public class ManifestDocumentTests
{
    [Fact]
    public void Parses_scalars_flow_lists_block_lists_and_nested_maps()
    {
        string[] lines =
        [
            "# commentaire de tête",
            "id: osm-geofabrik-istanbul",
            "license: ODbL 1.0 — https://opendatacommons.org/licenses/odbl/1-0/",
            "attribution_text: '© les contributeurs d''OpenStreetMap'",
            "steps: [import, build-admin-area, quality-report]",
            "outputs:",
            "  - data/processed/a.md",
            "  - data/processed/a.json",
            "expected_counts:",
            "  province: 1",
            "  district: 39",
            "  neighborhood: 964   # commentaire de fin de ligne",
            "verified_by:",
        ];

        var document = ManifestDocument.Parse(lines, "test.yaml");

        Assert.Equal("osm-geofabrik-istanbul", document.Text("id"));
        Assert.Equal("ODbL 1.0 — https://opendatacommons.org/licenses/odbl/1-0/", document.Text("license"));
        Assert.Equal("© les contributeurs d'OpenStreetMap", document.Text("attribution_text"));
        Assert.Equal(["import", "build-admin-area", "quality-report"], document.List("steps"));
        Assert.Equal(["data/processed/a.md", "data/processed/a.json"], document.List("outputs"));
        Assert.Equal("39", document.Map("expected_counts")["district"]);
        Assert.Null(document.Text("verified_by"));
    }

    [Fact]
    public void Rejects_duplicate_keys()
    {
        string[] lines = ["id: a", "id: b"];

        var exception = Assert.Throws<ManifestException>(() => ManifestDocument.Parse(lines, "test.yaml"));

        Assert.Contains("dupliquée", exception.Message);
    }

    [Fact]
    public void Rejects_lines_without_separator()
    {
        string[] lines = ["id: a", "pas une paire"];

        var exception = Assert.Throws<ManifestException>(() => ManifestDocument.Parse(lines, "test.yaml"));

        Assert.Contains("test.yaml:2", exception.Message);
    }
}
