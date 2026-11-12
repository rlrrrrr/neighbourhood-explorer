using System.Globalization;
using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Infrastructure;
using NeighbourhoodExplorer.Etl.Manifests;
using NeighbourhoodExplorer.Etl.Reports;
using NeighbourhoodExplorer.Etl.Transport;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Pipelines;

public sealed class IbbTransportPipeline(
    NpgsqlDataSource dataSource,
    IEtlConsole console,
    TimeProvider timeProvider) : IEtlPipeline
{
    public PipelineId Id => PipelineId.IbbTransport;

    public async Task<EtlOutcome> RunAsync(
        EtlPipelineRequest request,
        CancellationToken cancellationToken)
    {
        var expectedNeighborhoods = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.Neighborhoods,
            request.Manifest.Id);
        var expectedDistricts = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.Districts,
            request.Manifest.Id);
        var expectedBusStops = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.BusStops,
            request.Manifest.Id);
        var expectedRailStations = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.RailStations,
            request.Manifest.Id);
        var expectedFerryPiers = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.FerryPiers,
            request.Manifest.Id);
        var dataSourceId = request.Manifest.DataSourceId ?? request.Manifest.Id;
        var rawSourceId = request.Manifest.RawSourceId ?? request.Manifest.Id;

        var bundle = await TransportBundleReader.ReadAsync(
            request.DataDirectory,
            rawSourceId,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Manifest.ChecksumSha256)
            && !string.Equals(
                bundle.Checksum,
                request.Manifest.ChecksumSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new EtlFailureException(
                $"SHA-256 du lot ({bundle.Checksum}) différent de celui du manifeste " +
                $"({request.Manifest.ChecksumSha256}).");
        }

        RequireCount(bundle.Bus.Points.Count, expectedBusStops, "bus_stops");
        RequireCount(bundle.Rail.Points.Count, expectedRailStations, "rail_stations");
        RequireCount(bundle.Ferry.Points.Count, expectedFerryPiers, "ferry_piers");

        var computedAt = timeProvider.GetUtcNow();
        var observedAt = DateOnly.Parse(
            request.Manifest.ReferencePeriod,
            CultureInfo.InvariantCulture);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var scoped = await TransportProvinceFilter.ApplyAsync(
            connection,
            transaction,
            bundle.Points,
            cancellationToken);
        var deduplicated = TransportPointDeduplicator.Deduplicate(scoped.Points);
        console.WriteLine(
            $"ETL: transports : {bundle.Bus.Points.Count} arrêts de bus, " +
            $"{bundle.Rail.Points.Count} stations rail, {bundle.Ferry.Points.Count} pontons ; " +
            $"{scoped.Excluded.Count} points hors de la province d'Istanbul exclus ; " +
            $"{deduplicated.Points.Count} points distincts après dédoublonnage.");

        var counted = await TransportNeighborhoodCounter.CountAsync(
            connection,
            transaction,
            deduplicated.Points,
            cancellationToken);
        var metrics = TransportMetricCalculator.Calculate(counted.Neighborhoods);
        var districtCount = counted.Neighborhoods
            .Select(neighborhood => neighborhood.DistrictName)
            .Distinct()
            .Count();

        console.WriteLine(
            $"ETL: transports : {counted.Neighborhoods.Count} mahalles, {districtCount} ilçe, " +
            $"{counted.Assignments.Polygon} points dans un polygone, " +
            $"{counted.Assignments.Nearest} rattachés au plus proche, " +
            $"{counted.Assignments.Unassigned} non affectés.");

        var series = metrics.Series[0];
        var metricWrite = await MetricSeriesWriter.WriteAsync(
            new MetricSeriesWriteRequest(
                connection,
                transaction,
                new MetricCatalogSource(
                    dataSourceId,
                    request.Manifest.Name,
                    request.Manifest.Organization,
                    request.Manifest.Url,
                    request.Manifest.License,
                    request.Manifest.AttributionText,
                    TransportMetricCatalog.SourceMethodology,
                    DateOnly.Parse(
                        request.Manifest.RetrievedAt!,
                        CultureInfo.InvariantCulture),
                    request.Manifest.SourceVersion ?? string.Empty),
                request.Manifest.ReferencePeriod,
                observedAt,
                computedAt,
                metrics.Series),
            cancellationToken);

        var neighborhoods = new List<TransportNeighborhoodResult>(counted.Neighborhoods.Count);
        for (var index = 0; index < counted.Neighborhoods.Count; index++)
        {
            var neighborhood = counted.Neighborhoods[index];
            var value = series.Values[index];
            neighborhoods.Add(new TransportNeighborhoodResult(
                neighborhood.DistrictName,
                neighborhood.NeighborhoodName,
                neighborhood.BusStops,
                neighborhood.RailStations,
                neighborhood.FerryPiers,
                TransportMetricCalculator.WeightedPoints(neighborhood),
                value.RawValue,
                value.NormalizedValue));
        }

        var failures = new List<string>();
        new CoverageScope
        {
            NeighborhoodCount = counted.Neighborhoods.Count,
            DistrictCount = districtCount,
            ExpectedNeighborhoods = expectedNeighborhoods,
            ExpectedDistricts = expectedDistricts,
        }.AppendFailures(failures);
        if (counted.Assignments.Unassigned > 0)
        {
            failures.Add($"affectation : {counted.Assignments.Unassigned} point(s) de transport sans mahalle.");
        }

        new MetricCoverageCheck
        {
            MetricKey = series.Definition.Key,
            ExpectedValues = counted.Neighborhoods.Count,
            Values = series.Values,
        }.AppendFailures(failures);

        var distinct = deduplicated.Stats.ToDictionary(stats => stats.Mode);
        var outsideProvince = Enum.GetValues<TransportMode>()
            .Select(mode => new TransportModePointCount(
                mode.ToWireValue(),
                scoped.Excluded.Count(point => point.Mode == mode)))
            .ToArray();
        var files = new List<TransportSourceFileResult>
        {
            new(
                TransportBundleReader.IettStopsFile,
                bundle.Bus.Points.Count,
                bundle.Bus.Rejected,
                bundle.FileChecksums[TransportBundleReader.IettStopsFile]),
            new(
                TransportBundleReader.MetroStationsFile,
                bundle.Rail.Points.Count,
                bundle.Rail.Rejected,
                bundle.FileChecksums[TransportBundleReader.MetroStationsFile]),
            new(
                TransportBundleReader.FerryPiersFile,
                bundle.Ferry.Points.Count,
                bundle.Ferry.Rejected,
                bundle.FileChecksums[TransportBundleReader.FerryPiersFile]),
        };

        var report = new TransportCheckReport(
            request.Manifest.Id,
            dataSourceId,
            request.Manifest.Name,
            request.Manifest.Url,
            request.Manifest.License,
            request.Manifest.AttributionText,
            request.Manifest.SourceVersion ?? string.Empty,
            bundle.Checksum,
            files,
            outsideProvince,
            bundle.Bus.Points.Count,
            bundle.Rail.Points.Count,
            bundle.Ferry.Points.Count,
            distinct[TransportMode.Bus].Distinct,
            distinct[TransportMode.Rail].Distinct,
            distinct[TransportMode.Ferry].Distinct,
            counted.Assignments.Polygon,
            counted.Assignments.Nearest,
            counted.Assignments.Unassigned,
            counted.Assignments.UnassignedPoints
                .Select(point =>
                    $"{point.Mode.ToWireValue()} · {point.Name} " +
                    $"({point.Longitude:0.#####}, {point.Latitude:0.#####})")
                .ToArray(),
            observedAt.Year,
            request.Manifest.ReferencePeriod,
            observedAt,
            computedAt,
            counted.Neighborhoods.Count,
            districtCount,
            expectedNeighborhoods,
            expectedDistricts,
            neighborhoods,
            failures);

        await TransportCheckReportWriter.WriteAsync(
            request.DataDirectory,
            report,
            cancellationToken);

        var success = failures.Count == 0;
        if (success)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        console.WriteLine(
            $"ETL: rapport transports : " +
            $"{Path.Combine(request.DataDirectory, "processed", request.Manifest.Id, "transport-check.md")}");

        return new EtlOutcome(
            success,
            bundle.Checksum,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["neighborhoods"] = counted.Neighborhoods.Count,
                ["districts"] = districtCount,
                ["bus_points"] = bundle.Bus.Points.Count,
                ["rail_points"] = bundle.Rail.Points.Count,
                ["ferry_points"] = bundle.Ferry.Points.Count,
                ["outside_province"] = scoped.Excluded.Count,
                ["distinct_bus"] = distinct[TransportMode.Bus].Distinct,
                ["distinct_rail"] = distinct[TransportMode.Rail].Distinct,
                ["distinct_ferry"] = distinct[TransportMode.Ferry].Distinct,
                ["polygon_assigned"] = counted.Assignments.Polygon,
                ["nearest_assigned"] = counted.Assignments.Nearest,
                ["unassigned"] = counted.Assignments.Unassigned,
                ["metrics"] = 1,
                ["metric_values"] = series.Values.Count,
                ["metric_id"] = (int)metricWrite.MetricIds[series.Definition.Key],
            },
            success ? null : string.Join(" ; ", failures));
    }

    private static void RequireCount(int actual, int expected, string field)
    {
        if (actual != expected)
        {
            throw new EtlFailureException(
                $"transports : {actual} points pour « {field} » au lieu de {expected} " +
                "(mettre le manifeste à jour si la source a changé).");
        }
    }
}
