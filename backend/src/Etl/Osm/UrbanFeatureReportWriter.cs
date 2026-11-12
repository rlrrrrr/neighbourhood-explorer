using System.Globalization;
using System.Text;
using System.Text.Json;
using NeighbourhoodExplorer.Etl.Database;
using static NeighbourhoodExplorer.Etl.Reports.CheckReportFormat;

namespace NeighbourhoodExplorer.Etl.Osm;

public sealed record UrbanFeatureMetricSummary(
    string Key,
    string Unit,
    string QualityFlag,
    int ValueCount,
    int ZeroCount,
    double MinRaw,
    double MedianRaw,
    double MaxRaw,
    double MinNormalized,
    double MedianNormalized,
    double MaxNormalized);

public sealed record UrbanFeatureReport(
    string SourceId,
    string DataSourceId,
    string SourceName,
    string Organization,
    string SourceUrl,
    string License,
    string AttributionText,
    string SourceVersion,
    string ChecksumSha256,
    string Osm2pgsqlVersion,
    string ReferencePeriod,
    DateOnly ObservedAt,
    DateTimeOffset ComputedAt,
    UrbanFeatureBoundingBox Bounds,
    int NeighborhoodCount,
    int DistrictCount,
    int ExpectedNeighborhoods,
    int ExpectedDistricts,
    UrbanFeatureStagingCounts Staging,
    IReadOnlyList<UrbanFeaturePoiCategoryTotal> PoiCategories,
    IReadOnlyList<UrbanFeatureNeighborhoodCounts> Neighborhoods,
    IReadOnlyList<MetricSeries> Metrics,
    IReadOnlyList<string> Failures);

public static class UrbanFeatureReportWriter
{
    public static async Task WriteAsync(
        string dataDirectory,
        UrbanFeatureReport report,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(dataDirectory, "processed", report.SourceId);
        Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            Path.Combine(directory, "urban-features-check.md"),
            Markdown(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "urban-features-check.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "urban-features-mahalle.csv"),
            Csv(report),
            cancellationToken);
    }

    private static string Markdown(UrbanFeatureReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Contrôle des objets urbains OSM — {report.SourceId}");
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
        builder.AppendLine($"| Organisation | {Escape(report.Organization)} |");
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
        builder.AppendLine("## Emprise et import");
        builder.AppendLine();
        builder.AppendLine("| Champ | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| osm2pgsql | {Escape(report.Osm2pgsqlVersion)} |");
        builder.AppendLine(
            $"| Emprise (min lon, min lat, max lon, max lat) | " +
            $"{Number(report.Bounds.MinLongitude, "0.######")}, " +
            $"{Number(report.Bounds.MinLatitude, "0.######")}, " +
            $"{Number(report.Bounds.MaxLongitude, "0.######")}, " +
            $"{Number(report.Bounds.MaxLatitude, "0.######")} |");
        builder.AppendLine($"| POI importés | {report.Staging.PoiRows:N0} |");
        builder.AppendLine($"| Surfaces importées | {report.Staging.AreaRows:N0} |");
        builder.AppendLine($"| Tronçons routiers importés | {report.Staging.HighwayRows:N0} |");
        builder.AppendLine();
        builder.AppendLine("## Dédoublonnage des POI");
        builder.AppendLine();
        builder.AppendLine(
            "Un même lieu peut apparaître comme nœud et comme surface. Les objets sont " +
            "dédoublonnés par catégorie, nom normalisé en turc et proximité (grille d'environ " +
            "55 m) ; les surfaces et relations sont conservées en priorité.");
        builder.AppendLine();
        builder.AppendLine("| Catégorie | Avant | Après | Supprimés |");
        builder.AppendLine("|---|---:|---:|---:|");
        foreach (var category in report.PoiCategories)
        {
            builder.AppendLine(
                $"| {Escape(category.Category)} | {category.BeforeDeduplication:N0} | " +
                $"{category.AfterDeduplication:N0} | " +
                $"{category.BeforeDeduplication - category.AfterDeduplication:N0} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Couverture");
        builder.AppendLine();
        builder.AppendLine("| Contrôle | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine(
            $"| Mahalles avec valeurs | {report.NeighborhoodCount} / {report.ExpectedNeighborhoods} |");
        builder.AppendLine($"| Ilçe | {report.DistrictCount} / {report.ExpectedDistricts} |");
        builder.AppendLine($"| Métriques | {report.Metrics.Count} |");
        builder.AppendLine();
        builder.AppendLine("## Métriques");
        builder.AppendLine();
        builder.AppendLine(
            "| Métrique | Unité | Qualité | Valeurs | Zéros | Brut min | Brut médian | " +
            "Brut max | Normalisé min | Normalisé médian | Normalisé max |");
        builder.AppendLine("|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var metric in Summarize(report.Metrics))
        {
            builder.AppendLine(
                $"| `{metric.Key}` | {Escape(metric.Unit)} | {metric.QualityFlag} | " +
                $"{metric.ValueCount} | {metric.ZeroCount} | " +
                $"{Number(metric.MinRaw, "0.###")} | {Number(metric.MedianRaw, "0.###")} | " +
                $"{Number(metric.MaxRaw, "0.###")} | " +
                $"{Number(metric.MinNormalized, "0.###")} | " +
                $"{Number(metric.MedianNormalized, "0.###")} | " +
                $"{Number(metric.MaxNormalized, "0.###")} |");
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
        foreach (var metric in report.Metrics)
        {
            builder.AppendLine($"- `{metric.Definition.Key}` — {metric.Definition.Methodology}");
        }

        builder.AppendLine();
        builder.AppendLine(
            "L'extrait OSM est filtré sur l'emprise provinciale augmentée de 0,02° afin de " +
            "conserver les objets proches des limites. Les POI sont comptés dans le polygone " +
            "administratif ; l'accessibilité éducative et sanitaire compte les établissements " +
            "dans un rayon de 800 m autour de ce polygone (distance géographique). Un carrefour " +
            "est un point dont le degré routier est d'au moins trois (un tronçon qui traverse " +
            "compte pour deux branches, un tronçon qui s'arrête pour une). La marchabilité et la " +
            "diversité d'usages sont des indices relatifs, publiés avec " +
            "`quality_flag = estimated`.");
        builder.AppendLine();
        builder.AppendLine(
            "**La complétude d'OpenStreetMap est inégale.** Les équipements sont mieux " +
            "cartographiés là où les contributeurs sont actifs ; la vie nocturne est " +
            "probablement sous-estimée ; les bâtiments souvent tagués `building=yes` ne " +
            "permettent pas de distinguer finement les usages. Ces valeurs sont des " +
            "indicateurs issus des données disponibles, jamais des recensements.");
        builder.AppendLine();
        builder.AppendLine("## Attribution");
        builder.AppendLine();
        builder.AppendLine(
            $"{report.AttributionText} — extrait fourni par {report.Organization} " +
            $"({report.SourceUrl}).");
        builder.AppendLine();
        return builder.ToString();
    }

    private static string Csv(UrbanFeatureReport report)
    {
        var builder = new StringBuilder();
        builder.Append(
            "admin_area_id,district,neighborhood,area_km2,cafe_count,restaurant_count," +
            "nightlife_count,culture_count,daily_count,education_800m_count," +
            "healthcare_800m_count,intersection_count,residential_area_count," +
            "commercial_area_count,food_area_count,culture_area_count,office_area_count," +
            "green_area_count");
        foreach (var metric in report.Metrics)
        {
            builder.Append(',');
            builder.Append(metric.Definition.Key);
            builder.Append(',');
            builder.Append(metric.Definition.Key);
            builder.Append("_normalized");
        }

        builder.AppendLine();

        var valuesByMetric = report.Metrics.ToDictionary(
            metric => metric.Definition.Key,
            metric => metric.Values.ToDictionary(value => value.AdminAreaId),
            StringComparer.Ordinal);

        foreach (var neighborhood in report.Neighborhoods
                     .OrderBy(neighborhood => neighborhood.DistrictName, StringComparer.Ordinal)
                     .ThenBy(neighborhood => neighborhood.Name, StringComparer.Ordinal))
        {
            builder.Append(neighborhood.AdminAreaId.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvField(neighborhood.DistrictName));
            builder.Append(',');
            builder.Append(CsvField(neighborhood.Name));
            builder.Append(',');
            builder.Append(Number(neighborhood.AreaKm2, "F6"));
            builder.Append(',');
            builder.Append(neighborhood.CafeCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.RestaurantCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.NightlifeCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.CultureCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.DailyCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.EducationCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.HealthcareCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.IntersectionCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.ResidentialCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.CommercialCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.FoodCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.LandUseCultureCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.OfficeCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(neighborhood.GreenCount.ToString(CultureInfo.InvariantCulture));

            foreach (var metric in report.Metrics)
            {
                var value = valuesByMetric[metric.Definition.Key][neighborhood.AdminAreaId];
                builder.Append(',');
                builder.Append(Number(value.RawValue, "F6"));
                builder.Append(',');
                builder.Append(Number(value.NormalizedValue, "F9"));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static IReadOnlyList<UrbanFeatureMetricSummary> Summarize(
        IReadOnlyList<MetricSeries> metrics) =>
        metrics.Select(metric =>
        {
            var raw = metric.Values.Select(value => value.RawValue).OrderBy(value => value).ToList();
            var normalized = metric.Values
                .Select(value => value.NormalizedValue)
                .OrderBy(value => value)
                .ToList();

            return new UrbanFeatureMetricSummary(
                metric.Definition.Key,
                metric.Definition.Unit,
                metric.Definition.QualityFlag.ToWireValue(),
                metric.Values.Count,
                metric.Values.Count(value => value.RawValue == 0),
                raw.Count == 0 ? 0 : raw[0],
                Median(raw),
                raw.Count == 0 ? 0 : raw[^1],
                normalized.Count == 0 ? 0 : normalized[0],
                Median(normalized),
                normalized.Count == 0 ? 0 : normalized[^1]);
        }).ToList();
}
