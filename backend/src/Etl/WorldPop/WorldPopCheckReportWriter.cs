using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NeighbourhoodExplorer.Etl.WorldPop;

public sealed record WorldPopCheckReport(
    string SourceId,
    string SourceName,
    string SourceUrl,
    string License,
    string AttributionText,
    string SourceVersion,
    string ChecksumSha256,
    string GdalVersion,
    string PostgisVersion,
    int GridYear,
    int ReferenceYear,
    DateOnly ObservedAt,
    string ReferencePeriod,
    string RawFilePath,
    string ProjectedFilePath,
    int RasterWidth,
    int RasterHeight,
    int RasterSrid,
    double PixelSizeX,
    double PixelSizeY,
    double NoDataValue,
    double RasterMinimum,
    double RasterMaximum,
    double RasterMean,
    double RasterValidPercent,
    long RasterValidCells,
    int RasterTileCount,
    double RasterTotalPopulation,
    int NeighborhoodCount,
    int DistrictCount,
    double TotalRaw,
    int TotalAdnks,
    double TotalEstimated,
    int FlaggedCoefficientCount,
    int ZeroNeighborhoodCount,
    double MinDensity,
    double MaxDensity,
    double MedianDensity,
    long MetricId,
    string MetricKey,
    DateTimeOffset ComputedAt,
    IReadOnlyList<WorldPopCoefficient> Coefficients,
    IReadOnlyList<WorldPopNeighborhoodResult> Neighborhoods,
    IReadOnlyList<WorldPopZeroNeighborhood> ZeroNeighborhoods,
    IReadOnlyList<string> UnmatchedDistricts,
    IReadOnlyList<string> Failures);

public static class WorldPopCheckReportWriter
{
    public static async Task WriteAsync(
        string dataDirectory,
        WorldPopCheckReport report,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(dataDirectory, "processed", report.SourceId);
        Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            Path.Combine(directory, "worldpop-check.md"),
            Markdown(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "worldpop-check.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "population-density-mahalle.csv"),
            Csv(report),
            cancellationToken);
    }

    private static string Markdown(WorldPopCheckReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Contrôle WorldPop — {report.SourceId}");
        builder.AppendLine();
        builder.AppendLine(
            report.Failures.Count == 0
                ? "**Résultat : conforme.**"
                : $"**Résultat : {report.Failures.Count} échec(s).**");
        builder.AppendLine();
        builder.AppendLine("## Source");
        builder.AppendLine();
        builder.AppendLine("| Champ | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Jeu | {Escape(report.SourceName)} |");
        builder.AppendLine($"| Organisation | WorldPop, University of Southampton |");
        builder.AppendLine($"| URL | {report.SourceUrl} |");
        builder.AppendLine($"| Version | {Escape(report.SourceVersion)} |");
        builder.AppendLine($"| Licence | {Escape(report.License)} |");
        builder.AppendLine($"| Attribution | {Escape(report.AttributionText)} |");
        builder.AppendLine($"| SHA-256 du brut | `{report.ChecksumSha256}` |");
        builder.AppendLine($"| Année de la grille | {report.GridYear} |");
        builder.AppendLine($"| Référence ADNKS | {report.ReferenceYear} |");
        builder.AppendLine($"| `observed_at` | {report.ObservedAt:yyyy-MM-dd} |");
        builder.AppendLine($"| `reference_period` | {Escape(report.ReferencePeriod)} |");
        builder.AppendLine();
        builder.AppendLine("## Raster");
        builder.AppendLine();
        builder.AppendLine("| Champ | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Fichier brut | `{report.RawFilePath}` |");
        builder.AppendLine($"| Fichier projeté | `{report.ProjectedFilePath}` |");
        builder.AppendLine($"| GDAL | {Escape(report.GdalVersion)} |");
        builder.AppendLine($"| PostGIS | {Escape(report.PostgisVersion)} |");
        builder.AppendLine($"| Taille | {report.RasterWidth} × {report.RasterHeight} cases |");
        builder.AppendLine($"| CRS | EPSG:{report.RasterSrid} |");
        builder.AppendLine(
            $"| Taille de case | {Number(report.PixelSizeX, "0.###")} × " +
            $"{Number(Math.Abs(report.PixelSizeY), "0.###")} m |");
        builder.AppendLine($"| Nodata | {Number(report.NoDataValue, "0.###")} |");
        builder.AppendLine(
            $"| Valeurs | min {Number(report.RasterMinimum, "0.###")}, " +
            $"max {Number(report.RasterMaximum, "0.###")}, " +
            $"moyenne {Number(report.RasterMean, "0.###")} |");
        builder.AppendLine(
            $"| Cases valides | {report.RasterValidCells:N0} " +
            $"({Number(report.RasterValidPercent, "0.##")} %) |");
        builder.AppendLine($"| Tuiles PostGIS | {report.RasterTileCount} |");
        builder.AppendLine(
            $"| Somme du raster sur l'emprise | {Number(report.RasterTotalPopulation, "F3")} |");
        builder.AppendLine();
        builder.AppendLine("## Contrôles");
        builder.AppendLine();
        builder.AppendLine("| Contrôle | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Mahalles | {report.NeighborhoodCount} |");
        builder.AppendLine($"| Ilçe | {report.DistrictCount} |");
        builder.AppendLine(
            $"| Somme WorldPop brute | {Number(report.TotalRaw, "F3")} |");
        builder.AppendLine($"| Somme ADNKS | {report.TotalAdnks:N0} |");
        builder.AppendLine(
            $"| Somme estimée | {Number(report.TotalEstimated, "F3")} |");
        builder.AppendLine(
            $"| Écart | {Number(report.TotalEstimated - report.TotalAdnks, "F3")} |");
        builder.AppendLine(
            $"| Coefficients hors 0,7–1,3 | {report.FlaggedCoefficientCount} |");
        builder.AppendLine($"| Mahalles à somme nulle | {report.ZeroNeighborhoodCount} |");
        builder.AppendLine(
            $"| Densité (personnes/km²) | min {Number(report.MinDensity, "N0")}, " +
            $"médiane {Number(report.MedianDensity, "N0")}, " +
            $"max {Number(report.MaxDensity, "N0")} |");
        builder.AppendLine($"| Métrique | `{report.MetricKey}` (id {report.MetricId}) |");
        builder.AppendLine();
        builder.AppendLine("## Coefficients par ilçe");
        builder.AppendLine();
        builder.AppendLine(
            "Coefficient = total ADNKS de l'ilçe ÷ somme WorldPop de ses mahalles. " +
            "Il est conservé pour audit ; un coefficient hors de 0,7 à 1,3 est signalé.");
        builder.AppendLine();
        builder.AppendLine(
            "| Ilçe | Code TÜİK | Somme WorldPop | ADNKS | Coefficient | Signalé | Mahalles | Somme nulle |");
        builder.AppendLine("|---|---|---:|---:|---:|---|---:|---:|");
        foreach (var coefficient in report.Coefficients.OrderBy(item => item.DistrictName))
        {
            builder.AppendLine(
                $"| {Escape(coefficient.DistrictName)} | {coefficient.TuikCode} | " +
                $"{Number(coefficient.WorldpopSum, "F3")} | {coefficient.AdnksTotal:N0} | " +
                $"{Number(coefficient.Coefficient, "F4")} | " +
                $"{(coefficient.Flagged ? "oui" : "non")} | " +
                $"{coefficient.NeighborhoodCount} | {coefficient.ZeroNeighborhoodCount} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Mahalles à somme WorldPop nulle");
        builder.AppendLine();
        if (report.ZeroNeighborhoods.Count == 0)
        {
            builder.AppendLine("Aucun.");
        }
        else
        {
            builder.AppendLine("| Ilçe | Mahalle | Surface (km²) |");
            builder.AppendLine("|---|---|---:|");
            foreach (var zero in report.ZeroNeighborhoods)
            {
                builder.AppendLine(
                    $"| {Escape(zero.DistrictName)} | {Escape(zero.NeighborhoodName)} | " +
                    $"{Number(zero.AreaKm2, "F3")} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Correspondances TÜİK / OSM");
        builder.AppendLine();
        if (report.UnmatchedDistricts.Count == 0)
        {
            builder.AppendLine("Les 39 ilçe TÜİK sont appariés aux 39 ilçe OSM par nom normalisé.");
        }
        else
        {
            foreach (var unmatched in report.UnmatchedDistricts)
            {
                builder.AppendLine($"- {Escape(unmatched)}");
            }
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
                builder.AppendLine($"- {Escape(failure)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Méthode et limites");
        builder.AppendLine();
        builder.AppendLine(
            "La grille WorldPop 2025 (100 m, variante contrainte aux bâtiments) est " +
            "projetée en EPSG:32635 avec `gdalwarp -r sum`, puis importée dans PostGIS raster. " +
            "Pour chaque mahalle, les cases entièrement couvertes sont sommées directement ; " +
            "les cases traversées par la limite sont corrigées par la fraction de surface " +
            "recouverte, dans une bande de 150 m autour de la limite. La somme par ilçe est " +
            "recalée sur le total ADNKS 2025 : coefficient = ADNKS ÷ somme WorldPop, appliqué " +
            "à chaque mahalle. La densité est la population estimée divisée par la surface " +
            "du polygone administratif (EPSG:32635).");
        builder.AppendLine();
        builder.AppendLine(
            "**C'est une estimation, pas un recensement.** WorldPop est un modèle ; le " +
            "recalage ne crée pas de précision au niveau mahalle. La précision est plus " +
            "faible pour les petits mahalles et pour ceux dont la somme WorldPop est nulle. " +
            "La valeur est publiée avec `quality_flag = estimated`, l'année de la grille et " +
            "l'année du total ADNKS. Si TÜİK publie la population réelle par mahalle, elle " +
            "remplacera cette estimation et l'ancienne valeur restera en historique.");
        builder.AppendLine();
        builder.AppendLine("## Attribution");
        builder.AppendLine();
        builder.AppendLine(
            "WorldPop, University of Southampton (2025), *Constrained estimates of 2015-2030 " +
            "total number of people per grid square*, R2025A v1, DOI " +
            "10.5258/SOTON/WP00839, licence CC BY 4.0. Les valeurs ont été modifiées " +
            "(recalées sur les totaux ADNKS 2025 par ilçe).");
        builder.AppendLine();
        return builder.ToString();
    }

    private static string Csv(WorldPopCheckReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "admin_area_id,district,neighborhood,worldpop_raw,area_km2,coefficient," +
            "population_estimated,population_density,normalized,quality_flag");
        foreach (var neighborhood in report.Neighborhoods)
        {
            builder.Append(neighborhood.AdminAreaId.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvField(neighborhood.DistrictName));
            builder.Append(',');
            builder.Append(CsvField(neighborhood.NeighborhoodName));
            builder.Append(',');
            builder.Append(Number(neighborhood.PopulationRaw, "F6"));
            builder.Append(',');
            builder.Append(Number(neighborhood.AreaKm2, "F6"));
            builder.Append(',');
            builder.Append(Number(neighborhood.Coefficient, "F9"));
            builder.Append(',');
            builder.Append(Number(neighborhood.PopulationEstimated, "F6"));
            builder.Append(',');
            builder.Append(Number(neighborhood.Density, "F6"));
            builder.Append(',');
            builder.Append(Number(neighborhood.Normalized, "F9"));
            builder.Append(',');
            builder.Append(neighborhood.QualityFlag);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string CsvField(string value)
    {
        if (value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }

    private static string Number(double value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal);
}
