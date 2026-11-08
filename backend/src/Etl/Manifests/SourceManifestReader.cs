using System.Globalization;
using NeighbourhoodExplorer.Etl.Pipelines;

namespace NeighbourhoodExplorer.Etl.Manifests;

public static class SourceManifestReader
{
    public static SourceManifest Read(string path)
    {
        var document = ManifestDocument.Load(path);
        var rawPipeline = Require(document, "pipeline");
        if (!PipelineIdExtensions.TryParse(rawPipeline, out var pipelineId))
        {
            throw new ManifestException($"pipeline inconnu : {rawPipeline}");
        }

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
            Pipeline = pipelineId,
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

    private static ExpectedCounts ReadExpectedCounts(ManifestDocument document)
    {
        var counts = ExpectedCounts.Empty;
        foreach (var (key, value) in document.Map("expected_counts"))
        {
            if (!int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                throw new ManifestException($"expected_counts.{key} doit être un entier.");
            }

            counts = key switch
            {
                "province" => counts with { Province = parsed },
                "district" or "districts" => counts with
                {
                    Districts = MergeDistricts(counts.Districts, parsed, key),
                },
                "neighborhood" or "neighborhoods" => counts with
                {
                    Neighborhoods = MergeNeighborhoods(counts.Neighborhoods, parsed, key),
                },
                "rows" => counts with { Rows = parsed },
                "uavt" => counts with { Uavt = parsed },
                "buildings" => counts with { Buildings = parsed },
                "province_population" => counts with { ProvincePopulation = parsed },
                _ => throw new ManifestException($"expected_counts.{key} inconnu."),
            };
        }

        return counts;
    }

    private static int MergeDistricts(int? existing, int value, string key)
    {
        if (existing is { } current && current != value)
        {
            throw new ManifestException(
                $"expected_counts.{key} ({value}) contredit expected_counts.districts ({current}).");
        }

        return value;
    }

    private static int MergeNeighborhoods(int? existing, int value, string key)
    {
        if (existing is { } current && current != value)
        {
            throw new ManifestException(
                $"expected_counts.{key} ({value}) contredit expected_counts.neighborhoods ({current}).");
        }

        return value;
    }

    private static string Require(ManifestDocument document, string key) =>
        document.Text(key) is { Length: > 0 } value
            ? value
            : throw new ManifestException($"champ obligatoire manquant ou vide : {key}");
}
