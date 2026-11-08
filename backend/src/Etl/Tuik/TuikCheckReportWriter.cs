using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NeighbourhoodExplorer.Etl.Tuik;

public sealed record TuikCheck(string Name, string Expected, string Actual, bool Matches);

public sealed record TuikCheckReport(
    string SourceId,
    int ReferenceYear,
    DateTimeOffset GeneratedAt,
    string CanonicalChecksum,
    string CodelistPath,
    string DataPath,
    IReadOnlyList<TuikCheck> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<TuikTotal> Totals,
    string? Note);

public static class TuikCheckReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task WriteAsync(
        string dataDirectory,
        TuikCheckReport report,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(dataDirectory, "processed", report.SourceId);
        Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            Path.Combine(directory, "tuik-check.md"),
            Render(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "tuik-check.json"),
            JsonSerializer.Serialize(report, JsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, $"adnks-{report.ReferenceYear}-ilce.csv"),
            RenderCsv(report.Totals),
            cancellationToken);
    }

    private static string Render(TuikCheckReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Contrôle TÜİK ADNKS {report.ReferenceYear} ({report.SourceId})");
        builder.AppendLine();
        builder.AppendLine(
            $"Généré le {report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)} par l'ETL.");
        builder.AppendLine();
        builder.AppendLine("| Champ | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Codelist brute | `{report.CodelistPath}` |");
        builder.AppendLine($"| Données brutes | `{report.DataPath}` |");
        builder.AppendLine($"| Empreinte canonique | `{report.CanonicalChecksum}` |");
        builder.AppendLine();
        builder.AppendLine("## Contrôles");
        builder.AppendLine();
        builder.AppendLine("| Contrôle | Attendu | Obtenu | Résultat |");
        builder.AppendLine("|---|---:|---:|---|");
        foreach (var check in report.Checks)
        {
            builder.AppendLine(
                $"| {check.Name} | {check.Expected} | {check.Actual} | " +
                $"{(check.Matches ? "conforme" : "**écart**")} |");
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
        builder.AppendLine($"## Totaux ADNKS {report.ReferenceYear}");
        builder.AppendLine();
        builder.AppendLine("| Niveau | Code | Nom | Population |");
        builder.AppendLine("|---|---|---|---:|");
        foreach (var total in report.Totals.OrderBy(total => total.Level, StringComparer.Ordinal)
                     .ThenBy(total => total.Code, StringComparer.Ordinal))
        {
            builder.AppendLine(
                $"| {total.Level} | {total.Code} | {total.Name} | {total.Population} |");
        }

        if (!string.IsNullOrWhiteSpace(report.Note))
        {
            builder.AppendLine();
            builder.AppendLine("## Note");
            builder.AppendLine();
            builder.AppendLine(report.Note);
        }

        return builder.ToString();
    }

    private static string RenderCsv(IReadOnlyList<TuikTotal> totals)
    {
        var builder = new StringBuilder("level,code,name,population\n");
        foreach (var total in totals.OrderBy(total => total.Level, StringComparer.Ordinal)
                     .ThenBy(total => total.Code, StringComparer.Ordinal))
        {
            builder.AppendLine(
                $"{total.Level},{total.Code},{total.Name},{total.Population.ToString(CultureInfo.InvariantCulture)}");
        }

        return builder.ToString();
    }
}
