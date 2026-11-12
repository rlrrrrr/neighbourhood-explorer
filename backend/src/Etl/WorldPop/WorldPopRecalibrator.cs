using System.Globalization;
using NeighbourhoodExplorer.Etl.Database;
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

public sealed record WorldPopMetricMetadata(
    MetricCatalogSource Source,
    MetricDefinition Definition,
    string ReferencePeriod,
    DateOnly ObservedAt);

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

public sealed record WorldPopExpectedCounts(
    int Neighborhoods,
    int Districts,
    int ProvincePopulation);

public sealed record WorldPopRecalibrationRequest(
    NpgsqlConnection Connection,
    NpgsqlTransaction Transaction,
    string SourceId,
    int GridYear,
    int ReferenceYear,
    WorldPopExpectedCounts ExpectedCounts,
    DateTimeOffset ComputedAt,
    WorldPopMetricMetadata Metadata);

public static class WorldPopRecalibrator
{
    public static async Task<WorldPopRecalibrationResult> RecalculateAsync(
        WorldPopRecalibrationRequest request,
        CancellationToken cancellationToken)
    {
        var readFailures = new List<string>();
        var tuikSourceId = await ReadTuikSourceAsync(request, readFailures, cancellationToken);
        var tuikDistricts = tuikSourceId is null
            ? []
            : await ReadTuikDistrictsAsync(request, tuikSourceId, readFailures, cancellationToken);
        var tuikProvince = tuikSourceId is null
            ? null
            : await ReadTuikProvinceAsync(request, tuikSourceId, readFailures, cancellationToken);
        var osmDistricts = await ReadOsmDistrictsAsync(request, readFailures, cancellationToken);
        var worldpopSums = await ReadWorldpopDistrictSumsAsync(request, cancellationToken);
        var totalRaw = await ReadTotalRawAsync(request, cancellationToken);

        var plan = WorldPopRecalibrationPlanner.Build(
            new WorldPopPlannerInput(
                tuikDistricts,
                tuikProvince,
                osmDistricts,
                worldpopSums,
                request.ExpectedCounts.Districts,
                request.ExpectedCounts.ProvincePopulation));

        var failures = readFailures.Concat(plan.Failures).ToList();
        if (failures.Count > 0)
        {
            return BuildFailureResult(plan, totalRaw, failures);
        }

        await ReplaceCoefficientsAsync(request, plan.Coefficients, cancellationToken);
        await UpdateEstimatedPopulationsAsync(request, cancellationToken);
        await CheckDistrictSumsAsync(request, plan.Coefficients, failures, cancellationToken);

        if (failures.Count > 0)
        {
            return BuildFailureResult(plan, totalRaw, failures);
        }

        var neighborhoodDensities = await ReadDensitiesAsync(request, cancellationToken);
        var normalizedDensities = PercentileRank.Normalize(
            neighborhoodDensities.Select(density => density.Density).ToList());
        var metricWrite = await MetricSeriesWriter.WriteAsync(
            new MetricSeriesWriteRequest(
                request.Connection,
                request.Transaction,
                request.Metadata.Source,
                request.Metadata.ReferencePeriod,
                request.Metadata.ObservedAt,
                request.ComputedAt,
                [
                    new MetricSeries(
                        request.Metadata.Definition,
                        neighborhoodDensities
                            .Select((density, index) => new MetricValue(
                                density.AdminAreaId,
                                density.Density,
                                normalizedDensities[index]))
                            .ToArray()),
                ]),
            cancellationToken);
        var metricId = metricWrite.MetricIds[request.Metadata.Definition.Key];
        var neighborhoods = await ReadNeighborhoodResultsAsync(request, metricId, cancellationToken);
        var totalEstimated = neighborhoods.Sum(neighborhood => neighborhood.PopulationEstimated);
        var totalAdnks = plan.TotalAdnks;

        if (neighborhoods.Count != request.ExpectedCounts.Neighborhoods)
        {
            failures.Add(
                $"métrique : {neighborhoods.Count} valeurs de mahalle au lieu de " +
                $"{request.ExpectedCounts.Neighborhoods}.");
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
            plan.Coefficients.Count,
            neighborhoods.Count,
            totalRaw,
            totalAdnks,
            totalEstimated,
            plan.Coefficients.Count(coefficient => coefficient.Flagged),
            plan.Coefficients,
            neighborhoods,
            plan.UnmatchedDistricts,
            failures,
            metricId,
            densities.Count == 0 ? 0 : densities[0],
            densities.Count == 0 ? 0 : densities[^1],
            Median(densities));
    }

    private static WorldPopRecalibrationResult BuildFailureResult(
        WorldPopPlan plan,
        double totalRaw,
        IReadOnlyList<string> failures) =>
        new(
            plan.Coefficients.Count,
            0,
            totalRaw,
            plan.TotalAdnks,
            0,
            plan.Coefficients.Count(coefficient => coefficient.Flagged),
            plan.Coefficients,
            [],
            plan.UnmatchedDistricts,
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

    private static async Task<IReadOnlyList<WorldPopTuikDistrict>> ReadTuikDistrictsAsync(
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

        var districts = new List<WorldPopTuikDistrict>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            districts.Add(new WorldPopTuikDistrict(
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

    private static async Task<WorldPopTuikProvince?> ReadTuikProvinceAsync(
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

        var provinces = new List<WorldPopTuikProvince>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            provinces.Add(new WorldPopTuikProvince(
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

    private static async Task<IReadOnlyList<WorldPopOsmDistrict>> ReadOsmDistrictsAsync(
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

        var districts = new List<WorldPopOsmDistrict>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            districts.Add(new WorldPopOsmDistrict(
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

    private static async Task<IReadOnlyDictionary<long, WorldPopDistrictSum>> ReadWorldpopDistrictSumsAsync(
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

        var sums = new Dictionary<long, WorldPopDistrictSum>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sums[reader.GetInt64(0)] = new WorldPopDistrictSum(
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
            insert.Parameters.AddWithValue("computed_at", request.ComputedAt);
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

    private sealed record NeighborhoodDensity(long AdminAreaId, double Density);
}
