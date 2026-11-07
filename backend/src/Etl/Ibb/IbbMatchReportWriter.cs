using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NeighbourhoodExplorer.Etl.Ibb;

public sealed record IbbMatchReport(
    string SourceId,
    DateTimeOffset GeneratedAt,
    string RawFilePath,
    string InputSha256,
    int TotalRecords,
    IReadOnlyList<IbbCountCheck> Counts,
    IReadOnlyList<string> Failures,
    int RepairedNames,
    int MatchedByName,
    int MatchedByAlias,
    IReadOnlyList<IbbAliasMatch> AliasMatches,
    int OsmNeighborhoods,
    IReadOnlyList<IbbUnmatchedRow> Unmatched,
    string? Note);

public static class IbbMatchReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task WriteAsync(
        string dataDirectory,
        IbbMatchReport report,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(dataDirectory, "processed", report.SourceId);
        Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            Path.Combine(directory, "match-report.md"),
            Render(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "match-report.json"),
            JsonSerializer.Serialize(report, JsonOptions),
            cancellationToken);
    }

    private static string Render(IbbMatchReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Rapport d'appariement — bâtiments İBB 2017 ({report.SourceId})");
        builder.AppendLine();
        builder.AppendLine($"Généré le {report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)} par l'ETL.");
        builder.AppendLine();
        builder.AppendLine("| Champ | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Fichier source | `{report.RawFilePath}` |");
        builder.AppendLine($"| SHA-256 | `{report.InputSha256}` |");
        builder.AppendLine($"| Lignes lues | {report.TotalRecords} |");
        builder.AppendLine($"| Mahalles OSM disponibles | {report.OsmNeighborhoods} |");
        builder.AppendLine();

        builder.AppendLine("## Contrôles de nombre");
        builder.AppendLine();
        builder.AppendLine("| Contrôle | Attendu | Obtenu | Résultat |");
        builder.AppendLine("|---|---:|---:|---|");
        foreach (var count in report.Counts)
        {
            builder.AppendLine(
                $"| {count.Name} | {count.Expected} | {count.Actual} | {(count.Matches ? "conforme" : "**écart**")} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Échecs");
        builder.AppendLine();
        if (report.Failures.Count == 0)
        {
            builder.AppendLine("Aucun.");
        }
        else
        {
            foreach (var failure in report.Failures)
            {
                builder.AppendLine($"- {failure}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Encodage");
        builder.AppendLine();
        builder.AppendLine(
            $"{report.RepairedNames} noms réparés (latin-1 → cp1254), appliqué aux noms uniquement, jamais aux nombres.");

        builder.AppendLine();
        builder.AppendLine("## Appariement par (ilçe, nom normalisé)");
        builder.AppendLine();
        var percent = report.TotalRecords == 0
            ? 0
            : 100d * (report.MatchedByName + report.MatchedByAlias) / report.TotalRecords;
        builder.AppendLine(
            $"{report.MatchedByName + report.MatchedByAlias} mahalles appariés sur {report.TotalRecords} " +
            $"({percent.ToString("0.#", CultureInfo.InvariantCulture)} %) : " +
            $"{report.MatchedByName} par nom exact, {report.MatchedByAlias} par alias explicite.");
        builder.AppendLine();

        if (report.AliasMatches.Count > 0)
        {
            builder.AppendLine("### Alias appliqués");
            builder.AppendLine();
            builder.AppendLine("| Ilçe | Mahalle İBB | Mahalle OSM |");
            builder.AppendLine("|---|---|---|");
            foreach (var alias in report.AliasMatches)
            {
                builder.AppendLine($"| {alias.District} | {alias.Neighborhood} | {alias.OsmNeighborhood} |");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Mahalles İBB sans appariement");
        builder.AppendLine();
        if (report.Unmatched.Count == 0)
        {
            builder.AppendLine("Aucun.");
        }
        else
        {
            builder.AppendLine("| Ilçe | Mahalle | UAVT | Candidats OSM du même ilçe (non appariés) |");
            builder.AppendLine("|---|---|---:|---|");
            foreach (var row in report.Unmatched)
            {
                builder.AppendLine(
                    $"| {row.District} | {row.Neighborhood} | {row.MahalleUavt} | " +
                    $"{string.Join(", ", row.Candidates)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Limite de l'appariement");
        builder.AppendLine();
        builder.AppendLine(
            report.Note ?? "Le CSV İBB ne contient ni coordonnées ni géométrie : l'étape « inclusion spatiale » " +
            "ne peut pas s'exécuter sans une géométrie des mahalles İBB. Les mahalles restants sont documentés " +
            "dans `docs/data-istanbul.md` ; aucune correspondance spatiale n'est tentée.");

        return builder.ToString();
    }
}
