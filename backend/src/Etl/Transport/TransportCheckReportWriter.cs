using System.Text;
using System.Text.Json;
using static NeighbourhoodExplorer.Etl.Reports.CheckReportFormat;

namespace NeighbourhoodExplorer.Etl.Transport;

public sealed record TransportSourceFileResult(
    string FileName,
    int Valid,
    int Rejected,
    string Sha256);

public sealed record TransportNeighborhoodResult(
    string DistrictName,
    string NeighborhoodName,
    int BusStops,
    int RailStations,
    int FerryPiers,
    double WeightedPoints,
    double Density,
    double Normalized);

public sealed record TransportModePointCount(string Mode, int Count);

public sealed record TransportCheckReport(
    string SourceId,
    string DataSourceId,
    string SourceName,
    string SourceUrl,
    string License,
    string AttributionText,
    string SourceVersion,
    string BundleChecksum,
    IReadOnlyList<TransportSourceFileResult> Files,
    IReadOnlyList<TransportModePointCount> OutsideProvince,
    int BusPoints,
    int RailPoints,
    int FerryPoints,
    int DistinctBus,
    int DistinctRail,
    int DistinctFerry,
    int PolygonAssigned,
    int NearestAssigned,
    int Unassigned,
    IReadOnlyList<string> UnassignedPoints,
    int ReferenceYear,
    string ReferencePeriod,
    DateOnly ObservedAt,
    DateTimeOffset ComputedAt,
    int NeighborhoodCount,
    int DistrictCount,
    int ExpectedNeighborhoods,
    int ExpectedDistricts,
    IReadOnlyList<TransportNeighborhoodResult> Neighborhoods,
    IReadOnlyList<string> Failures);

public static class TransportCheckReportWriter
{
    public static async Task WriteAsync(
        string dataDirectory,
        TransportCheckReport report,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(dataDirectory, "processed", report.SourceId);
        Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            Path.Combine(directory, "transport-check.md"),
            BuildMarkdown(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "transport-check.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "transport-mahalle.csv"),
            BuildCsv(report),
            cancellationToken);
    }

    private static string BuildMarkdown(TransportCheckReport report)
    {
        var densities = report.Neighborhoods
            .Select(item => item.Density)
            .OrderBy(value => value)
            .ToList();
        var normalized = report.Neighborhoods
            .Select(item => item.Normalized)
            .OrderBy(value => value)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine($"# Contrôle des transports — {report.SourceId}");
        builder.AppendLine();
        builder.AppendLine(report.Failures.Count == 0
            ? "**Résultat : conforme.**"
            : "**Résultat : non conforme.**");
        builder.AppendLine();
        builder.AppendLine("## Source");
        builder.AppendLine();
        builder.AppendLine("| Champ | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Jeu | {Escape(report.SourceName)} |");
        builder.AppendLine($"| URL | {Escape(report.SourceUrl)} |");
        builder.AppendLine($"| Version | {Escape(report.SourceVersion)} |");
        builder.AppendLine($"| Licence | {Escape(report.License)} |");
        builder.AppendLine($"| Attribution | {Escape(report.AttributionText)} |");
        builder.AppendLine($"| `data_source` | `{report.DataSourceId}` |");
        builder.AppendLine($"| SHA-256 du lot | `{report.BundleChecksum}` |");
        builder.AppendLine($"| `observed_at` | {report.ObservedAt:yyyy-MM-dd} |");
        builder.AppendLine($"| `reference_period` | {Escape(report.ReferencePeriod)} |");
        builder.AppendLine($"| Calculé le | {report.ComputedAt:yyyy-MM-dd HH:mm:ss} UTC |");
        builder.AppendLine();
        builder.AppendLine("## Fichiers bruts");
        builder.AppendLine();
        builder.AppendLine("| Fichier | Points valides | Rejetés | SHA-256 |");
        builder.AppendLine("|---|---:|---:|---|");
        foreach (var file in report.Files)
        {
            builder.AppendLine(
                $"| `{Escape(file.FileName)}` | {file.Valid:N0} | {file.Rejected:N0} | " +
                $"`{file.Sha256}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Périmètre");
        builder.AppendLine();
        builder.AppendLine(
            "Points hors de la province d'Istanbul, exclus du calcul (les lignes İETT " +
            "desservent aussi Kocaeli) :");
        builder.AppendLine();
        builder.AppendLine("| Mode | Points hors périmètre |");
        builder.AppendLine("|---|---:|");
        foreach (var item in report.OutsideProvince)
        {
            builder.AppendLine($"| {ModeLabel(item.Mode)} | {item.Count:N0} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Dédoublonnage");
        builder.AppendLine();
        builder.AppendLine("| Mode | Points bruts | Hors périmètre | Points distincts | Fusionnés |");
        builder.AppendLine("|---|---:|---:|---:|---:|");
        var excludedBus = ExcludedCount(report, "bus");
        var excludedRail = ExcludedCount(report, "rail");
        var excludedFerry = ExcludedCount(report, "ferry");
        builder.AppendLine(
            $"| Bus (İETT) | {report.BusPoints:N0} | {excludedBus:N0} | " +
            $"{report.DistinctBus:N0} | " +
            $"{report.BusPoints - excludedBus - report.DistinctBus:N0} |");
        builder.AppendLine(
            $"| Rail (Metro İstanbul) | {report.RailPoints:N0} | {excludedRail:N0} | " +
            $"{report.DistinctRail:N0} | " +
            $"{report.RailPoints - excludedRail - report.DistinctRail:N0} |");
        builder.AppendLine(
            $"| Ferry (Şehir Hatları) | {report.FerryPoints:N0} | {excludedFerry:N0} | " +
            $"{report.DistinctFerry:N0} | " +
            $"{report.FerryPoints - excludedFerry - report.DistinctFerry:N0} |");
        builder.AppendLine();
        builder.AppendLine("## Affectation aux mahalles");
        builder.AppendLine();
        builder.AppendLine("| Affectation | Points |");
        builder.AppendLine("|---|---:|");
        builder.AppendLine($"| Polygone (point couvert) | {report.PolygonAssigned:N0} |");
        builder.AppendLine($"| Mahalle le plus proche (≤ 500 m) | {report.NearestAssigned:N0} |");
        builder.AppendLine($"| Non affectés | {report.Unassigned:N0} |");
        if (report.UnassignedPoints.Count > 0)
        {
            builder.AppendLine();
            foreach (var point in report.UnassignedPoints)
            {
                builder.AppendLine($"- {Escape(point)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Couverture");
        builder.AppendLine();
        builder.AppendLine("| Contrôle | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine(
            $"| Mahalles avec valeurs | {report.NeighborhoodCount} / {report.ExpectedNeighborhoods} |");
        builder.AppendLine($"| Ilçe | {report.DistrictCount} / {report.ExpectedDistricts} |");
        builder.AppendLine();
        builder.AppendLine("## Métrique");
        builder.AppendLine();
        builder.AppendLine(
            "| Métrique | Unité | Qualité | Valeurs | Zéros | Brut min | Brut médian | " +
            "Brut max | Normalisé min | Normalisé médian | Normalisé max |");
        builder.AppendLine("|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        builder.AppendLine(
            $"| `transport_accessibility` | points pondérés/km² | ok | " +
            $"{report.Neighborhoods.Count} | " +
            $"{densities.Count(value => value == 0)} | " +
            $"{Number(densities.FirstOrDefault(), "0.#")} | {Number(Median(densities), "0.#")} | " +
            $"{Number(densities.LastOrDefault(), "0.#")} | " +
            $"{Number(normalized.FirstOrDefault(), "0.###")} | " +
            $"{Number(Median(normalized), "0.###")} | " +
            $"{Number(normalized.LastOrDefault(), "0.###")} |");
        builder.AppendLine();
        builder.AppendLine(
            $"Points distincts pris en compte : bus {report.DistinctBus:N0}, rail " +
            $"{report.DistinctRail:N0}, ferry {report.DistinctFerry:N0}. Pondérations : bus " +
            $"{TransportMetricCatalog.BusWeight}, rail {TransportMetricCatalog.RailWeight}, " +
            $"ferry {TransportMetricCatalog.FerryWeight}.");
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
                builder.AppendLine($"- {Escape(failure)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Méthode et limites");
        builder.AppendLine();
        builder.AppendLine($"- {TransportMetricCatalog.TransportAccessibility.Methodology}");
        builder.AppendLine(
            "- Écarté : tout point hors du polygone de la province d'Istanbul (arrêts İETT des " +
            "lignes qui prolongent vers Kocaeli) ; ces points ne sont rattachés à aucun mahalle et " +
            "n'entrent ni dans le dédoublonnage ni dans la densité.");
        builder.AppendLine(
            "- Un point est rattaché au premier mahalle qui le couvre (ordre des identifiants) ; " +
            "s'il n'est couvert par aucun polygone, il est rattaché au mahalle le plus proche à " +
            "moins de 500 m, puis laissé de côté et listé ci-dessus au-delà.");
        builder.AppendLine(
            "- Le dédoublonnage fusionne les deux sens d'un même arrêt de bus (rayon 60 m) et les " +
            "quais d'un même terminal de ferry (rayon 150 m) ; les noms sont normalisés en turc.");
        builder.AppendLine(
            "- Ne sont pas comptés : Marmaray et les trains TCDD, les navettes privées, les " +
            "fréquences et la qualité de service. Le métro et le tramway sont inclus via Metro " +
            "İstanbul ; les arrêts İETT viennent du GTFS publié par l'İBB.");
        builder.AppendLine(
            "- `quality_flag = ok` : c'est un comptage de points officiels, pas une estimation ; " +
            "la couverture reste partielle et le nombre d'arrêts n'est pas une mesure de fréquence.");

        return builder.ToString();
    }

    private static string BuildCsv(TransportCheckReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "district,neighborhood,bus_stops,rail_stations,ferry_piers,weighted_points," +
            "weighted_density_per_km2,normalized");
        foreach (var item in report.Neighborhoods)
        {
            builder.AppendLine(
                $"{CsvField(item.DistrictName)},{CsvField(item.NeighborhoodName)}," +
                $"{item.BusStops},{item.RailStations},{item.FerryPiers}," +
                $"{Number(item.WeightedPoints, "0")},{Number(item.Density, "0.####")}," +
                $"{Number(item.Normalized, "0.######")}");
        }

        return builder.ToString();
    }

    private static int ExcludedCount(TransportCheckReport report, string mode) =>
        report.OutsideProvince
            .Where(item => string.Equals(item.Mode, mode, StringComparison.Ordinal))
            .Sum(item => item.Count);

    private static string ModeLabel(string mode) => mode switch
    {
        "bus" => "Bus (İETT)",
        "rail" => "Rail (Metro İstanbul)",
        "ferry" => "Ferry (Şehir Hatları)",
        _ => mode,
    };

}
