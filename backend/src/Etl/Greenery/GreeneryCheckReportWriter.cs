using System.Text;
using System.Text.Json;
using static NeighbourhoodExplorer.Etl.Reports.CheckReportFormat;

namespace NeighbourhoodExplorer.Etl.Greenery;

public sealed record GreeneryNeighborhoodResult(
    string DistrictName,
    string NeighborhoodName,
    long GreenPixels,
    long ValidPixels,
    double Share,
    double Normalized);

public sealed record GreeneryCheckReport(
    string SourceId,
    string DataSourceId,
    string SourceName,
    string SourceUrl,
    string License,
    string AttributionText,
    string SourceVersion,
    string ChecksumSha256,
    string GdalVersion,
    string PostgisVersion,
    string CroppedFile,
    int RasterWidth,
    int RasterHeight,
    double PixelSizeX,
    double PixelSizeY,
    int TileCount,
    int ReferenceYear,
    string ReferencePeriod,
    DateOnly ObservedAt,
    DateTimeOffset ComputedAt,
    int NeighborhoodCount,
    int DistrictCount,
    int ExpectedNeighborhoods,
    int ExpectedDistricts,
    IReadOnlyList<GreeneryNeighborhoodResult> Neighborhoods,
    IReadOnlyList<string> Failures);

public static class GreeneryCheckReportWriter
{
    public static async Task WriteAsync(
        string dataDirectory,
        GreeneryCheckReport report,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(dataDirectory, "processed", report.SourceId);
        Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            Path.Combine(directory, "greenery-check.md"),
            BuildMarkdown(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "greenery-check.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "greenery-mahalle.csv"),
            BuildCsv(report),
            cancellationToken);
    }

    private static string BuildMarkdown(GreeneryCheckReport report)
    {
        var shares = report.Neighborhoods.Select(item => item.Share).OrderBy(value => value).ToList();
        var normalized = report.Neighborhoods
            .Select(item => item.Normalized)
            .OrderBy(value => value)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine($"# Contrôle de la verdure — {report.SourceId}");
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
        builder.AppendLine($"| SHA-256 du brut | `{report.ChecksumSha256}` |");
        builder.AppendLine($"| `observed_at` | {report.ObservedAt:yyyy-MM-dd} |");
        builder.AppendLine($"| `reference_period` | {Escape(report.ReferencePeriod)} |");
        builder.AppendLine($"| Calculé le | {report.ComputedAt:yyyy-MM-dd HH:mm:ss} UTC |");
        builder.AppendLine();
        builder.AppendLine("## Raster");
        builder.AppendLine();
        builder.AppendLine("| Champ | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| gdal | {Escape(report.GdalVersion)} |");
        builder.AppendLine($"| PostGIS | {Escape(report.PostgisVersion)} |");
        builder.AppendLine($"| Fichier découpé | `{Escape(Path.GetFileName(report.CroppedFile))}` |");
        builder.AppendLine($"| Taille | {report.RasterWidth:N0} × {report.RasterHeight:N0} pixels |");
        builder.AppendLine(
            $"| Taille du pixel | {Number(report.PixelSizeX, "0.########")} × " +
            $"{Number(Math.Abs(report.PixelSizeY), "0.########")} degrés |");
        builder.AppendLine($"| Tuiles importées | {report.TileCount:N0} |");
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
            $"| `greenery` | % de la surface | estimated | {report.Neighborhoods.Count} | " +
            $"{shares.Count(value => value == 0)} | " +
            $"{Number(shares.FirstOrDefault(), "0.#")} | {Number(Median(shares), "0.#")} | " +
            $"{Number(shares.LastOrDefault(), "0.#")} | " +
            $"{Number(normalized.FirstOrDefault(), "0.###")} | " +
            $"{Number(Median(normalized), "0.###")} | " +
            $"{Number(normalized.LastOrDefault(), "0.###")} |");
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
        builder.AppendLine($"- {GreeneryMetricCatalog.Greenery.Methodology}");
        builder.AppendLine(
            "- ESA WorldCover est une classification satellite à 10 m (2021, v200) : elle ne " +
            "distingue pas les jardins privés des parcs publics et sa précision baisse sous la " +
            "canopée ou dans les zones bâties denses.");
        builder.AppendLine(
            "- La valeur brute est une part de surface (%), la valeur normalisée est son " +
            "percentile sur les 964 mahalles. `quality_flag = estimated` : c'est une estimation " +
            "satellite, pas un inventaire.");
        builder.AppendLine(
            "- Les pixels sans donnée sont exclus du dénominateur ; un pixel est attribué au " +
            "mahalle si son centre est couvert par le polygone (comportement par défaut de " +
            "`ST_Clip`).");

        return builder.ToString();
    }

    private static string BuildCsv(GreeneryCheckReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "district,neighborhood,green_pixels,valid_pixels,green_share_percent,normalized");
        foreach (var item in report.Neighborhoods)
        {
            builder.AppendLine(
                $"{CsvField(item.DistrictName)},{CsvField(item.NeighborhoodName)}," +
                $"{item.GreenPixels},{item.ValidPixels}," +
                $"{Number(item.Share, "0.####")},{Number(item.Normalized, "0.######")}");
        }

        return builder.ToString();
    }

}
