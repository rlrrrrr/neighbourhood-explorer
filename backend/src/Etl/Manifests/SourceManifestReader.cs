using System.Globalization;

namespace NeighbourhoodExplorer.Etl.Manifests;

public static class SourceManifestReader
{
    public static SourceManifest Read(string path)
    {
        var document = ManifestDocument.Load(path);

        return new SourceManifest
        {
            Id = Require(document, "id"),
            Name = Require(document, "name"),
            Organization = Require(document, "organization"),
            Url = Require(document, "url"),
            License = Require(document, "license"),
            AttributionText = Require(document, "attribution_text"),
            RetrievedAt = document.Text("retrieved_at"),
            ProvinceCode = document.Text("province_code"),
            ProvinceName = document.Text("province_name"),
            GridYear = ReadOptionalInt(document, "grid_year"),
            ReferencePeriod = Require(document, "reference_period"),
            PublishedAt = Require(document, "published_at"),
            SourceVersion = document.Text("source_version"),
            Format = Require(document, "format"),
            CoordinateSystem = document.Text("coordinate_system"),
            Encoding = document.Text("encoding"),
            ChecksumSha256 = document.Text("checksum_sha256"),
            VerifiedBy = document.Text("verified_by"),
            VerifiedAt = document.Text("verified_at"),
            Steps = document.List("steps"),
            Outputs = document.List("outputs"),
            Metrics = document.List("metrics"),
            Pipeline = Require(document, "pipeline"),
            ExpectedCounts = ReadExpectedCounts(document),
        };
    }

    public static IReadOnlyList<string> FindMissingFields(SourceManifest manifest)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(manifest.VerifiedBy))
        {
            missing.Add("verified_by");
        }

        if (string.IsNullOrWhiteSpace(manifest.VerifiedAt))
        {
            missing.Add("verified_at");
        }

        if (string.IsNullOrWhiteSpace(manifest.RetrievedAt))
        {
            missing.Add("retrieved_at");
        }

        if (string.IsNullOrWhiteSpace(manifest.ChecksumSha256))
        {
            missing.Add("checksum_sha256");
        }

        return missing;
    }

    private static int? ReadOptionalInt(ManifestDocument document, string key)
    {
        var raw = document.Text(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ManifestException($"{key} doit être un entier.");
    }

    private static IReadOnlyDictionary<string, int> ReadExpectedCounts(ManifestDocument document)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (level, value) in document.Map("expected_counts"))
        {
            if (!int.TryParse(value, out var parsed))
            {
                throw new ManifestException($"expected_counts.{level} doit être un entier.");
            }

            counts[level] = parsed;
        }

        return counts;
    }

    private static string Require(ManifestDocument document, string key) =>
        document.Text(key) is { Length: > 0 } value
            ? value
            : throw new ManifestException($"champ obligatoire manquant ou vide : {key}");
}
