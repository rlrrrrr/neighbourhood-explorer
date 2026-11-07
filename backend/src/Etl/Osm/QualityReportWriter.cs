using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NeighbourhoodExplorer.Etl.Osm;

public static class QualityReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const double GapAlertPercent = 1;
    private const double GapAlertSquareKilometers = 1;
    private const double OverlapAlertPercent = 0.1;
    private const double OverlapAlertSquareKilometers = 0.5;

    public static async Task WriteAsync(
        string dataDirectory,
        AdminAreaQualityReport report,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(dataDirectory, "processed", report.SourceId);
        Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            Path.Combine(directory, "quality-report.md"),
            Render(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "quality-report.json"),
            JsonSerializer.Serialize(report, JsonOptions),
            cancellationToken);
    }

    private static string Render(AdminAreaQualityReport report)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"# Rapport de qualité — limites administratives ({report.SourceId})");
        builder.AppendLine();
        builder.AppendLine($"Généré le {report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)} par l'ETL.");
        builder.AppendLine();
        builder.AppendLine("| Champ | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Fichier source | `{report.RawFilePath}` |");
        builder.AppendLine($"| SHA-256 | `{report.InputSha256}` |");
        builder.AppendLine($"| osm2pgsql | {report.Osm2pgsqlVersion} |");
        builder.AppendLine($"| Province | {report.Build.ProvinceName} ({report.Build.CountryCode}) |");
        builder.AppendLine($"| Emprise (lon, lat) | {Format(report.Bounds.MinLongitude)}, {Format(report.Bounds.MinLatitude)} → {Format(report.Bounds.MaxLongitude)}, {Format(report.Bounds.MaxLatitude)} |");
        builder.AppendLine($"| Projection des surfaces | EPSG:32635 (UTM 35N) |");
        builder.AppendLine();

        builder.AppendLine("## Contrôles de nombre");
        builder.AppendLine();
        builder.AppendLine("| Niveau | Attendu | Obtenu | Résultat |");
        builder.AppendLine("|---|---:|---:|---|");
        foreach (var count in report.Counts)
        {
            builder.AppendLine($"| {count.Level} | {count.Expected} | {count.Actual} | {(count.Matches ? "conforme" : "**écart**")} |");
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
        builder.AppendLine("## Couverture des ilçe par les mahalles");
        builder.AppendLine();
        builder.AppendLine(
            $"Seuils d'alerte (signalement, pas échec) : trou > {Format(GapAlertPercent)} % ou > " +
            $"{Format(GapAlertSquareKilometers)} km² ; chevauchement > {Format(OverlapAlertPercent)} % ou > " +
            $"{Format(OverlapAlertSquareKilometers)} km². Surfaces calculées dans EPSG:32635.");
        builder.AppendLine();
        builder.AppendLine("| Ilçe | Surface (km²) | Somme mahalles | Union | Trous (km² / %) | Chevauchements (km² / %) | Débordement (km²) | Alerte |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---|");

        foreach (var row in report.DistrictCoverage)
        {
            var alert = IsAlert(row) ? "oui" : "";
            builder.AppendLine(
                $"| {row.Area} | {Format(row.AreaKm2)} | {Format(row.SumPartsKm2)} | {Format(row.UnionPartsKm2)} | " +
                $"{Format(row.GapKm2)} / {Format(row.GapPercent)} | {Format(row.OverlapKm2)} / {Format(row.OverlapPercent)} | " +
                $"{Format(row.SpillKm2)} | {alert} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Couverture de la province par les ilçe");
        builder.AppendLine();
        var province = report.ProvinceCoverage;
        builder.AppendLine(
            $"| Province | Surface (km²) | Somme ilçe | Union | Trous (km² / %) | Chevauchements (km² / %) | Débordement (km²) |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        builder.AppendLine(
            $"| {province.Area} | {Format(province.AreaKm2)} | {Format(province.SumPartsKm2)} | {Format(province.UnionPartsKm2)} | " +
            $"{Format(province.GapKm2)} / {Format(province.GapPercent)} | {Format(province.OverlapKm2)} / {Format(province.OverlapPercent)} | " +
            $"{Format(province.SpillKm2)} |");

        builder.AppendLine();
        builder.AppendLine("## Noms de mahalle hors modèle (« … Mahallesi »)");
        builder.AppendLine();
        AppendNameIssues(builder, report.Build.NamesNotMatchingPattern);

        builder.AppendLine();
        builder.AppendLine("## Noms de mahalle portés par plusieurs relations");
        builder.AppendLine();
        AppendDuplicateNames(builder, report.Build.DuplicateNames);

        builder.AppendLine();
        builder.AppendLine("## Doublons dans un même ilçe");
        builder.AppendLine();
        AppendDuplicateNames(builder, report.Build.DuplicateNamesWithinDistrict);

        builder.AppendLine();
        builder.AppendLine("## Géométries invalides corrigées (ST_MakeValid)");
        builder.AppendLine();
        if (report.Build.InvalidGeometries.Count == 0)
        {
            builder.AppendLine("Aucune.");
        }
        else
        {
            foreach (var issue in report.Build.InvalidGeometries)
            {
                builder.AppendLine($"- niveau {issue.AdminLevel}, relation {issue.RelationId} : {issue.Name}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Mahalles sans ilçe parent");
        builder.AppendLine();
        if (report.Build.UnassignedNeighborhoods.Count == 0)
        {
            builder.AppendLine("Aucun.");
        }
        else
        {
            foreach (var unassigned in report.Build.UnassignedNeighborhoods)
            {
                builder.AppendLine($"- {unassigned.Name} (relation {unassigned.RelationId})");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Mahalles touchant plusieurs ilçe (parent choisi par point intérieur, puis surface d'intersection)");
        builder.AppendLine();
        if (report.Build.AmbiguousNeighborhoods.Count == 0)
        {
            builder.AppendLine("Aucun.");
        }
        else
        {
            foreach (var ambiguous in report.Build.AmbiguousNeighborhoods)
            {
                builder.AppendLine(
                    $"- {ambiguous.Name} (relation {ambiguous.RelationId}) : {string.Join(", ", ambiguous.Districts)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Zones de niveau 6 ou 8 qui touchent la province sans être dedans");
        builder.AppendLine();
        if (report.Build.OutsideProvinceAreas.Count == 0)
        {
            builder.AppendLine("Aucune.");
        }
        else
        {
            foreach (var outside in report.Build.OutsideProvinceAreas)
            {
                builder.AppendLine($"- niveau {outside.AdminLevel}, relation {outside.RelationId} : {outside.Name}");
            }
        }

        return builder.ToString();
    }

    private static void AppendNameIssues(StringBuilder builder, IReadOnlyList<NameIssue> issues)
    {
        if (issues.Count == 0)
        {
            builder.AppendLine("Aucun.");
            return;
        }

        builder.AppendLine("| Ilçe | Nom OSM | Relation |");
        builder.AppendLine("|---|---|---|");
        foreach (var issue in issues)
        {
            builder.AppendLine($"| {issue.District} | {issue.Name} | {issue.RelationId} |");
        }
    }

    private static void AppendDuplicateNames(StringBuilder builder, IReadOnlyList<DuplicateName> duplicates)
    {
        if (duplicates.Count == 0)
        {
            builder.AppendLine("Aucun.");
            return;
        }

        builder.AppendLine("| Nom | Occurrences | Ilçe |");
        builder.AppendLine("|---|---:|---|");
        foreach (var duplicate in duplicates)
        {
            builder.AppendLine($"| {duplicate.Name} | {duplicate.Count} | {string.Join(", ", duplicate.Districts)} |");
        }
    }

    private static bool IsAlert(CoverageRow row) =>
        row.GapPercent > GapAlertPercent
        || row.GapKm2 > GapAlertSquareKilometers
        || row.OverlapPercent > OverlapAlertPercent
        || row.OverlapKm2 > OverlapAlertSquareKilometers;

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
