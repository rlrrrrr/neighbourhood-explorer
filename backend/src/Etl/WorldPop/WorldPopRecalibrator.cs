using System.Globalization;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.WorldPop;

public sealed record WorldPopCoefficient(
    long DistrictId,
    string DistrictName,
    string TuikCode,
    string TuikName,
    double WorldpopSum,
    int AdnksTotal,
    double Coefficient,
    bool Flagged,
    int NeighborhoodCount,
    int ZeroNeighborhoodCount);

public sealed record WorldPopNeighborhoodResult(
    long AdminAreaId,
    string DistrictName,
    string NeighborhoodName,
    double PopulationRaw,
    double AreaKm2,
    double Coefficient,
    double PopulationEstimated,
    double Density,
    double Normalized,
    string QualityFlag);

public sealed record WorldPopMetricTranslation(
    string Locale,
    string Name,
    string Description,
    string Subject);

public sealed record WorldPopMetricMetadata(
    string SourceId,
    string SourceName,
    string Organization,
    string Url,
    string License,
    string AttributionText,
    string Methodology,
    DateOnly RetrievedAt,
    string SourceVersion,
    string MetricKey,
    string Category,
    string Unit,
    string NormalizationMethod,
    string NormalizationVersion,
    int DisplayPrecision,
    string ReferencePeriod,
    DateOnly ObservedAt,
    IReadOnlyList<WorldPopMetricTranslation> Translations);

public sealed record WorldPopRecalibrationResult(
    int DistrictCount,
    int NeighborhoodCount,
    double TotalRaw,
    int TotalAdnks,
    double TotalEstimated,
    int FlaggedCount,
    IReadOnlyList<WorldPopCoefficient> Coefficients,
    IReadOnlyList<WorldPopNeighborhoodResult> Neighborhoods,
    IReadOnlyList<string> UnmatchedDistricts,
    IReadOnlyList<string> Failures,
    long MetricId,
    double MinDensity,
    double MaxDensity,
    double MedianDensity);

public sealed record WorldPopRecalibrationRequest(
    NpgsqlConnection Connection,
    NpgsqlTransaction Transaction,
    string SourceId,
    int GridYear,
    int ReferenceYear,
    int ExpectedNeighborhoods,
    int ExpectedDistricts,
    int ExpectedProvincePopulation,
    WorldPopMetricMetadata Metadata);

public static class WorldPopRecalibrator
{
    private const double MinimumCoefficient = 0.7;
    private const double MaximumCoefficient = 1.3;

    public static async Task<WorldPopRecalibrationResult> RecalculateAsync(
        WorldPopRecalibrationRequest request,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var unmatched = new List<string>();

        var tuikSourceId = await ReadTuikSourceAsync(request, failures, cancellationToken);
        var tuikDistricts = tuikSourceId is null
            ? []
            : await ReadTuikDistrictsAsync(request, tuikSourceId, failures, cancellationToken);
        var tuikProvince = tuikSourceId is null
            ? null
            : await ReadTuikProvinceAsync(request, tuikSourceId, failures, cancellationToken);
        var osmDistricts = await ReadOsmDistrictsAsync(request, failures, cancellationToken);
        var worldpopSums = await ReadWorldpopDistrictSumsAsync(request, cancellationToken);
        var totalRaw = await ReadTotalRawAsync(request, cancellationToken);

        if (tuikDistricts.Count != request.ExpectedDistricts)
        {
            failures.Add(
                $"TÜİK : {tuikDistricts.Count} ilçe au lieu de {request.ExpectedDistricts}.");
        }

        if (osmDistricts.Count != request.ExpectedDistricts)
        {
            failures.Add(
                $"OSM : {osmDistricts.Count} ilçe au lieu de {request.ExpectedDistricts}.");
        }

        if (tuikProvince is null)
        {
            failures.Add("TÜİK : total provincial absent.");
        }
        else
        {
            var districtSum = tuikDistricts.Sum(district => (long)district.Population);
            if (districtSum != tuikProvince.Population)
            {
                failures.Add(
                    $"TÜİK : somme des ilçe {districtSum} ≠ total provincial " +
                    $"{tuikProvince.Population}.");
            }

            if (tuikProvince.Population != request.ExpectedProvincePopulation)
            {
                failures.Add(
                    $"TÜİK : population provinciale {tuikProvince.Population} ≠ attendu " +
                    $"{request.ExpectedProvincePopulation}.");
            }
        }

        var osmByName = new Dictionary<string, OsmDistrict>(StringComparer.Ordinal);
        foreach (var district in osmDistricts)
        {
            if (!osmByName.TryAdd(district.NameNormalized, district))
            {
                failures.Add($"OSM : nom d'ilçe normalisé en double « {district.NameNormalized} ».");
            }
        }

        var matchedOsmIds = new HashSet<long>();
        var coefficients = new List<WorldPopCoefficient>();
        foreach (var tuikDistrict in tuikDistricts)
        {
            if (!osmByName.TryGetValue(tuikDistrict.NameNormalized, out var osmDistrict))
            {
                unmatched.Add($"TÜİK {tuikDistrict.Code} {tuikDistrict.Name} sans ilçe OSM.");
                continue;
            }

            matchedOsmIds.Add(osmDistrict.Id);
            if (!worldpopSums.TryGetValue(osmDistrict.Id, out var worldpop))
            {
                failures.Add($"WorldPop : aucune somme pour l'ilçe {osmDistrict.Name}.");
                continue;
            }

            if (worldpop.Sum <= 0)
            {
                failures.Add(
                    $"WorldPop : somme nulle pour l'ilçe {osmDistrict.Name} " +
                    $"({worldpop.NeighborhoodCount} mahalles) ; recalage impossible.");
                continue;
            }

            var coefficient = tuikDistrict.Population / worldpop.Sum;
            coefficients.Add(new WorldPopCoefficient(
                osmDistrict.Id,
                osmDistrict.Name,
                tuikDistrict.Code,
                tuikDistrict.Name,
                worldpop.Sum,
                tuikDistrict.Population,
                coefficient,
                coefficient < MinimumCoefficient || coefficient > MaximumCoefficient,
                worldpop.NeighborhoodCount,
                worldpop.ZeroNeighborhoodCount));
        }

        foreach (var osmDistrict in osmDistricts)
        {
            if (!matchedOsmIds.Contains(osmDistrict.Id))
            {
                unmatched.Add($"OSM {osmDistrict.Name} sans ilçe TÜİK.");
            }
        }

        if (unmatched.Count > 0)
        {
            failures.Add($"{unmatched.Count} ilçe(s) sans correspondance TÜİK/OSM.");
        }

        if (failures.Count > 0)
        {
            return BuildFailureResult(request, coefficients, unmatched, failures, totalRaw);
        }

        await ReplaceCoefficientsAsync(request, coefficients, cancellationToken);
        await UpdateEstimatedPopulationsAsync(request, cancellationToken);
        await CheckDistrictSumsAsync(request, coefficients, failures, cancellationToken);

        if (failures.Count > 0)
        {
            return BuildFailureResult(request, coefficients, unmatched, failures, totalRaw);
        }

        var metricId = await UpsertMetricAsync(request, cancellationToken);
        var neighborhoodDensities = await ReadDensitiesAsync(request, cancellationToken);
        await InsertMetricValuesAsync(request, metricId, neighborhoodDensities, cancellationToken);
        var neighborhoods = await ReadNeighborhoodResultsAsync(request, metricId, cancellationToken);
        var totalEstimated = neighborhoods.Sum(neighborhood => neighborhood.PopulationEstimated);
        var totalAdnks = coefficients.Sum(coefficient => coefficient.AdnksTotal);

        if (neighborhoods.Count != request.ExpectedNeighborhoods)
        {
            failures.Add(
                $"métrique : {neighborhoods.Count} valeurs de mahalle au lieu de " +
                $"{request.ExpectedNeighborhoods}.");
        }

        if (Math.Abs(totalEstimated - totalAdnks) > 0.5)
        {
            failures.Add(
                $"somme des populations estimées {totalEstimated:F3} ≠ total ADNKS " +
                $"{totalAdnks}.");
        }

        var densities = neighborhoods
            .Select(neighborhood => neighborhood.Density)
            .OrderBy(density => density)
            .ToList();

        return new WorldPopRecalibrationResult(
            coefficients.Count,
            neighborhoods.Count,
            totalRaw,
            totalAdnks,
            totalEstimated,
            coefficients.Count(coefficient => coefficient.Flagged),
            coefficients,
            neighborhoods,
            unmatched,
            failures,
            metricId,
            densities.Count == 0 ? 0 : densities[0],
            densities.Count == 0 ? 0 : densities[^1],
            Median(densities));
    }

    private static WorldPopRecalibrationResult BuildFailureResult(
        WorldPopRecalibrationRequest request,
        IReadOnlyList<WorldPopCoefficient> coefficients,
        IReadOnlyList<string> unmatched,
        IReadOnlyList<string> failures,
        double totalRaw) =>
        new(
            coefficients.Count,
            0,
            totalRaw,
            coefficients.Sum(coefficient => coefficient.AdnksTotal),
            0,
            coefficients.Count(coefficient => coefficient.Flagged),
            coefficients,
            [],
            unmatched,
            failures,
            0,
            0,
            0,
            0);

    private static async Task<string?> ReadTuikSourceAsync(
        WorldPopRecalibrationRequest request,
        List<string> failures,
        CancellationToken cancellationToken)
    {
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            SELECT DISTINCT source_id
            FROM tuik_adnks_totals
            WHERE reference_year = @reference_year
            ORDER BY source_id
            """;
        command.Parameters.AddWithValue("reference_year", request.ReferenceYear);

        var sources = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sources.Add(reader.GetString(0));
        }

        if (sources.Count == 0)
        {
            failures.Add(
                $"TÜİK : aucun total ADNKS pour {request.ReferenceYear} ; " +
                "lancer le pipeline tuik-adnks avant le recalage.");
            return null;
        }

        if (sources.Count > 1)
        {
            failures.Add(
                $"TÜİK : plusieurs sources ADNKS pour {request.ReferenceYear} " +
                $"({string.Join(", ", sources)}) ; source ambiguë.");
            return null;
        }

        return sources[0];
    }

    private static async Task<IReadOnlyList<TuikDistrict>> ReadTuikDistrictsAsync(
        WorldPopRecalibrationRequest request,
        string tuikSourceId,
        List<string> failures,
        CancellationToken cancellationToken)
    {
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            SELECT code, name, name_normalized, population
            FROM tuik_adnks_totals
            WHERE source_id = @source_id
              AND reference_year = @reference_year
              AND level = 'district'
            ORDER BY code
            """;
        command.Parameters.AddWithValue("source_id", tuikSourceId);
        command.Parameters.AddWithValue("reference_year", request.ReferenceYear);

        var districts = new List<TuikDistrict>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            districts.Add(new TuikDistrict(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)));
        }

        if (districts.Count == 0)
        {
            failures.Add($"TÜİK : aucun ilçe pour {request.ReferenceYear}.");
        }

        return districts;
    }

    private static async Task<TuikProvince?> ReadTuikProvinceAsync(
        WorldPopRecalibrationRequest request,
        string tuikSourceId,
        List<string> failures,
        CancellationToken cancellationToken)
    {
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            SELECT code, name, population
            FROM tuik_adnks_totals
            WHERE source_id = @source_id
              AND reference_year = @reference_year
              AND level = 'province'
            ORDER BY code
            """;
        command.Parameters.AddWithValue("source_id", tuikSourceId);
        command.Parameters.AddWithValue("reference_year", request.ReferenceYear);

        var provinces = new List<TuikProvince>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            provinces.Add(new TuikProvince(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2)));
        }

        if (provinces.Count != 1)
        {
            failures.Add($"TÜİK : {provinces.Count} total provincial au lieu de 1.");
            return null;
        }

        return provinces[0];
    }

    private static async Task<IReadOnlyList<OsmDistrict>> ReadOsmDistrictsAsync(
        WorldPopRecalibrationRequest request,
        List<string> failures,
        CancellationToken cancellationToken)
    {
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            SELECT id, name, name_normalized
            FROM admin_area
            WHERE level = 'district'
            ORDER BY id
            """;

        var districts = new List<OsmDistrict>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            districts.Add(new OsmDistrict(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        if (districts.Count == 0)
        {
            failures.Add("OSM : aucun ilçe ; lancer le pipeline osm-admin-boundaries avant le recalage.");
        }

        return districts;
    }

    private static async Task<IReadOnlyDictionary<long, WorldpopDistrictSum>> ReadWorldpopDistrictSumsAsync(
        WorldPopRecalibrationRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            SELECT d.id,
                   sum(s.population_raw),
                   count(*)::int,
                   count(*) FILTER (WHERE s.population_raw = 0)::int
            FROM worldpop_neighborhood_sum s
            JOIN admin_area n ON n.id = s.admin_area_id AND n.level = 'neighborhood'
            JOIN admin_area d ON d.id = n.parent_id AND d.level = 'district'
            WHERE s.source_id = @source_id AND s.grid_year = @grid_year
            GROUP BY d.id
            """;
        command.Parameters.AddWithValue("source_id", request.SourceId);
        command.Parameters.AddWithValue("grid_year", request.GridYear);

        var sums = new Dictionary<long, WorldpopDistrictSum>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sums[reader.GetInt64(0)] = new WorldpopDistrictSum(
                reader.GetDouble(1),
                reader.GetInt32(2),
                reader.GetInt32(3));
        }

        return sums;
    }

    private static async Task<double> ReadTotalRawAsync(
        WorldPopRecalibrationRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            SELECT coalesce(sum(population_raw), 0)
            FROM worldpop_neighborhood_sum
            WHERE source_id = @source_id AND grid_year = @grid_year
            """;
        command.Parameters.AddWithValue("source_id", request.SourceId);
        command.Parameters.AddWithValue("grid_year", request.GridYear);
        return (double)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task ReplaceCoefficientsAsync(
        WorldPopRecalibrationRequest request,
        IReadOnlyList<WorldPopCoefficient> coefficients,
        CancellationToken cancellationToken)
    {
        await using (var delete = request.Connection.CreateCommand())
        {
            delete.Transaction = request.Transaction;
            delete.CommandText = """
                DELETE FROM worldpop_district_coefficient
                WHERE source_id = @source_id
                  AND grid_year = @grid_year
                  AND reference_year = @reference_year
                """;
            delete.Parameters.AddWithValue("source_id", request.SourceId);
            delete.Parameters.AddWithValue("grid_year", request.GridYear);
            delete.Parameters.AddWithValue("reference_year", request.ReferenceYear);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = """
            INSERT INTO worldpop_district_coefficient
                (source_id, grid_year, reference_year, district_id, worldpop_sum,
                 adnks_total, coefficient, flagged, computed_at)
            VALUES
                (@source_id, @grid_year, @reference_year, @district_id, @worldpop_sum,
                 @adnks_total, @coefficient, @flagged, @computed_at)
            """;

        foreach (var coefficient in coefficients)
        {
            await using var insert = request.Connection.CreateCommand();
            insert.Transaction = request.Transaction;
            insert.CommandText = insertSql;
            insert.Parameters.AddWithValue("source_id", request.SourceId);
            insert.Parameters.AddWithValue("grid_year", request.GridYear);
            insert.Parameters.AddWithValue("reference_year", request.ReferenceYear);
            insert.Parameters.AddWithValue("district_id", coefficient.DistrictId);
            insert.Parameters.AddWithValue("worldpop_sum", coefficient.WorldpopSum);
            insert.Parameters.AddWithValue("adnks_total", coefficient.AdnksTotal);
            insert.Parameters.AddWithValue("coefficient", coefficient.Coefficient);
            insert.Parameters.AddWithValue("flagged", coefficient.Flagged);
            insert.Parameters.AddWithValue("computed_at", DateTimeOffset.UtcNow);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpdateEstimatedPopulationsAsync(
        WorldPopRecalibrationRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            UPDATE worldpop_neighborhood_sum s
            SET population_estimated = s.population_raw * c.coefficient
            FROM worldpop_district_coefficient c, admin_area n
            WHERE s.admin_area_id = n.id
              AND n.parent_id = c.district_id
              AND s.source_id = @source_id
              AND s.grid_year = @grid_year
              AND c.source_id = @source_id
              AND c.grid_year = @grid_year
              AND c.reference_year = @reference_year
            """;
        command.Parameters.AddWithValue("source_id", request.SourceId);
        command.Parameters.AddWithValue("grid_year", request.GridYear);
        command.Parameters.AddWithValue("reference_year", request.ReferenceYear);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CheckDistrictSumsAsync(
        WorldPopRecalibrationRequest request,
        IReadOnlyList<WorldPopCoefficient> coefficients,
        List<string> failures,
        CancellationToken cancellationToken)
    {
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            SELECT c.district_id, c.adnks_total, coalesce(sum(s.population_estimated), 0)
            FROM worldpop_district_coefficient c
            LEFT JOIN admin_area n
                ON n.parent_id = c.district_id AND n.level = 'neighborhood'
            LEFT JOIN worldpop_neighborhood_sum s
                ON s.admin_area_id = n.id
               AND s.source_id = c.source_id
               AND s.grid_year = c.grid_year
            WHERE c.source_id = @source_id
              AND c.grid_year = @grid_year
              AND c.reference_year = @reference_year
            GROUP BY c.district_id, c.adnks_total
            """;
        command.Parameters.AddWithValue("source_id", request.SourceId);
        command.Parameters.AddWithValue("grid_year", request.GridYear);
        command.Parameters.AddWithValue("reference_year", request.ReferenceYear);

        var coefficientByDistrict = coefficients.ToDictionary(
            coefficient => coefficient.DistrictId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var districtId = reader.GetInt64(0);
            var adnksTotal = reader.GetInt32(1);
            var estimated = reader.GetDouble(2);
            if (Math.Abs(estimated - adnksTotal) > 0.5)
            {
                var name = coefficientByDistrict.TryGetValue(districtId, out var coefficient)
                    ? coefficient.DistrictName
                    : districtId.ToString(CultureInfo.InvariantCulture);
                failures.Add(
                    $"recalage : {name} estime {estimated:F3} ≠ ADNKS {adnksTotal}.");
            }
        }
    }

    private static async Task<long> UpsertMetricAsync(
        WorldPopRecalibrationRequest request,
        CancellationToken cancellationToken)
    {
        var metadata = request.Metadata;
        await using (var source = request.Connection.CreateCommand())
        {
            source.Transaction = request.Transaction;
            source.CommandText = """
                INSERT INTO data_source
                    (id, name, organization, url, license, attribution_text, methodology,
                     retrieved_at, source_version)
                VALUES
                    (@id, @name, @organization, @url, @license, @attribution_text, @methodology,
                     @retrieved_at, @source_version)
                ON CONFLICT (id) DO UPDATE SET
                    name = EXCLUDED.name,
                    organization = EXCLUDED.organization,
                    url = EXCLUDED.url,
                    license = EXCLUDED.license,
                    attribution_text = EXCLUDED.attribution_text,
                    methodology = EXCLUDED.methodology,
                    retrieved_at = EXCLUDED.retrieved_at,
                    source_version = EXCLUDED.source_version
                """;
            source.Parameters.AddWithValue("id", metadata.SourceId);
            source.Parameters.AddWithValue("name", metadata.SourceName);
            source.Parameters.AddWithValue("organization", metadata.Organization);
            source.Parameters.AddWithValue("url", metadata.Url);
            source.Parameters.AddWithValue("license", metadata.License);
            source.Parameters.AddWithValue("attribution_text", metadata.AttributionText);
            source.Parameters.AddWithValue("methodology", metadata.Methodology);
            source.Parameters.AddWithValue("retrieved_at", metadata.RetrievedAt);
            source.Parameters.AddWithValue("source_version", metadata.SourceVersion);
            await source.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var metric = request.Connection.CreateCommand();
        metric.Transaction = request.Transaction;
        metric.CommandText = """
            INSERT INTO metric_definition
                (key, category, unit, native_level, polarity, normalization_method,
                 normalization_version, display_precision, source_id)
            VALUES
                (@key, @category, @unit, 'neighborhood', 'neutral', @normalization_method,
                 @normalization_version, @display_precision, @source_id)
            ON CONFLICT (key) DO UPDATE SET
                category = EXCLUDED.category,
                unit = EXCLUDED.unit,
                native_level = EXCLUDED.native_level,
                polarity = EXCLUDED.polarity,
                normalization_method = EXCLUDED.normalization_method,
                normalization_version = EXCLUDED.normalization_version,
                display_precision = EXCLUDED.display_precision,
                source_id = EXCLUDED.source_id
            RETURNING id
            """;
        metric.Parameters.AddWithValue("key", metadata.MetricKey);
        metric.Parameters.AddWithValue("category", metadata.Category);
        metric.Parameters.AddWithValue("unit", metadata.Unit);
        metric.Parameters.AddWithValue("normalization_method", metadata.NormalizationMethod);
        metric.Parameters.AddWithValue("normalization_version", metadata.NormalizationVersion);
        metric.Parameters.AddWithValue("display_precision", metadata.DisplayPrecision);
        metric.Parameters.AddWithValue("source_id", metadata.SourceId);
        var metricId = (long)(await metric.ExecuteScalarAsync(cancellationToken))!;

        foreach (var translation in metadata.Translations)
        {
            await using var command = request.Connection.CreateCommand();
            command.Transaction = request.Transaction;
            command.CommandText = """
                INSERT INTO metric_translation
                    (metric_id, locale, name, description, subject)
                VALUES
                    (@metric_id, @locale, @name, @description, @subject)
                ON CONFLICT (metric_id, locale) DO UPDATE SET
                    name = EXCLUDED.name,
                    description = EXCLUDED.description,
                    subject = EXCLUDED.subject
                """;
            command.Parameters.AddWithValue("metric_id", metricId);
            command.Parameters.AddWithValue("locale", translation.Locale);
            command.Parameters.AddWithValue("name", translation.Name);
            command.Parameters.AddWithValue("description", translation.Description);
            command.Parameters.AddWithValue("subject", translation.Subject);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return metricId;
    }

    private static async Task InsertMetricValuesAsync(
        WorldPopRecalibrationRequest request,
        long metricId,
        IReadOnlyList<NeighborhoodDensity> densities,
        CancellationToken cancellationToken)
    {
        if (densities.Count == 0)
        {
            return;
        }

        var metadata = request.Metadata;
        var normalized = PercentileRank.Normalize(
            densities.Select(density => density.Density).ToList());

        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            INSERT INTO metric_value
                (admin_area_id, metric_id, observed_at, reference_period, raw_value,
                 normalized_value, quality_flag, computed_at)
            SELECT values.admin_area_id,
                   @metric_id,
                   @observed_at,
                   @reference_period,
                   values.density,
                   values.normalized,
                   'estimated',
                   @computed_at
            FROM unnest(@admin_area_ids, @densities, @normalized)
                AS values(admin_area_id, density, normalized)
            ON CONFLICT (admin_area_id, metric_id, observed_at) DO UPDATE SET
                reference_period = EXCLUDED.reference_period,
                raw_value = EXCLUDED.raw_value,
                normalized_value = EXCLUDED.normalized_value,
                quality_flag = EXCLUDED.quality_flag,
                computed_at = EXCLUDED.computed_at
            """;
        command.Parameters.AddWithValue(
            "admin_area_ids",
            densities.Select(density => density.AdminAreaId).ToArray());
        command.Parameters.AddWithValue(
            "densities",
            densities.Select(density => density.Density).ToArray());
        command.Parameters.AddWithValue("normalized", normalized.ToArray());
        command.Parameters.AddWithValue("metric_id", metricId);
        command.Parameters.AddWithValue("observed_at", metadata.ObservedAt);
        command.Parameters.AddWithValue("reference_period", metadata.ReferencePeriod);
        command.Parameters.AddWithValue("computed_at", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<NeighborhoodDensity>> ReadDensitiesAsync(
        WorldPopRecalibrationRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            SELECT s.admin_area_id,
                   s.population_estimated / s.area_km2 AS density
            FROM worldpop_neighborhood_sum s
            WHERE s.source_id = @source_id
              AND s.grid_year = @grid_year
              AND s.population_estimated IS NOT NULL
            ORDER BY s.admin_area_id
            """;
        command.Parameters.AddWithValue("source_id", request.SourceId);
        command.Parameters.AddWithValue("grid_year", request.GridYear);

        var densities = new List<NeighborhoodDensity>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            densities.Add(new NeighborhoodDensity(reader.GetInt64(0), reader.GetDouble(1)));
        }

        return densities;
    }

    private static async Task<IReadOnlyList<WorldPopNeighborhoodResult>> ReadNeighborhoodResultsAsync(
        WorldPopRecalibrationRequest request,
        long metricId,
        CancellationToken cancellationToken)
    {
        var metadata = request.Metadata;
        await using var command = request.Connection.CreateCommand();
        command.Transaction = request.Transaction;
        command.CommandText = """
            SELECT n.id,
                   d.name,
                   n.name,
                   s.population_raw,
                   s.area_km2,
                   c.coefficient,
                   s.population_estimated,
                   s.population_estimated / s.area_km2 AS density,
                   mv.normalized_value,
                   mv.quality_flag
            FROM worldpop_neighborhood_sum s
            JOIN admin_area n ON n.id = s.admin_area_id
            JOIN admin_area d ON d.id = n.parent_id
            JOIN worldpop_district_coefficient c
                ON c.district_id = d.id
               AND c.source_id = s.source_id
               AND c.grid_year = s.grid_year
               AND c.reference_year = @reference_year
            JOIN metric_value mv
                ON mv.admin_area_id = n.id
               AND mv.metric_id = @metric_id
               AND mv.observed_at = @observed_at
            WHERE s.source_id = @source_id AND s.grid_year = @grid_year
            ORDER BY d.name, n.name
            """;
        command.Parameters.AddWithValue("reference_year", request.ReferenceYear);
        command.Parameters.AddWithValue("metric_id", metricId);
        command.Parameters.AddWithValue("observed_at", metadata.ObservedAt);
        command.Parameters.AddWithValue("source_id", request.SourceId);
        command.Parameters.AddWithValue("grid_year", request.GridYear);

        var neighborhoods = new List<WorldPopNeighborhoodResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            neighborhoods.Add(new WorldPopNeighborhoodResult(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetDouble(6),
                reader.GetDouble(7),
                reader.GetDouble(8),
                reader.GetString(9)));
        }

        return neighborhoods;
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2
            : values[middle];
    }

    private sealed record TuikDistrict(
        string Code,
        string Name,
        string NameNormalized,
        int Population);

    private sealed record TuikProvince(string Code, string Name, int Population);

    private sealed record OsmDistrict(long Id, string Name, string NameNormalized);

    private sealed record NeighborhoodDensity(long AdminAreaId, double Density);

    private sealed record WorldpopDistrictSum(
        double Sum,
        int NeighborhoodCount,
        int ZeroNeighborhoodCount);
}
